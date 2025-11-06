using Confluent.Kafka;
using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MaritimeFlowService.Streams
{
    internal class KafkaEventConsumer : IEventConsumer
    {
        private readonly ConsumerConfig _config;
        private readonly string _topic;
        private readonly string? _targetKey;

        // 保留旧的兼容构造：broker, topic, groupId
        public KafkaEventConsumer(string broker, string topic, string groupId)
            : this(new ConsumerConfig
            {
                BootstrapServers = broker,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            }, topic, null)
        {
        }

        // 新的构造：直接传入 ConsumerConfig，并可指定 targetKey（可为 null）
        public KafkaEventConsumer(ConsumerConfig config, string topic, string? targetKey = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _targetKey = string.IsNullOrWhiteSpace(targetKey) ? null : targetKey;
        }

        // 辅助构造：从常规参数构建 ConsumerConfig（便于从 JSON 配置映射）
        public KafkaEventConsumer(
            string bootstrapServers,
            string topic,
            string groupId,
            string autoOffsetReset = "Earliest",
            bool enableAutoCommit = true,
            int? sessionTimeoutMs = null,
            int? maxPollIntervalMs = null,
            string? targetKey = null)
            : this(BuildConsumerConfig(bootstrapServers, groupId, autoOffsetReset, enableAutoCommit, sessionTimeoutMs, maxPollIntervalMs), topic, targetKey)
        {
        }

        private static ConsumerConfig BuildConsumerConfig(string bootstrapServers, string groupId, string autoOffsetReset, bool enableAutoCommit, int? sessionTimeoutMs, int? maxPollIntervalMs)
        {
            var cfg = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = enableAutoCommit
            };

            // 解析 AutoOffsetReset 字符串（大小写不敏感）
            if (!string.IsNullOrWhiteSpace(autoOffsetReset) &&
                Enum.TryParse<AutoOffsetReset>(autoOffsetReset, true, out var aor))
            {
                cfg.AutoOffsetReset = aor;
            }
            else
            {
                cfg.AutoOffsetReset = AutoOffsetReset.Earliest;
            }

            if (sessionTimeoutMs.HasValue) cfg.SessionTimeoutMs = sessionTimeoutMs.Value;
            if (maxPollIntervalMs.HasValue) cfg.MaxPollIntervalMs = maxPollIntervalMs.Value;

            return cfg;
        }

        public async Task StartAsync(Func<AISData, Task> onEvent, CancellationToken token)
        {
            // 使用 string key, string value，这样可以基于 key 做筛选（TargetKey）
            using var consumer = new ConsumerBuilder<string, string>(_config).Build();
            consumer.Subscribe(_topic);
            Console.WriteLine($"Kafka consumer subscribed to topic {_topic}. TargetKey: {_targetKey ?? "(none)"}");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var cr = consumer.Consume(token);
                        if (cr?.Message == null) continue;

                        // 若配置了 TargetKey，则只处理 key 匹配的消息（exact match）
                        if (_targetKey != null)
                        {
                            var key = cr.Message.Key;
                            if (!string.Equals(key, _targetKey, StringComparison.Ordinal))
                            {
                                // 跳过不匹配的消息
                                continue;
                            }
                        }

                        var payload = cr.Message.Value;
                        if (!string.IsNullOrWhiteSpace(payload))
                        {
                            try
                            {
                                //var ev = System.Text.Json.JsonSerializer.Deserialize<AISData>(payload);
                                var ev = JsonConvert.DeserializeObject<AISData>(payload);
                                if (ev != null)
                                {
                                    await onEvent(ev).ConfigureAwait(false);
                                }
                                else
                                {
                                    Console.WriteLine("KafkaEventConsumer: 反序列化结果为 null");
                                }
                            }
                            catch (System.Text.Json.JsonException jex)
                            {
                                Console.WriteLine($"KafkaEventConsumer: JSON 解析错误: {jex.Message}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"KafkaEventConsumer: 处理消息时出错: {ex.Message}");
                            }
                        }
                    }
                    catch (ConsumeException cex)
                    {
                        Console.WriteLine($"KafkaEventConsumer: Consume 错误: {cex.Error.Reason}");
                        // 根据需要可以决定是否 break 或 continue
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            finally
            {
                try
                {
                    consumer.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"KafkaEventConsumer: 关闭消费者时出错: {ex.Message}");
                }
            }
        }
    }
}
