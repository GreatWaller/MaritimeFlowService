using MaritimeFlowService.Alerts;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;

namespace MaritimeFlowService.Engine
{
    internal class RuleEngine : IDisposable
    {
        private readonly ReaderWriterLockSlim ruleLock = new();
        private readonly ConcurrentDictionary<string, RuleState> states = new();
        private List<Rule> rules = new();

        // 规则处理线程相关
        private readonly ConcurrentDictionary<string, BlockingCollection<MaritimeEvent>> ruleQueues = new();
        private readonly ConcurrentDictionary<string, Thread> ruleThreads = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> ruleCts = new();
        private ConcurrentBag<Alert> alertsCollection = new();
        private bool isInitialized;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        // 热更新：增量应用 newRules（新增 / 删除 / 修改）
        public void HotUpdateRules(List<Rule> newRules)
        {
            if (newRules == null) return;

            ruleLock.EnterWriteLock();
            try
            {
                var newDict = newRules.ToDictionary(r => r.Id);
                var oldDict = rules.ToDictionary(r => r.Id);

                // 删除：存在于 old 但不在 new 中
                var removed = oldDict.Keys.Except(newDict.Keys).ToList();
                foreach (var id in removed)
                {
                    StopRuleInternal(id, removeState: true);
                }

                // 新增：存在于 new 但不在 old 中
                var added = newDict.Keys.Except(oldDict.Keys).ToList();
                foreach (var id in added)
                {
                    // 保持 state（如果不存在则创建），然后启动线程（如果启用）
                    var rule = newDict[id];
                    states.GetOrAdd(rule.Id, id => new RuleState(id));
                    if (rule.Enabled)
                        StartRuleInternal(rule);
                }

                // 更新：存在于两者，但内容不同
                var maybeUpdated = newDict.Keys.Intersect(oldDict.Keys);
                foreach (var id in maybeUpdated)
                {
                    var oldRule = oldDict[id];
                    var newRule = newDict[id];
                    if (!RulesDeepEqual(oldRule, newRule))
                    {
                        // 停掉老线程但保留状态，启动新线程（如果启用）
                        StopRuleInternal(id, removeState: false);
                        states.GetOrAdd(newRule.Id, i => new RuleState(i));
                        if (newRule.Enabled)
                            StartRuleInternal(newRule);
                    }
                }

                // 更新内存中 rules 列表（按优先级排序）
                rules = newRules.OrderByDescending(r => r.Priority).ToList();
                isInitialized = true;
            }
            finally
            {
                ruleLock.ExitWriteLock();
            }
        }

        // 启动单个规则的线程与队列（内部使用）
        private void StartRuleInternal(Rule rule)
        {
            if (rule == null) return;

            // 如果已有运行实例，先停止
            if (ruleThreads.ContainsKey(rule.Id))
            {
                StopRuleInternal(rule.Id, removeState: false);
            }

            var queue = new BlockingCollection<MaritimeEvent>();
            ruleQueues[rule.Id] = queue;

            var cts = new CancellationTokenSource();
            ruleCts[rule.Id] = cts;

            Thread thread = new Thread(() => ProcessRuleEvents(rule, queue, cts.Token))
            {
                Name = $"Rule-{rule.Id}-Thread",
                IsBackground = true
            };

            ruleThreads[rule.Id] = thread;
            thread.Start();

            Console.WriteLine($"已为规则 {rule.Id} 创建专用线程 {thread.ManagedThreadId}");
        }

        // 停止单个规则线程并可选删除状态
        private void StopRuleInternal(string ruleId, bool removeState)
        {
            if (string.IsNullOrEmpty(ruleId)) return;

            if (ruleCts.TryRemove(ruleId, out var cts))
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }

            if (ruleThreads.TryRemove(ruleId, out var thread))
            {
                try
                {
                    if (thread.IsAlive)
                    {
                        _ = thread.Join(1000);
                    }
                }
                catch { }
            }

            if (ruleQueues.TryRemove(ruleId, out var queue))
            {
                try
                {
                    queue.CompleteAdding();
                    queue.Dispose();
                }
                catch { }
            }

            if (removeState)
            {
                states.TryRemove(ruleId, out _);
            }

            Console.WriteLine($"规则 {ruleId} 的线程已停止并清理（removeState={removeState}）");
        }

        private void InitializeRuleThreads()
        {
            // 按现有 rules 启动所有启用的规则线程（仅在第一次使用时）
            foreach (var rule in rules.Where(r => r.Enabled))
            {
                states.GetOrAdd(rule.Id, id => new RuleState(id));
                StartRuleInternal(rule);
            }

            isInitialized = true;
        }

        private void StopRuleThreads()
        {
            // 取消所有规则线程并清理
            foreach (var kv in ruleCts)
            {
                try { kv.Value.Cancel(); } catch { }
            }

            foreach (var kv in ruleThreads)
            {
                var t = kv.Value;
                try
                {
                    if (t.IsAlive)
                        _ = t.Join(1000);
                }
                catch { }
            }

            // 清空集合（不删除 states）
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
                    var state = states.GetOrAdd(rule.Id, id => new RuleState(id));

                    // 处理多个条件
                    bool matched = false;
                    if (rule.Conditions != null && rule.Conditions.Count > 0)
                    {
                        if (string.Equals(rule.CombineLogic, "AND", StringComparison.OrdinalIgnoreCase))
                        {
                            matched = rule.Conditions.All(c => c.Evaluate(ev, state, this));
                        }
                        else if (string.Equals(rule.CombineLogic, "OR", StringComparison.OrdinalIgnoreCase))
                        {
                            matched = rule.Conditions.Any(c => c.Evaluate(ev, state, this));
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unsupported combine logic: {rule.CombineLogic}");
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
                                AlertType = rule.Action?.AlertType ?? string.Empty,
                                Severity = rule.Action?.Severity ?? string.Empty,
                                EntityId = ev.MMSI,
                                Timestamp = DateTime.UtcNow,
                                Notify = rule.Action?.Notify ?? new List<string>()
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
                Console.WriteLine($"规则 {rule.Id} 的处理线程已停止（取消）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"规则 {rule.Id} 处理线程发生错误: {ex.Message}");
            }
        }

        // 将事件分发到每个规则的队列并返回聚合告警
        public List<Alert> Evaluate(MaritimeEvent ev)
        {
            if (!isInitialized)
            {
                ruleLock.EnterWriteLock();
                try
                {
                    if (!isInitialized)
                        InitializeRuleThreads();
                }
                finally { ruleLock.ExitWriteLock(); }
            }

            // 清空之前的告警结果
            lock (alertsCollection)
            {
                alertsCollection = new ConcurrentBag<Alert>();
            }

            // 将事件分发到每个规则的队列（读锁保护）
            ruleLock.EnterReadLock();
            try
            {
                foreach (var kv in ruleQueues)
                {
                    var queue = kv.Value;
                    if (!queue.IsAddingCompleted)
                    {
                        try { queue.Add(ev); } catch { }
                    }
                }
            }
            finally
            {
                ruleLock.ExitReadLock();
            }

            // 简单等待，实际可替换为更精确的同步（例如计数器或任务）
            Thread.Sleep(100);

            List<Alert> alerts;
            lock (alertsCollection)
            {
                alerts = alertsCollection.ToList();
            }

            // 处理排他性规则：只保留优先级最高的排他性规则告警
            if (alerts.Any())
            {
                var exclusiveRules = rules.Where(r => r.Enabled && r.Exclusive).ToList();
                if (exclusiveRules.Any())
                {
                    var exclusiveAlerts = alerts.Where(a => exclusiveRules.Any(r => r.Id == a.RuleId)).ToList();
                    if (exclusiveAlerts.Any())
                    {
                        var highestPriorityRule = exclusiveRules
                            .Where(r => exclusiveAlerts.Any(a => a.RuleId == r.Id))
                            .OrderByDescending(r => r.Priority)
                            .FirstOrDefault();

                        if (highestPriorityRule != null)
                        {
                            alerts = alerts.Where(a => a.RuleId == highestPriorityRule.Id).ToList();
                        }
                    }
                }
            }

            return alerts;
        }

        // 简单的深度比较（序列化比较）
        private static bool RulesDeepEqual(Rule a, Rule b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            var sa = JsonSerializer.Serialize(a, _jsonOptions);
            var sb = JsonSerializer.Serialize(b, _jsonOptions);
            return sa == sb;
        }

        public void Dispose()
        {
            ruleLock.EnterWriteLock();
            try
            {
                StopRuleThreads();
                // 清理 states
                states.Clear();
            }
            finally
            {
                ruleLock.ExitWriteLock();
            }
        }

        public void HotUpdateRule(Rule newRule)
        {
            if (newRule == null) return;

            ruleLock.EnterWriteLock();
            try
            {
                var existing = rules.FirstOrDefault(r => r.Id == newRule.Id);
                if (existing == null)
                {
                    // 新增规则
                    rules.Add(newRule);
                    rules = rules.OrderByDescending(r => r.Priority).ToList();
                    states.GetOrAdd(newRule.Id, id => new RuleState(id));
                    if (newRule.Enabled) StartRuleInternal(newRule);
                    Console.WriteLine($"规则 {newRule.Id} 已新增并启动（如果启用）");
                    return;
                }

                // 若无变化只返回
                if (RulesDeepEqual(existing, newRule))
                {
                    Console.WriteLine($"规则 {newRule.Id} 无变化，忽略热更");
                    return;
                }

                // 替换内存中的规则定义并按需重启线程（保留状态）
                var idx = rules.FindIndex(r => r.Id == newRule.Id);
                if (idx >= 0) rules[idx] = newRule;
                rules = rules.OrderByDescending(r => r.Priority).ToList();

                // 先停止现有线程（保留状态），再根据 newRule.Enabled 启动
                StopRuleInternal(newRule.Id, removeState: false);
                states.GetOrAdd(newRule.Id, id => new RuleState(id));
                if (newRule.Enabled) StartRuleInternal(newRule);

                Console.WriteLine($"规则 {newRule.Id} 已更新并重启（如果启用）");
            }
            finally
            {
                ruleLock.ExitWriteLock();
            }
        }
    }
}
