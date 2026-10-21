using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Learning.MAF.HistoryRecords
{
    internal class TodoListProvider:AIContextProvider
    {
        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            var items = GetItems(context.Session);
            var sb = new StringBuilder("你当前的待办：\n");

            for (int i = 0; i < items.Count; i++)
            {
                sb.AppendLine($"{i}. {items[i]}");
            }

            return new(new AIContext
            {
                Tools = [
                    AIFunctionFactory.Create((string item)=>AddItem(context.Session,item),"AddTodo","添加一个待办"),
                    AIFunctionFactory.Create((string item)=>RemoveItem(context.Session,item),"RemoveTodo","删一条待办"),
                    ],
                Messages = [new ChatMessage(ChatRole.User, sb.ToString())]
            });
        }

        protected override ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            return base.StoreAIContextAsync(context, cancellationToken);
        }
        private static List<string> GetItems(AgentSession? agentSession)
        {
            return agentSession?.StateBag.GetValue<List<string>>(nameof(TodoListProvider)) ?? [];
        }

        private static void SetItem(AgentSession? agentSession,List<string> items)
        {
            agentSession?.StateBag.SetValue(nameof(TodoListProvider), items);
        }

        private static void AddItem(AgentSession? agentSession, string item)
        {
            var items = GetItems(agentSession);

            items.Add(item);
        }

        private static void RemoveItem(AgentSession? agentSession, string item)
        {
            var items = GetItems(agentSession);

            var index= items.IndexOf(item);

            items.RemoveAt(index);
        }
    }
}
