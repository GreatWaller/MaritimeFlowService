using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class RuleState
    {
        public string RuleId { get; set; }
        public Dictionary<string, DateTime> TemporalStart { get; set; } = new();
        public Dictionary<string, DateTime> LastSeen { get; set; } = new();
        public List<MaritimeEvent> RecentEvents { get; set; } = new();
        public RuleState(string id) { RuleId = id; }
    }
}
