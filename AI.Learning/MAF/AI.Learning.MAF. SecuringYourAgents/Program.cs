using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ComponentModel;
using System.Text.RegularExpressions;

// ========================================
// 1. OpenAI Configuration
// ========================================

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var configuration = config.GetRequiredSection("Groq");

var endpoint = configuration["Endpoint"];

var modelName = configuration["Model"];

var apiKey = configuration["ApiKey"];

// ========================================
// 2. Create Chant Client
// ========================================

var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
{
    Endpoint = new Uri(endpoint)
});

var chatClient = openAiClient.GetChatClient(modelName).AsIChatClient();

// ========================================
// 3. Create Agent
// ========================================

var lookupFunction = AIFunctionFactory.Create(LookupOrder);
var discountFunction = AIFunctionFactory.Create(ApplyDiscount);
var approvalRequiredDiscount = new ApprovalRequiredAIFunction(discountFunction);

var supportBot = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    ChatOptions = new ChatOptions()
    {
        Instructions = """
        You are a customer support agent for Contoso Electronics.
        Help customers with order questions.
        You may offer small discounts when appropriate.
        Never promise a discount until the toll succeeds.
        """,
        Tools = [lookupFunction, approvalRequiredDiscount]
    },
    Name = "ContosoSupport"
});

var session = await supportBot.CreateSessionAsync();


// ========================================
// 4. Execute Agent
// ========================================

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input)) break;

    var agentRunOptions = new ChatClientAgentRunOptions()
    {
        ChatOptions = new ChatOptions()
        {
            MaxOutputTokens = 400
        }
    };

    var response = await supportBot.RunAsync(input, session, agentRunOptions);
    var output = await HandleApprovalRequestsAsync(response, supportBot, session);

    var sanitizedOutput = Regex.Replace(output.Text, "<[^>]+>", "");

    Console.WriteLine(sanitizedOutput);
}

[Description("Looks up a customer's order by order ID.")]
static string LookupOrder([Description("The order ID to look up.")] string orderId)
{
    var normalizedOrderId = NormalizeOrderId(orderId);

    if (!Regex.IsMatch(normalizedOrderId, "^ORD-[0-9]{3}$")) return $"Error: Order IDs nust match the format ORD-123";

    return normalizedOrderId switch
    {
        "ORD-001" => "Order ORD-001: Surface Latop, shipped 2026-10-25, delivered.",
        "ORD-002" => "Order ORD-002: Xbox Controller, processing, estimated delivery 2026-11-01",
        _ => $"Order {normalizedOrderId} was not found"
    };
}

[Description("Applies a discount to a customer's order")]
static string ApplyDiscount([Description("The order ID")] string orderId, [Description("The discount percentage to apply.")] int percentage)
{
    var normalizedOrderId = NormalizeOrderId(orderId);

    if (!Regex.IsMatch(normalizedOrderId, "^ORD-[0-9]{3}$")) return $"Error: Order IDs nust match the format ORD-123";

    if (percentage < 1 || percentage > 10) return $"Error: Discounts must be between 1 and 10 percent. Requested: {percentage}";

    return $"Discount of {percentage}% approved for {normalizedOrderId}";
}

static string NormalizeOrderId(string orderId)
{
    return string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim().ToUpperInvariant();
}

static async Task<AgentResponse> HandleApprovalRequestsAsync(AgentResponse response, AIAgent agent, AgentSession session)
{
    while (true)
    {
        var approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();

        if (approvalRequests.Count == 0) return response;

        foreach (var request in approvalRequests)
        {
            Console.WriteLine("Approval required for a tool call.");
            Console.Write("Approve? (y/n):");

            var approved = string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
            var approvalMessage = new ChatMessage(ChatRole.Tool, [request.CreateResponse(approved)]);

            response = await agent.RunAsync(approvalMessage, session);
        }
    }
}