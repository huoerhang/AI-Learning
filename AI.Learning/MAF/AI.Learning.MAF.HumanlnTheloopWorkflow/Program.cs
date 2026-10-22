using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;

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
// 3. Create Tools
// ========================================

[Description("Checks the current status of the staging environment")]
static string CheckStagingStatus()
{
    return "Staging environment is healthy. All 47 tests passing. Last deployment: 2 hours ago. No errors in the last 24 hours.";
}

[Description("Deploys the application to production. This is irreverible.")]
static string DeployToProduction([Description("The version to deploy")] string version)
{
    return $"Successfully deployed version {version} to production. All health checks passing.";
}


// ========================================
// 4. Create Agents
// ========================================

var deployAgent = new ChatClientAgent(chatClient, "You are a Devops engineer. Check staging status first, the deploy to product.", "DeployAgent", "Handles deployments",
    [
        AIFunctionFactory.Create(CheckStagingStatus),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeployToProduction))
    ]);

var verifyAgent = new ChatClientAgent(chatClient, "You are a QA engineer. Verify the deployment was successful and summarize the results.", "VerifyAgent");

// ========================================
// 5. Build Workflow
// ========================================

var workflow = AgentWorkflowBuilder.BuildSequential([deployAgent, verifyAgent]);

// ========================================
// 6. Execute Workflow
// ========================================

Console.WriteLine("Deployment Pipeline");

var message = new ChatMessage(ChatRole.User, "Deploy version 2.1.0 to production");
await using var runner = await InProcessExecution.RunStreamingAsync(workflow, message);
await runner.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var workflowEvent in runner.WatchStreamAsync())
{
    switch (workflowEvent)
    {
        case RequestInfoEvent requestInfoEvent when requestInfoEvent.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequestContent):
            Console.WriteLine();
            Console.WriteLine("Approval required:");
            Console.WriteLine("Approve? (y / n)");
            var isApproved = Console.ReadLine().Trim().ToLower() == "y";
            await runner.SendResponseAsync(requestInfoEvent.Request.CreateResponse(approvalRequestContent.CreateResponse(approved: isApproved)));
            Console.WriteLine(isApproved ? "Approved - resuming workflow" : "Rejected - workflow will handle rejection");
            break;
        case AgentResponseUpdateEvent agentResponseUpdateEvent:
            Console.Write(agentResponseUpdateEvent.Update.Text);
            break;
        case WorkflowOutputEvent workflowOutputEvent:
            Console.WriteLine();
            Console.WriteLine("Pipeline complete");
            break;
    }
}