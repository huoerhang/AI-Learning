using AI.Learning.Common;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;

AIAgent agent = new OpenAIClient(credential: new ApiKeyCredential(OpenAIConsts.ApiKey), new OpenAIClientOptions()
{
    Endpoint=new Uri(OpenAIConsts.Url),
    Transport = new HttpClientPipelineTransport(new HttpClient(new LoggingHandler()))
}).GetChatClient(OpenAIConsts.ModelName).AsAIAgent(instructions: "你是一个讲笑话高手。", name:"Joker");


Console.WriteLine(await agent.RunAsync("讲一个关于海盗的笑话。"));

Console.ReadLine();
