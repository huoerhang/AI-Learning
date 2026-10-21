using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var deepSeekSection = config.GetSection("Deepseek");

var endpoint = deepSeekSection["Endpoint"];
var modelName = deepSeekSection["Model"];
var apiKey = deepSeekSection["ApiKey"];

var aiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
{
    Endpoint = new Uri(endpoint)
});

var chatClient = aiClient.GetChatClient(modelName).AsIChatClient();
var researcher = new ChatClientAgent(chatClient, """
You are a research agent. When given a topic, provide detailed,
factual information about it. Include multiple perspectives and
relevant data points. Structure your response as raw research
notes – don't worry about polish, just be thorough and accurate.
Aim for 3–5 paragraphs of detailed findings.
"""
);
var factChecker = new ChatClientAgent(chatClient, """
You are a fact-checking specialist. You receive claims or statements and evaluate their accuracy. 
For each claim, state whether it's accurate, partially accurate, or inaccurate, and provide a brief explanation. 
Be honest when something cannot be verified.
"""
);
var summarizer = new ChatClientAgent(chatClient, """    
You are a summarization specialist. You receive raw research notes
and distill them into a clear, concise summary. Structure your
output as:
- A one-sentence overview
- 3–5 bullet points with the key findings
- A brief conclusion
Keep it tight – no fluff, no repetition. 
"""
);

var workflow = AgentWorkflowBuilder.BuildSequential([researcher, factChecker, summarizer]);

Console.WriteLine("Research Assistant - type a topic a research");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Topic: ");
    var topic = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(topic)) break;

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, topic));

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    string? latestExecutorId = null;

    await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
    {
        switch (workflowEvent)
        {
            case ExecutorCompletedEvent executorCompletedEvent:
                Console.WriteLine(executorCompletedEvent.Data);
                break;
            case AgentResponseUpdateEvent updateEvent:
                Console.Write($"{updateEvent.Data}");
                break;
            case WorkflowOutputEvent outputEvent:
                Console.WriteLine();
                Console.WriteLine("Workflow completed");
                Console.WriteLine("---------------------------");
                break;
        }
    }
}