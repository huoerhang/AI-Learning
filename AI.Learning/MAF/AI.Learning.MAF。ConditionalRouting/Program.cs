
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;

using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

// ========================================
// 1. OpenAI Configuration
// ========================================

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var groqSection = config.GetRequiredSection("Groq");

var endpoint = groqSection["Endpoint"]
    ?? throw new InvalidOperationException("Groq:Endpoint is missing.");

var modelName = groqSection["Model"]
    ?? throw new InvalidOperationException("Groq:Model is missing.");

var apiKey = groqSection["ApiKey"]
    ?? throw new InvalidOperationException("Groq:ApiKey is missing.");

// ========================================
// 2. Create OpenAI Chat Client
// ========================================

var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = new Uri(endpoint)
});

IChatClient chatClient = openAIClient
    .GetChatClient(modelName)
    .AsIChatClient();

// ========================================
// 3. Create Agents
// ========================================

ChatClientAgent spamDetectorAgent = new(
    chatClient,
    new ChatClientAgentOptions
    {
        ChatOptions = new()
        {
            Instructions = """
                You are a spam detection assistant.
                Analyze the email and determine if it is spam.
                Be decisive.
                """,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                AIJsonUtilities.CreateJsonSchema(
                    typeof(DetectionResult)))
        }
    });

ChatClientAgent emailAssistantAgent = new(
    chatClient,
    "You are an email assistant that drafts professional responses.",
    "EmailAssistant");

// ========================================
// 4. Create Executors
// ========================================

var spamDetectionExecutor = new SpamDetectionExecutor(spamDetectorAgent);

var legitimateHandler = new LegitimateEmailExecutor(emailAssistantAgent);

var spamHandler = new SpamHandlerExecutor();

var uncertainHandler = new UncertainHandlerExecutor();

// ========================================
// 5. Build Workflow
// ========================================

Func<DetectionResult?, bool> condition(SpamDecision expected)
    => result => result is not null
                 && result.Decision == expected;

WorkflowBuilder builder = new(spamDetectionExecutor);

builder.AddSwitch(
    spamDetectionExecutor,
    switchBuilder => switchBuilder
        .AddCase(condition(SpamDecision.Spam), spamHandler)
        .AddCase(condition(SpamDecision.NotSpam), legitimateHandler)
        .WithDefault(uncertainHandler)
    )
    .WithOutputFrom(spamHandler, legitimateHandler, uncertainHandler);

var workflow = builder.Build();

// ========================================
// 6. Execute Workflow
// ========================================

Console.WriteLine("Email Classfier - enter an email to classify (Ctrl+C to quit)");
Console.WriteLine();

string[] testEmails =
[
    "Congratulations! You've won $1,000,000! Click here to claim!",
    "Hi, I wanted to follow up on our meeting yesterday about the project proposal."
];

foreach (var email in testEmails)
{
    Console.WriteLine($"Email: {email}");

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, email));

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    await foreach (var workflowEvent in run.WatchStreamAsync())
    {
        if (workflowEvent is WorkflowOutputEvent outputEvent)
        {
            Console.WriteLine($"Result: {outputEvent.Data}");
            break;
        }
    }

    Console.WriteLine();
}

// ========================================
// Executors
// ========================================

class SpamDetectionExecutor
    : Executor<ChatMessage, DetectionResult>
{
    private readonly AIAgent _agent;

    public SpamDetectionExecutor(AIAgent agent)
        : base(nameof(SpamDetectionExecutor))
    {
        _agent = agent;
    }

    public override async ValueTask<DetectionResult> HandleAsync(
        ChatMessage message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(message);

        var result = JsonSerializer.Deserialize<DetectionResult>(
            response.Text);

        return result
            ?? throw new JsonException("Invalid detection result.");
    }
}

class LegitimateEmailExecutor
    : Executor<DetectionResult, string>
{
    private readonly AIAgent _agent;

    public LegitimateEmailExecutor(AIAgent agent)
        : base(nameof(LegitimateEmailExecutor))
    {
        _agent = agent;
    }

    public override async ValueTask<string> HandleAsync(
        DetectionResult message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        return $"Draft a professional reply to this legitimate email. Detection notes: {message.Reason}";
    }
}

class SpamHandlerExecutor
    : Executor<DetectionResult, string>
{
    public SpamHandlerExecutor()
        : base(nameof(SpamHandlerExecutor))
    {
    }

    public override async ValueTask<string> HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return $"Email marked as spam: {message.Reason}";
    }
}

class UncertainHandlerExecutor
    : Executor<DetectionResult, string>
{
    public UncertainHandlerExecutor()
        : base(nameof(UncertainHandlerExecutor))
    {
    }

    public override async ValueTask<string> HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return $"Email needs manual review: {message.Reason}";
    }
}

// ========================================
// Models
// ========================================

public enum SpamDecision
{
    NotSpam,
    Spam,
    Uncertain
}

public class DetectionResult
{
    [JsonPropertyName("spam_decision")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SpamDecision Decision { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
