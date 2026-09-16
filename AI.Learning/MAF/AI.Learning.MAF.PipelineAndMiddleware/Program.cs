using AI.Learning.Common;
using Google.Protobuf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;


AIAgent agent = new OpenAIClient( new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions 
{
    Endpoint = new Uri(OpenAIConsts.Url),

   // Transport = new HttpClientPipelineTransport(new HttpClient(new LoggingHandler()))
})
    .GetChatClient(OpenAIConsts.ModelName)
    .AsAIAgent(
        instructions: "你是一个乐于助人的助手，必要时调用工具。",
        name: "Helper",
        tools: [AIFunctionFactory.Create(AIFunctions.GetWeather)],
        clientFactory:chatClient=> chatClient.AsBuilder().Use(getResponseFunc:MyChatClientMiddleware,getStreamingResponseFunc:null).Build()
        ).AsBuilder()
    .Use(runFunc: MyAgentMiddlewareWithResponse, runStreamingFunc: MyAgentMiddlewareWithStreaem)	// Agent 中间件
    .Use(callback: MyFuncationCallingMiddleware)											// 函数调用中间件
    .Build();


// 第 2 轮：触发工具调用（模型会自动调 GetWeather），模型仍记得第 1 轮
Console.WriteLine();


await foreach (var update in agent.RunStreamingAsync("西安的天气怎么样？"))
{
    Console.Write(update);
}

Console.WriteLine();

Console.Read();


async Task<AgentResponse> MyAgentMiddlewareWithResponse(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,AgentSession? session, AgentRunOptions options,AIAgent agent, CancellationToken cancellationToken)
{
    Console.WriteLine("[Agent中间件]调用前");

    var response= await agent.RunAsync(messages,session,options, cancellationToken);

    Console.WriteLine("[Agent中间件]调用后");

    return response;
}

async IAsyncEnumerable<AgentResponseUpdate> MyAgentMiddlewareWithStreaem(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AgentSession? session, AgentRunOptions options, AIAgent agent, CancellationToken cancellationToken)
{
    Console.WriteLine("[Agent中间件]调用前");

    await foreach(var update in agent.RunStreamingAsync(messages,session,options, cancellationToken))
    {
        yield return update;
    }

    Console.WriteLine("[Agent中间件]调用后");
}


async ValueTask<object?> MyFuncationCallingMiddleware(AIAgent agent, FunctionInvocationContext context, Func<FunctionInvocationContext,CancellationToken,ValueTask<object?>> next,CancellationToken cancellationToken)
{
    Console.WriteLine($"[Function Name {context.Function.Name}]调用前");

    var result = await next(context, cancellationToken);

    Console.WriteLine($"[Function Name {context.Function.Name}]调用后，结果:\t{result}");

    return result;
}

async Task<ChatResponse> MyChatClientMiddleware(IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,ChatOptions options, IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("IChatClient 中间件开始");
    var response = await chatClient.GetResponseAsync(chatMessages, options, cancellationToken);

    Console.WriteLine("IChatClient 中间件结束");

    return response;
}