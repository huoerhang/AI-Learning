using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

// ========================================
// 1. OpenAI Configuration
// ========================================

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var configuration = config.GetRequiredSection("Groq");

var endpoint = configuration["Endpoint"];

var modelName = configuration["Model"];

var apiKey = configuration["ApiKey"];


// ========================================
// 2. Create OpenAI Chat Client
// ========================================

var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
{
    Endpoint = new Uri(endpoint)
});

var chatClient = openAiClient.GetChatClient(modelName).AsIChatClient();

// ========================================
// 2. Create Agents
// ========================================

var emailAssistant = new ChatClientAgent(chatClient, "You are an email assistant. Draft professional concise reponses.", "EmailAssistant");

// ========================================
// 3. Create Executors
// ========================================

var ingestion = new EmailIngestionExecutor();
var responder = new EmailResponderExecutor(emailAssistant);

// ========================================
// 4. Create Executors
// ========================================

var workflow = new WorkflowBuilder(ingestion).AddEdge(ingestion, responder).WithOutputFrom(responder).Build();

// ========================================
// 5. Execute Workflow
// ========================================

Console.WriteLine("Email Processor - using shared state");
Console.WriteLine();

var emailContent = "Hi, I wanted to follow up on our meeting yesterday. Can we shedule a call to discuss the proposal?";

await using var runner = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, emailContent));

await runner.TrySendMessageAsync(new TurnToken(true));

await foreach (var workflowEvent in runner.WatchStreamAsync())
{
    switch (workflowEvent)
    {
        case ExecutorInvokedEvent invokedEvent:
            Console.WriteLine();
            Console.WriteLine($"{invokedEvent.ExecutorId} starting");
            break;
        case ExecutorCompletedEvent completedEvent:
            Console.WriteLine();
            Console.WriteLine($"{completedEvent.ExecutorId} done");
            break;
        case WorkflowOutputEvent outputEvent:
            Console.WriteLine(workflowEvent.Data);
            break;
        case ProcessingStateEvent processingStateEvent:
            Console.WriteLine();
            Console.WriteLine($"Stage: {processingStateEvent.Data}");
            break;
        case MetricsEvent metricEvent:
            var metric = (ProcessingMetrices)metricEvent.Data;
            Console.WriteLine();
            Console.WriteLine($"Word count: {metric.WorkCount}");
            Console.WriteLine($"Character count: {metric.CharacterCount}");
            Console.WriteLine($"Elapsed time: {metric.ProcessingTime} ms");
            break;
    }
}

public sealed class Email
{
    [JsonPropertyName("email_id")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("email_content")]
    public string EmailContent { get; set; } = string.Empty;
}

public class EmailIngestionExecutor : Executor<ChatMessage, string>
{
    public EmailIngestionExecutor() : base(nameof(EmailIngestionExecutor))
    {

    }
    public async override ValueTask<string> HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var emailId = Guid.NewGuid().ToString();

        var email = new Email
        {
            EmailId = emailId,
            EmailContent = message.Text
        };

        await context.QueueStateUpdateAsync(emailId, email, scopeName: "EmialStore");

        return emailId;
    }
}

[YieldsOutput(typeof(string))]
public class EmailResponderExecutor : Executor<string>
{
    private readonly AIAgent _agent;
    public EmailResponderExecutor(AIAgent agent) : base(nameof(EmailResponderExecutor))
    {
        _agent = agent;
    }

    public override async ValueTask HandleAsync(string emailId, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(new ProcessingStateEvent("Drafting response..."));

        var stopwatch = Stopwatch.StartNew();

        var email = await context.ReadStateAsync<Email>(emailId, scopeName: "EmialStore");

        var response = await _agent.RunAsync($"Draf a professional response to othis email: {email.EmailContent}");

        await context.YieldOutputAsync(response);

        stopwatch.Stop();

        var wordCount = response.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var processingMetric = new ProcessingMetrices()
        {
            CharacterCount = response.Text.Length,
            WorkCount = wordCount,
            ProcessingTime = stopwatch.Elapsed
        };

        await context.AddEventAsync(new MetricsEvent(processingMetric));

        await context.AddEventAsync(new ProcessingStateEvent("Response drafted..."));
    }
}

public class ProcessingMetrices
{
    public int WorkCount { get; set; }

    public int CharacterCount { get; set; }

    public TimeSpan ProcessingTime { get; set; }
}

internal class ProcessingStateEvent : WorkflowEvent
{
    public ProcessingStateEvent(string stage) : base(stage)
    {

    }
}

public class MetricsEvent : WorkflowEvent
{
    public MetricsEvent(ProcessingMetrices metrices) : base(metrices)
    {

    }
}