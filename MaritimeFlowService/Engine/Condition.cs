using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal abstract class Condition
    {
        public abstract bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine);
    }
}
