using AI.Learning.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text;

//AIAgent agent = new OpenAIClient(new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions()
//{
//    Endpoint = new Uri(OpenAIConsts.Url)
//}).GetChatClient(OpenAIConsts.ModelName).AsAIAgent(instructions: "你是一个讲笑话高手。", name: "Joker");

// 非流式
//AgentResponse response = await agent.RunAsync("讲一个关于海盗的笑话。");

//Console.WriteLine(response);              // 直接打印回复文本，它的 ToString() 就是 response.Text
//Console.WriteLine(response.Usage.TotalTokenCount);         // Token 用量等元信息
//Console.WriteLine(response.FinishReason);   // 结束原因



// 流式
//StringBuilder sb = new StringBuilder();
//Microsoft.Extensions.AI.ChatFinishReason? finishReason = null;

//await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("讲一个关于海盗的笑话。"))
//{
//    Console.Write(update.Text);

//    sb.Append(update.Text);

//    finishReason ??= update.FinishReason;

//   foreach(var call in update.Contents.OfType<FunctionCallContent>())
//    {
//        Console.WriteLine($"\n[调用工具]{call.Name}");
//    }
//}

//Console.WriteLine($"\n---结束原因: {finishReason}");

// 函数调用

//[Description("获取指定地点的当天天气。")]
//static string GetWeather([Description("地点名称")] string location)
//{
//    return $"{location} 今天多云，最高 15°C";
//}

//AIAgent agent = new OpenAIClient(new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions()
//{
//    Endpoint = new Uri(OpenAIConsts.Url)
//}).GetChatClient(OpenAIConsts.ModelName).AsAIAgent(instructions: "你是一个乐于助人的助手。", name: "Helper", tools: [AIFunctionFactory.Create(GetWeather)]);

//await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("阿姆斯特丹天气怎么样？"))
//{
//    Console.Write(update);
//}

// 自动多轮对话

//AIAgent agent = new OpenAIClient(new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions()
//{
//    Endpoint = new Uri(OpenAIConsts.Url)
//}).GetChatClient(OpenAIConsts.ModelName).AsAIAgent(instructions: "你是一个乐于助人的助手。", name: "Helper", tools: [AIFunctionFactory.Create(AIFunctions.GetPostalCode),AIFunctionFactory.Create(AIFunctions.GetWeaterByPostalCode)]);

//await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("阿姆斯特丹的城市代码和天气怎么样？？"))
//{
//    Console.Write(update);
//}


// 实践总结

AIAgent agent = new OpenAIClient(new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions()
{
    Endpoint = new Uri(OpenAIConsts.Url)
}).GetChatClient(OpenAIConsts.ModelName).AsAIAgent(instructions: "你是一个乐于助人的助手。", name: "Helper", tools: [AIFunctionFactory.Create(AIFunctions.GetWeather)]);

var session = await agent.CreateSessionAsync();

await foreach(AgentResponseUpdate update in agent.RunStreamingAsync("你好！",session))
{
    Console.Write(update);
}

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("阿姆斯特丹的天气怎么样？？",session))
{
    Console.Write(update);
}

Console.WriteLine();

Microsoft.Extensions.AI.ChatMessage imgMessage = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User,
    [
    new TextContent("请帮我识别这张图中的是什么动物? 是不是铁头将军？"),
    await DataContent.LoadFromAsync("C:\\Users\\Administrator\\Desktop\\铁头将军.jpg")
    ]);

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(imgMessage, session))
{
    Console.Write(update);
}


Console.ReadLine();
