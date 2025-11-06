using MaritimeFlowService.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class Coordinate
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
    internal class SpatialCondition : Condition
    {
        public string Op { get; set; } // in_polygon / in_circle / side_distance_lt
        public List<Coordinate> Polygon { get; set; }
        public Coordinate CircleCenter { get; set; }
        public double RadiusMeters { get; set; } = 1000;
        public double DistanceMeters { get; set; } = 100;
        public MaritimeEvent OtherEntity { get; set; }

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            return Op switch
            {
                "in_polygon" => GeoUtils.PointInPolygon(ev.Location, Polygon),
                "in_circle" => GeoUtils.DistanceMeters(ev.Location, (CircleCenter.Lat,CircleCenter.Lon)) <= RadiusMeters,
                "side_distance_lt" => OtherEntity != null && GeoUtils.DistanceMeters(ev.Location, OtherEntity.Location) < DistanceMeters,
                _ => false
            };
        }
    }
}
