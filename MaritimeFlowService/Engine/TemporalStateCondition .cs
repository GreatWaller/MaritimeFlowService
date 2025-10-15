using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class TemporalStateCondition: Condition
    {
        public string Op { get; set; } // continuous_for / no_event_for
        public int DurationSeconds { get; set; }

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            var now = DateTime.UtcNow;

            if (Op == "continuous_for")
            {
                if (!state.TemporalStart.ContainsKey(ev.MMSI))
                    state.TemporalStart[ev.MMSI] = now;

                // 持续超过 DurationSeconds 就触发
                return (now - state.TemporalStart[ev.MMSI]).TotalSeconds >= DurationSeconds;
            }

            if (Op == "no_event_for")
            {
                if (state.LastSeen.ContainsKey(ev.MMSI))
                    return (now - state.LastSeen[ev.MMSI]).TotalSeconds >= DurationSeconds;

                state.LastSeen[ev.MMSI] = now;
            }

            return false;
        }
    }
}
