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

        // 可选的内部筛选条件：RecentEvents 仅保留满足该条件的事件（例如 SpatialCondition）
        public Condition? Filter { get; set; }

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            if (ev == null) return false;

            // 确保 RecentEvents 已初始化
            state.RecentEvents ??= new List<MaritimeEvent>();

            var now = DateTime.UtcNow;
            var cutoffSeconds = WindowSeconds;

            // 先按时间窗口筛选
            var recent = state.RecentEvents
                .Where(e => (now - e.EventTime).TotalSeconds <= cutoffSeconds);

            // 如果提供了 Filter，则进一步按该条件筛选
            if (Filter != null)
            {
                // 对现有事件使用 Filter.Evaluate；传入同一 state/engine
                recent = recent.Where(e => Filter.Evaluate(e, state, engine));
            }

            // 替换 RecentEvents 为筛选结果的列表（避免重复分配可改为 RemoveAll）
            state.RecentEvents = recent.ToList();
            Console.WriteLine(state.RecentEvents.Count);

            // 仅在当前事件也满足 Filter（或无 Filter）时添加
            if (Filter == null || Filter.Evaluate(ev, state, engine))
            {
                state.RecentEvents.Add(ev);
            }

            // 操作判断（忽略大小写）
            var op = (Op ?? string.Empty).Trim().ToLowerInvariant();
            if (op == "count_in_window")
            {
                return state.RecentEvents.Count >= Threshold;
            }

            if (op == "pair_exists")
            {
                for (int i = 0; i < state.RecentEvents.Count; i++)
                {
                    for (int j = i + 1; j < state.RecentEvents.Count; j++)
                    {
                        if (GeoUtils.DistanceMeters(state.RecentEvents[i].Location, state.RecentEvents[j].Location) < Threshold)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
