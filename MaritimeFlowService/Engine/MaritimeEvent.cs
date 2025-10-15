using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Engine
{
    internal class MaritimeEvent
    {
        public string Id { get; set; }
        public string MMSI { get; set; }
        public (double Lat, double Lon) Location { get; set; }
        public double SpeedKnots { get; set; }
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
        public string State { get; set; }
        public bool RadarPresent { get; set; }
        public bool AISSignal { get; set; }
        public string VideoDetectionClass { get; set; }
        // 通用属性字典（便于扩展）
        public Dictionary<string, object> Attributes { get; set; } = new();
    }
}
