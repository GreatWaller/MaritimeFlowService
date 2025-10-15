using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Alerts
{
    internal class Alert
    {
        public string RuleId { get; set; }
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public string EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Notify { get; set; }
    }
}
