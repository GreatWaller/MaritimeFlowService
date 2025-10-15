using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class RuleAction
    {
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public List<string> Notify { get; set; }
    }
}
