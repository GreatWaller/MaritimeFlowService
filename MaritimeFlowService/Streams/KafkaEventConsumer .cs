using Confluent.Kafka;
using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Streams
{
    internal class KafkaEventConsumer : IEventConsumer
    {
        private readonly string _broker;
        private readonly string _topic;
        private readonly string _groupId;

        public KafkaEventConsumer(string broker, string topic, string groupId)
        {
            _broker = broker;
            _topic = topic;
            _groupId = groupId;
        }

        public async Task StartAsync(Func<MaritimeEvent, Task> onEvent, CancellationToken token)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _broker,
                GroupId = _groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(_topic);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var cr = consumer.Consume(token);
                    if (cr?.Message?.Value != null)
                    {
                        // 假设 Kafka 消息是 JSON 格式
                        var ev = System.Text.Json.JsonSerializer.Deserialize<MaritimeEvent>(cr.Message.Value);
                        if (ev != null) await onEvent(ev);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                consumer.Close();
            }
        }
    }
}
