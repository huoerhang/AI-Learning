
// ========================================
// 1. OpenAI Configuration
// ========================================

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

        var configuration = config.GetRequiredSection("Groq");

        var endpoint = configuration["Endpoint"];

        var modelName = configuration["Model"];

        var apiKey = configuration["ApiKey"];

        // ========================================
        // 2. Create OpenAI Chat Client
        // ========================================

        var openAiCLient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri(endpoint)
        });

        var chantClient = openAiCLient.GetChatClient(modelName).AsIChatClient();

        // ========================================
        // 3. Create Workflow
        // ========================================

        var judeExecutor = new JudgeExecutor(42);

        var numberPort = RequestPort.Create<GuessSignal, int>("GuestNumber");

        var workflow = new WorkflowBuilder(numberPort)
            .AddEdge(numberPort, judeExecutor)
            .AddEdge(judeExecutor, numberPort)
            .WithOutputFrom(judeExecutor)
            .Build();

        // ========================================
        // 4. Execute Workflow
        // ========================================

        Console.WriteLine("Guess the number (1-100)!");

        await using var guestRunner = await InProcessExecution.RunStreamingAsync(workflow, GuessSignal.Init);

        await foreach (var workflowEvent in guestRunner.WatchStreamAsync())
        {
            switch (workflowEvent)
            {
                case RequestInfoEvent requestEvent when requestEvent.Request.TryGetDataAs(out GuessSignal signal):
                    var hint = signal switch
                    {
                        GuessSignal.Init => "Make you first guess",
                        GuessSignal.TooHight => "Too high, please try again",
                        GuessSignal.TooLow => "Too low, please try again"
                    };

                    Console.WriteLine(signal);
                    var guess = int.Parse(Console.ReadLine());
                    await guestRunner.SendResponseAsync(requestEvent.Request.CreateResponse(guess));
                    break;

                case WorkflowOutputEvent outputEvent:
                    Console.WriteLine(outputEvent.Data);
                    break;
            }
        }
    }
}

// ========================================
// 2. Create Executors
// ========================================


public enum GuessSignal
{
    Init,
    TooHight,
    TooLow
}


[SendsMessage(typeof(GuessSignal))]
[YieldsOutput(typeof(string))]
internal sealed class JudgeExecutor : Executor<int>
{
    private readonly int _target;
    private int _tries;

    public JudgeExecutor(int target) : base(nameof(JudgeExecutor))
    {
        _target = target;
    }

    public override async ValueTask HandleAsync(int message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        _tries++;

        if (message == _target)
        {
            await context.YieldOutputAsync($"Correct! The number was {_target}. Found in {_tries} tries!");
            return;
        }

        if (message < _target)
        {
            await context.SendMessageAsync(GuessSignal.TooLow);
            return;
        }

        await context.SendMessageAsync(GuessSignal.TooHight);
    }
}