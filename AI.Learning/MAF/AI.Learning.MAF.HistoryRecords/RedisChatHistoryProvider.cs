using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static Microsoft.Agents.AI.ChatHistoryMemoryProvider;

namespace AI.Learning.MAF.HistoryRecords
{
    internal class RedisChatHistoryProvider : ChatHistoryProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _keyPrefix;
        private readonly int _maxMessages;
        private readonly ProviderSessionState<State> _sessionState;

        public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];

        public RedisChatHistoryProvider(IConnectionMultiplexer redis, string keyPrefix="chat_history", int maxMessages = 1000)
        {
            _redis = redis;
            _keyPrefix = keyPrefix;
            _maxMessages = maxMessages;

            _sessionState = new ProviderSessionState<State>(_ => new State($"cov-{Guid.NewGuid():N}"), GetType().Name);
        }

        protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            State state = _sessionState.GetOrInitializeState(context.Session);
            var db = _redis.GetDatabase();
            var key = $"{_keyPrefix}:{state.ConversationId}";

            RedisValue[] values = await db.ListRangeAsync(key);

            var messages = new List<ChatMessage>();

            foreach(var value in values)
            {
                if (value.IsNullOrEmpty) continue;
                var msg = JsonSerializer.Deserialize<ChatMessage>(value.ToString());

                if(msg is not null)
                {
                    messages.Add(msg);
                }
            }

            return messages;
        }

        protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
        {
            State state = _sessionState.GetOrInitializeState(context.Session);
            var allNew = (context.RequestMessages ?? []).Concat(context.ResponseMessages ?? []).ToList();

            if (allNew.Count == 0) return;

            IDatabase db = _redis.GetDatabase();
            var key = $"{_keyPrefix}:{state.ConversationId}";

            // 把每条消息序列化成 JSON，批量 RPUSH 进 List
            RedisValue[] serialized = allNew.Select(m => (RedisValue)JsonSerializer.Serialize(m)).ToArray();
            await db.ListRightPushAsync(key, serialized);

            // 超过上限时，保留尾部最新的 N 条（裁掉老的）
            await db.ListTrimAsync(key, -_maxMessages, -1);

        }

        public sealed class State(string conversationId)
        {
            public string ConversationId { get; } = conversationId;
        }
    }
}
