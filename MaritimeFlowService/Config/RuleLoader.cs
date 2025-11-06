using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaritimeFlowService.Config
{
    internal class RuleLoader
    {
        //public static List<Rule> LoadRulesFromJson(string path)
        //{
        //    string json = File.ReadAllText(path);

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        // ✅ 必须启用类型识别（默认即可）
        //        IncludeFields = true
        //    };


        //    return JsonSerializer.Deserialize<List<Rule>>(json, options)
        //        ?? new List<Rule>();
        //}
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static List<Rule> LoadRulesFromJson(string path)
        {
            if (!File.Exists(path)) return new List<Rule>();

            string json = File.ReadAllText(path);

            // 先尝试反序列化为数组
            try
            {
                var list = JsonSerializer.Deserialize<List<Rule>>(json, options);
                if (list != null) return list;
            }
            catch
            {
                // 忽略，下面尝试单对象
            }

            // 再尝试单个对象
            try
            {
                var single = JsonSerializer.Deserialize<Rule>(json, options);
                if (single != null) return new List<Rule> { single };
            }
            catch
            {
                // 忽略，返回空列表
            }

            return new List<Rule>();
        }

        public static List<Rule> LoadRulesFromDirectory(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return new List<Rule>();

            var result = new List<Rule>();
            var files = Directory.GetFiles(dirPath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var rules = LoadRulesFromJson(file);
                    if (rules != null && rules.Count > 0)
                        result.AddRange(rules);
                }
                catch
                {
                    // 可选：记录日志或抛出异常。当前选择静默忽略单个文件错误。
                }
            }

            return result;
        }
    }
}
