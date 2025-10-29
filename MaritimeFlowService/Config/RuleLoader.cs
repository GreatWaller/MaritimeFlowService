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
        public static List<Rule> LoadRulesFromJson(string path)
        {
            string json = File.ReadAllText(path);

            //var options = new JsonSerializerOptions
            //{
            //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //    ReadCommentHandling = JsonCommentHandling.Skip,
            //    AllowTrailingCommas = true,
            //    WriteIndented = true
            //};

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // ✅ 必须启用类型识别（默认即可）
                IncludeFields = true
            };


            return JsonSerializer.Deserialize<List<Rule>>(json, options)
                ?? new List<Rule>();
        }
    }
}
