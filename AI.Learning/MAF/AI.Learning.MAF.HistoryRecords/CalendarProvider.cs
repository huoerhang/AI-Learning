using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AI.Learning.MAF.HistoryRecords
{
    internal class CalendarProvider(Func<Task<string[]>> loadEvents):MessageAIContextProvider
    {
        protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            var events = await loadEvents();

            return [new ChatMessage(ChatRole.User, $"你接下来有这些安排：\n{string.Join("\n", events)}")];
        }
    }
}
