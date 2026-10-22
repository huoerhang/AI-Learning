using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

// ========================================
// 1. OpenAI Configuration
// ========================================

const string serviceName = "AI.Learning.MAF.TracingAgentCalls";

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var configuration = config.GetRequiredSection("Deepseek");

var endpoint = configuration["Endpoint"];

var modelName = configuration["Model"];

var apiKey = configuration["ApiKey"];

// ========================================
// 2. Create Tracing
// ========================================

var activitySource = new ActivitySource("AgentTracking");
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
    .AddHttpClientInstrumentation()
    .AddSource("AgentTracking")
    .AddSource("Microsoft.Agents.*")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
    }).Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
    .AddMeter("Agentracking")
    .AddHttpClientInstrumentation()
    .AddRuntimeInstrumentation()
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
    }).Build();

// ========================================
// 2. Create OpenAI Chat Client
// ========================================

var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
{
    Endpoint = new Uri(endpoint)
}).GetChatClient(modelName).AsIChatClient().AsBuilder().UseOpenTelemetry(configure: config =>
{
    config.EnableSensitiveData = true;
}).Build();

// ========================================
// 3. Create Agent
// ========================================

//var weatherAgent = openAiClient.GetChatClient(modelName).AsAIAgent(
//    new ChatClientAgentOptions()
//    {
//        ChatOptions = new ChatOptions()
//        {
//            Instructions = "You are a helpful weather assistant. Use the available tools to answer weather questions. Be concise.",
//            Tools = [AIFunctionFactory.Create(GetCurrentWeather)]
//        },
//        Name = "WeatherBot"
//    });

var weatherAgent = new ChatClientAgent(openAiClient, new ChatClientAgentOptions
{
    ChatOptions = new ChatOptions
    {
        Instructions = "You are a helpful weather assistant. Use the available tools to answer weather questions. Be concise.",
        Tools = [AIFunctionFactory.Create(GetCurrentWeather)]
    },
    Name = "WeatherBot"
}).AsBuilder().UseOpenTelemetry(configure: config => config.EnableSensitiveData = true).Build();

var session = await weatherAgent.CreateSessionAsync();

// ========================================
// 4. Execute Agent
// ========================================
var questions = new string[]
{
    "What's the weather in Beijing?",
    "About Xi'an?",
    "Compare the two - which city is warmer?"
};

foreach (var question in questions)
{
    using var activity = activitySource.StartActivity($"User turn: {question}");
    activity.SetTag("user.message", question);

    Console.WriteLine($"You: {question}");
    var resposne = await weatherAgent.RunAsync(question, session);
    Console.WriteLine($"Agent: {resposne}");
    Console.WriteLine();

    activity.SetTag("agent.response_length", resposne.ToString().Length);
}


[Description("Gets the current weather for a given city")]
static string GetCurrentWeather([Description("The city to get the weather for")] string city)
{
    Thread.Sleep(200);

    return city.ToLower() switch
    {
        "beijing" => "Cloundy, 14°",
        "xi'an" => "Rainy, 11°",
        "yulin" => "Sunny, 23°"
    };
}