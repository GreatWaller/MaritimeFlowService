using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    // 让 JSON 支持多态条件
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(ValueCondition), typeDiscriminator: "ValueCondition")]
    [JsonDerivedType(typeof(SpatialCondition), typeDiscriminator: "SpatialCondition")]
    [JsonDerivedType(typeof(AggregationCondition), typeDiscriminator: "AggregationCondition")]
    [JsonDerivedType(typeof(PositionAnomalyCondition), typeDiscriminator: "PositionAnomalyCondition")]
    internal abstract class Condition
    {
        public abstract bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine);
    }
}
