using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Streams
{

    //internal class AISData
    //{
    //    public int MsgType { get; set; }
    //    public BrfData Brf { get; set; }
    //}

    internal class AISData
    {
        public int msgType { get; set; }
        public Com com { get; set; }
    }

    internal class Com
    {
        public string sourceId { get; set; }
        public string sensor { get; set; }
        public string batch { get; set; }
        public string uniqueSign { get; set; }
        public bool fused { get; set; }
        public string sourceIp { get; set; }
        public string BSName { get; set; }
        public string enc { get; set; }
        public int fuseFlags { get; set; }
        public List<string> rawBatches { get; set; }
        public string name { get; set; }
        public string localName { get; set; }
        public string aisName { get; set; }
        public string callSign { get; set; }
        public string shipId { get; set; }
        public long mmsi { get; set; }
        public long imo { get; set; }
        public int bdCard { get; set; }
        public string terminalNo { get; set; }
        public int terminalType { get; set; }
        public string globalId { get; set; }
        public int devLevel { get; set; }
        public int rawShipType { get; set; }
        public int typeCode { get; set; }
        public int countryCode { get; set; }
        public string shipDesc { get; set; }
        public int sailArea { get; set; }
        public double length { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public double dwt { get; set; }
        public double netDWT { get; set; }
        public double grossTon { get; set; }
        public double netTon { get; set; }
        public double maxDraught { get; set; }
        public int trackFlags { get; set; }
        public int shipFlags { get; set; }
        public int checkFlags { get; set; }
        public int otherFlags { get; set; }
        public int countFlags { get; set; }
        public int sourceFlags { get; set; }
        public string eta { get; set; }
        public string dest { get; set; }
        public string regionId { get; set; }
        public string curPos { get; set; }
        public string curPosName { get; set; }
        public string berthID { get; set; }
        public string voyageCode { get; set; }
        public int cargoTypeCode { get; set; }
        public double cargoWeight { get; set; }
        public int planExeState { get; set; }
        public string berthPlanID { get; set; }
        public int planPhraseState { get; set; }
        public string lastHarbor { get; set; }
        public string destHarbor { get; set; }
        public long arrivalTime { get; set; }
        public long departTime { get; set; }
        public int navStatus { get; set; }
        public int Event { get; set; }
        public int activity { get; set; }
        public double anchorLon { get; set; }
        public double anchorLat { get; set; }
        public double anchorDis { get; set; }
        public double invadeDis { get; set; }
        public string shipSign { get; set; }
        public string shipRegCode { get; set; }
        public string shipInitRegCode { get; set; }
        public string shipTel { get; set; }
        public string shipOwner { get; set; }
        public double forwardDraught { get; set; }
        public double behindDraught { get; set; }
        public List<object> trace { get; set; }
        public double lon { get; set; }
        public double lat { get; set; }
        public double alt { get; set; }
        public double sog { get; set; }
        public double cog { get; set; }
        public double rot { get; set; }
        public double heading { get; set; }
        public long time { get; set; }
        public int trackPtCnt { get; set; }
        public double startLon { get; set; }
        public double startLat { get; set; }
        public long startTime { get; set; }
        public bool moved { get; set; }
        public int nav { get; set; }
    }
    internal class AISDataBase
    {
        public string Lat { get; set; } // 纬度（字符串格式）
        public string Lon { get; set; } // 经度（字符串格式）
        public string Mmsi { get; set; } // MMSI号
        public string Name { get; set; }
        public string Time { get; set; } // 时间戳

        // 辅助方法：将字符串经纬度转换为double
        public double Latitude => double.TryParse(Lat, out var lat) ? lat : 0.0;
        public double Longitude => double.TryParse(Lon, out var lon) ? lon : 0.0;
    }
    internal class BrfData : AISDataBase
    {
        public string SourceId { get; set; }
        public string Batch { get; set; }
        public string UniqueSign { get; set; }
        public string SourceIp { get; set; }
        public string BSName { get; set; }
        public string Enc { get; set; }
        public string ShipId { get; set; }
        public int BdCard { get; set; }
        public string TerminalNo { get; set; }
        public int TerminalType { get; set; }
        public string GlobalId { get; set; }
        public int FuseFlags { get; set; }
        public int NavStatus { get; set; }
        public string RegionId { get; set; }
        public double Alt { get; set; }
        public double Sog { get; set; }
        public double Cog { get; set; }
        public double Rot { get; set; }
        public double Heading { get; set; }
        public long Time { get; set; }
        public int TrackPtCnt { get; set; }
        public int CountFlags { get; set; }
    }
}
