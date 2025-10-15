using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class Rule
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public bool Enabled { get; set; }
        public bool Exclusive { get; set; }
        //public Condition Condition { get; set; }
        // 多个条件
        public List<Condition> Conditions { get; set; } = new();

        // 条件组合逻辑: AND / OR
        public string CombineLogic { get; set; } = "AND";
        public RuleAction Action { get; set; }
    }
}
