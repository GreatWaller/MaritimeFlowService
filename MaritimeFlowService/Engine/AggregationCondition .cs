using MaritimeFlowService.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class AggregationCondition : Condition
    {
        public string Op { get; set; } // count_in_window / pair_exists
        public int Threshold { get; set; }
        public int WindowSeconds { get; set; }

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            var now = DateTime.UtcNow;
            state.RecentEvents = state.RecentEvents.Where(e => (now - e.EventTime).TotalSeconds <= WindowSeconds).ToList();
            state.RecentEvents.Add(ev);

            if (Op == "count_in_window") return state.RecentEvents.Count >= Threshold;

            if (Op == "pair_exists")
            {
                for (int i = 0; i < state.RecentEvents.Count; i++)
                    for (int j = i + 1; j < state.RecentEvents.Count; j++)
                        if (GeoUtils.DistanceMeters(state.RecentEvents[i].Location, state.RecentEvents[j].Location) < Threshold) return true;
            }
            return false;
        }
    }
}
