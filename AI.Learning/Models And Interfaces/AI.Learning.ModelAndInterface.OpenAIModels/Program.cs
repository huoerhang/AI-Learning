using AI.Learning.Common;
using OpenAI.Models;

namespace AI.Learning.ModelAndInterface.OpenAIModels
{
    internal class Program
    {
        static void Main(string[] args)
        {
            OpenAIModelClient client = new OpenAIModelClient(credential: new System.ClientModel.ApiKeyCredential(""), options: new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(OpenAIConsts.Url)
            });
            OpenAIModelCollection openAIModels = client.GetModels();

            foreach (var model in openAIModels)
            {
                Console.WriteLine($"模型: {model.Id}    归属: {model.OwnedBy}    创建: {model.CreatedAt:yyyy-MM-dd}");
            }

            Console.ReadLine();
        }
    }
}
