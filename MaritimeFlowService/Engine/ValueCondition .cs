using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class ValueCondition: Condition
    {
        public string Field { get; set; } = "";   // 要检查的字段，比如 "Speed", "AISStatus"
        public string Operator { get; set; } = ""; // 支持: >, <, ==, !=, >=, <=, contains
        public string Value { get; set; } = "";    // 阈值或比较目标

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            if (!ev.Attributes.TryGetValue(Field, out var fieldValue))
                return false;

            // 1. 数值比较
            if (double.TryParse(fieldValue.ToString(), out double fieldNum) &&
                double.TryParse(Value, out double compareNum))
            {
                return Operator switch
                {
                    ">" => fieldNum > compareNum,
                    "<" => fieldNum < compareNum,
                    ">=" => fieldNum >= compareNum,
                    "<=" => fieldNum <= compareNum,
                    "==" => Math.Abs(fieldNum - compareNum) < 1e-6,
                    "!=" => Math.Abs(fieldNum - compareNum) > 1e-6,
                    _ => false
                };
            }

            // 2. 字符串比较
            string fieldStr = fieldValue.ToString() ?? "";
            return Operator switch
            {
                "==" => fieldStr.Equals(Value, StringComparison.OrdinalIgnoreCase),
                "!=" => !fieldStr.Equals(Value, StringComparison.OrdinalIgnoreCase),
                "contains" => fieldStr.Contains(Value, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}
