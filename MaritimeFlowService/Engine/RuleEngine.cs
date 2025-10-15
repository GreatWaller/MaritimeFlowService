using MaritimeFlowService.Alerts;
using System.Collections.Concurrent;
using System.Data;

namespace MaritimeFlowService.Engine
{
    internal class RuleEngine : IDisposable
    {
        private readonly ReaderWriterLockSlim ruleLock = new();
        private readonly object statesLock = new();
        private List<Rule> rules = [];
        private readonly ConcurrentDictionary<string, RuleState> states = new();

        // 规则处理线程相关
        private readonly ConcurrentDictionary<string, BlockingCollection<MaritimeEvent>> ruleQueues = new();
        private readonly ConcurrentDictionary<string, Thread> ruleThreads = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> ruleCts = new();
        private ConcurrentBag<Alert> alertsCollection = [];
        private bool isInitialized;

        public void HotUpdateRules(List<Rule> newRules)
        {
            ruleLock.EnterWriteLock();
            try
            {
                // 停止所有现有规则线程
                if (isInitialized)
                {
                    StopRuleThreads();
                }

                rules = [.. newRules.OrderByDescending(static r => r.Priority)];

                // 为每个规则创建专用线程
                InitializeRuleThreads();
            }
            finally { ruleLock.ExitWriteLock(); }
        }

        private void InitializeRuleThreads()
        {
            foreach (Rule? rule in rules.Where(r => r.Enabled))
            {
                // 确保规则有状态对象
                _ = states.GetOrAdd(rule.Id, id => new RuleState(id));

                // 为规则创建事件队列
                BlockingCollection<MaritimeEvent> queue = new BlockingCollection<MaritimeEvent>();
                ruleQueues[rule.Id] = queue;

                // 创建取消令牌
                CancellationTokenSource cts = new CancellationTokenSource();
                ruleCts[rule.Id] = cts;

                // 创建并启动规则处理线程
                Thread thread = new Thread(() => ProcessRuleEvents(rule, queue, cts.Token))
                {
                    Name = $"Rule-{rule.Id}-Thread",
                    IsBackground = true
                };

                ruleThreads[rule.Id] = thread;
                thread.Start();

                Console.WriteLine($"已为规则 {rule.Id} 创建专用线程 {thread.ManagedThreadId}");
            }

            isInitialized = true;
        }

        private void StopRuleThreads()
        {
            // 取消所有规则线程
            foreach (CancellationTokenSource cts in ruleCts.Values)
            {
                cts.Cancel();
            }

            // 等待所有线程结束
            foreach (Thread thread in ruleThreads.Values)
            {
                if (thread.IsAlive)
                {
                    _ = thread.Join(1000); // 等待最多1秒
                }
            }

            // 清空集合
            ruleQueues.Clear();
            ruleThreads.Clear();
            ruleCts.Clear();

            isInitialized = false;
        }

        private void ProcessRuleEvents(Rule rule, BlockingCollection<MaritimeEvent> queue, CancellationToken token)
        {
            try
            {
                foreach (MaritimeEvent ev in queue.GetConsumingEnumerable(token))
                {
                    RuleState state = states.GetOrAdd(rule.Id, id => new RuleState(id));

                    // 处理多个条件
                    bool matched = false;
                    if (rule.Conditions != null && rule.Conditions.Count > 0)
                    {
                        if (rule.CombineLogic == "AND")
                        {
                            matched = rule.Conditions.All(c => c.Evaluate(ev, state, this));
                        }
                        else
                        {
                            matched = rule.CombineLogic == "OR"
                                ? rule.Conditions.Any(c => c.Evaluate(ev, state, this))
                                : throw new InvalidOperationException($"Unsupported combine logic: {rule.CombineLogic}");
                        }
                    }

                    if (matched)
                    {
                        Console.WriteLine($"线程 {Environment.CurrentManagedThreadId} 处理规则 {rule.Id} 匹配成功");
                        lock (alertsCollection)
                        {
                            alertsCollection.Add(new Alert
                            {
                                RuleId = rule.Id,
                                AlertType = rule.Action.AlertType,
                                Severity = rule.Action.Severity,
                                EntityId = ev.Id,
                                Timestamp = DateTime.UtcNow,
                                Notify = rule.Action.Notify
                            });
                        }
                    }
                    else
                    {
                        Console.WriteLine($"线程 {Environment.CurrentManagedThreadId} 处理规则 {rule.Id} 匹配失败");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 线程被取消，正常退出
                Console.WriteLine($"规则 {rule.Id} 的处理线程已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"规则 {rule.Id} 处理线程发生错误: {ex.Message}");
            }
        }

        public List<Alert> Evaluate(MaritimeEvent ev)
        {
            if (!isInitialized)
            {
                InitializeRuleThreads();
            }

            // 清空之前的告警结果
            lock (alertsCollection)
            {
                alertsCollection = [];
            }

            // 将事件分发到每个规则的队列
            ruleLock.EnterReadLock();
            try
            {
                foreach (string ruleId in ruleQueues.Keys)
                {
                    if (ruleQueues.TryGetValue(ruleId, out BlockingCollection<MaritimeEvent>? queue))
                    {
                        queue.Add(ev);
                    }
                }
            }
            finally
            {
                ruleLock.ExitReadLock();
            }

            // 等待所有规则处理完成（这里可以设置一个超时时间）
            Thread.Sleep(100); // 简单等待100毫秒，实际应用中可能需要更复杂的同步机制

            // 处理排他性规则
            List<Alert> alerts;
            lock (alertsCollection)
            {
                alerts = [.. alertsCollection];
            }

            if (alerts.Any())
            {
                // 如果有排他性规则触发，只保留优先级最高的那个
                List<Rule> exclusiveRules = rules.Where(r => r.Enabled && r.Exclusive).ToList();
                if (exclusiveRules.Any())
                {
                    List<Alert> exclusiveAlerts = alerts.Where(a => exclusiveRules.Any(r => r.Id == a.RuleId)).ToList();
                    if (exclusiveAlerts.Any())
                    {
                        // 找到触发的排他性规则中优先级最高的
                        Rule? highestPriorityRule = exclusiveRules
                            .Where(r => exclusiveAlerts.Any(a => a.RuleId == r.Id))
                            .OrderByDescending(r => r.Priority)
                            .FirstOrDefault();

                        if (highestPriorityRule != null)
                        {
                            // 只保留这个规则的告警，移除其他所有告警
                            alerts = [.. alerts.Where(a => a.RuleId == highestPriorityRule.Id)];
                        }
                    }
                }
            }

            return alerts;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
