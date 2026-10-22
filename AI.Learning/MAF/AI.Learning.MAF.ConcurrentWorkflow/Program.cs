// ========================================
// 1. OpenAI Configuration
// ========================================

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var configuration = config.GetRequiredSection("Deepseek");

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
// 3. Create Agents
// ========================================

var weartherAgent = new ChatClientAgent(chatClient, """
    You are a weather forcaster. When given a city, provide a brief 
    but informative weather forcast including temperature, conditions, 
    and what to wear. Keep it to 2-3 sentences.
    """, "WeatherForeccaster");

var newsAgent = new ChatClientAgent(chatClient, new ChatClientAgentOptions()
{
    ChatOptions = new ChatOptions()
    {
        Instructions = """
        You are a news briefing agent. When given a topic or genteral 
        request, provide 3 plausible top headlines with one-sentence 
        summaries each. Present them as numbered list.
        """
    },
    Name = "NewsBriefer"
});

var productivityAgent = new ChatClientAgent(chatClient, """
    You are a productivity coach. Provide a motivational tip of the
    day and suggest 3 focus priorities. Keep it brief and actionable.
    """, "ProductivityCoach");


// ========================================
// 4. Build Workflow
// ========================================

//var workflow = new WorkflowBuilder(weartherAgent)
//    .AddEdge(weartherAgent, newsAgent)
//    .AddEdge(newsAgent, productivityAgent)
//    .WithOutputFrom(weartherAgent, newsAgent, productivityAgent)
//    .Build();


var workflow = AgentWorkflowBuilder.BuildConcurrent([weartherAgent, newsAgent, productivityAgent]);


// ========================================
// 5. Execute Workflow
// ========================================

Console.WriteLine("Morning Briefing");
Console.WriteLine();

var messages = new List<ChatMessage>()
{
    new ChatMessage(ChatRole.User,"Give me my morning briefing. I'm in Xian and interested in teach news.")
};

var stopwatch = Stopwatch.StartNew();

await using StreamingRun runner = await InProcessExecution.RunStreamingAsync(workflow, messages);

await runner.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach(WorkflowEvent workflowEvent in runner.WatchStreamAsync())
{
    switch (workflowEvent)
    {
        case WorkflowOutputEvent workflowOutputEvent:
            Console.Write(workflowOutputEvent.Data);
            break;
    }
}

stopwatch.Stop();
Console.WriteLine();
Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F1}s");