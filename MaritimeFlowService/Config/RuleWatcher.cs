using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using MaritimeFlowService.Config;
using MaritimeFlowService.Engine;

internal class RuleWatcher : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly RuleEngine engine;
    private readonly string rulesDir;
    private readonly JsonSerializerOptions opts = new() { PropertyNameCaseInsensitive = true, IncludeFields = true };

    public RuleWatcher(string rulesDirectory, RuleEngine engine)
    {
        this.rulesDir = rulesDirectory ?? throw new ArgumentNullException(nameof(rulesDirectory));
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));

        watcher = new FileSystemWatcher(rulesDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Deleted += OnDeleted;
        watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // 防抖与等待写入完成
        try
        {
            Thread.Sleep(100);
            if (!File.Exists(e.FullPath)) return;

            var text = File.ReadAllText(e.FullPath);

            // 优先尝试单个 Rule 反序列化
            try
            {
                var rule = JsonSerializer.Deserialize<Rule>(text, opts);
                if (rule != null)
                {
                    engine.HotUpdateRule(rule);
                    return;
                }
            }
            catch { /* 忽略单对象解析错误，尝试数组 */ }

            try
            {
                var arr = JsonSerializer.Deserialize<List<Rule>>(text, opts);
                if (arr != null && arr.Count > 0)
                {
                    // 若文件包含多个规则，整批更新（增量机制由 HotUpdateRules 处理）
                    engine.HotUpdateRules(arr);
                    return;
                }
            }
            catch { /* 忽略 */ }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"规则文件处理失败 {e.Name}: {ex.Message}");
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => OnChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(e.FullPath) ?? string.Empty, Path.GetFileName(e.FullPath)));

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        // 删除单个文件后从目录重新加载全量规则（保证删除生效）
        try
        {
            var all = RuleLoader.LoadRulesFromDirectory(rulesDir);
            engine.HotUpdateRules(all);
            Console.WriteLine($"规则目录变更：文件 {e.Name} 删除，已从目录全量重载规则");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除事件处理失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        watcher.Dispose();
    }
}