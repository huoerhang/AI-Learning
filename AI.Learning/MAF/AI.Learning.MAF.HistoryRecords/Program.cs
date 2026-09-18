using AI.Learning.Common;
using AI.Learning.MAF.HistoryRecords;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using StackExchange.Redis;
using System.ClientModel;

var redis = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:6379");

var historyProvider = new RedisChatHistoryProvider(redis, keyPrefix: "chat_history", maxMessages: 1000);

AIAgent agent = new OpenAIClient(
        credential: new ApiKeyCredential(OpenAIConsts.ApiKey),
        options: new OpenAIClientOptions { Endpoint = new Uri(OpenAIConsts.Url) })
    .GetChatClient(model: OpenAIConsts.ModelName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "Helper",
        Description = "一个示例 AI Agent，使用 OpenAI Chat Completion API。",
        ChatOptions = new ChatOptions
        {
            Instructions = "你是一个乐于助人的助手，必要时调用工具",
            Tools = [AIFunctionFactory.Create(AIFunctions.GetWeather)]
        },

        ChatHistoryProvider = historyProvider
    })
        .AsBuilder()
    .UseOpenTelemetry(sourceName: "AAA", configure: cfg =>
    {
        cfg.EnableSensitiveData = true;
    }).Build();


// 1. 建一个空 session
AgentSession restored = await agent.CreateSessionAsync();

// 2. 把已知的 ConversationId 塞进 StateBag（provider 那一格）
restored.StateBag.SetValue("RedisChatHistoryProvider", new RedisChatHistoryProvider.State("conv-3e9b751b124d41829a7c188730ace530"));

Console.WriteLine();

await foreach (var u in agent.RunStreamingAsync("西安的天气怎么样？", restored))
{
    Console.Write(u);
}

await foreach (var u in agent.RunStreamingAsync("我上个问题是什么？", restored))
{
    Console.Write(u);
}

Console.ReadLine();