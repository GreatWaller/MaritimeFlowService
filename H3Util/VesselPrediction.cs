using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace H3Util
{
    public class VesselPrediction
    {
        private readonly Dictionary<string, Dictionary<string, Dictionary<string, double>>> _models =
           new(StringComparer.Ordinal);

        private const int Resolution = 10;
        private const int BinSize = 60;
        private const double AnomalyThreshold = 0.1;

        // -----------------------------
        // 船型映射
        // -----------------------------
        private static string MapVesselTypeToClass(int vtype)
        {
            if (vtype >= 30 && vtype <= 39)
                return (vtype >= 36) ? "pleasure" : "fishing";
            if (vtype >= 50 && vtype <= 59)
                return (vtype == 55 || vtype == 56) ? "police" : "tug";
            if (vtype >= 60 && vtype <= 69) return "passenger";
            if (vtype >= 70 && vtype <= 79) return "cargo";
            if (vtype >= 80 && vtype <= 89) return "tanker";
            if (vtype >= 40 && vtype <= 49) return "highspeed";
            return "other";
        }

        // -----------------------------
        // 加载模型
        // -----------------------------
        public void LoadModels(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"模型目录不存在: {folderPath}");

            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                string json = File.ReadAllText(file);
                var model = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json);
                if (model == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(file);
                string cls = fileName.Replace("h3_transition_model_class_", "", StringComparison.OrdinalIgnoreCase);

                _models[cls] = new Dictionary<string, Dictionary<string, double>>(model, StringComparer.Ordinal);
            }

            Console.WriteLine($"✅ 已加载模型：{string.Join(", ", _models.Keys)}");
        }

        // -----------------------------
        // 工具函数
        // -----------------------------
        private static int DirectionBin(double cog)
        {
            return (int)((((cog % 360) + 360) % 360) / BinSize);
        }

        private static string MakeStateKey(H3Index h3, double cog)
        {
            int dirBin = DirectionBin(cog);
            ulong h3Int = (ulong)h3;  // ✅ H3Index → ulong
            return $"({h3Int}, {dirBin})";  // Python 模型风格
        }

        // 经纬度 → H3Index
        private static H3Index LatLonToH3(double lat, double lon, int resolution)
        {
            // 注意：Coordinate 是 (lon, lat)
            var coord = new Coordinate(lon, lat);
            return coord.ToH3Index(resolution);
        }

        // H3Index → 经纬度
        private static (double lat, double lon) H3ToLatLon(H3Index index)
        {
            var coord = index.ToCoordinate(); // 返回 NetTopologySuite.Geometries.Coordinate
            return (coord.Y, coord.X);        // (lat, lon)
        }

        // -----------------------------
        // 轨迹预测（多步）
        // -----------------------------
        public List<Dictionary<string, object>> PredictPath(double lat, double lon, double cog, int vtype, int steps = 5)
        {
            string shipClass = MapVesselTypeToClass(vtype);
            if (!_models.ContainsKey(shipClass))
                throw new InvalidOperationException($"未加载模型：{shipClass}");

            var model = _models[shipClass];
            var results = new List<Dictionary<string, object>>();

            for (int i = 0; i < steps; i++)
            {
                var h3 = LatLonToH3(lat, lon, Resolution);
                string stateKey = MakeStateKey(h3, cog);

                if (!model.TryGetValue(stateKey, out var nextProbs))
                    break;

                string bestNext = null;
                double bestProb = -1;
                foreach (var kv in nextProbs)
                {
                    if (kv.Value > bestProb)
                    {
                        bestNext = kv.Key;
                        bestProb = kv.Value;
                    }
                }

                if (bestNext == null)
                    break;
                // 先将十进制字符串转为 ulong，再格式化为16进制字符串
                ulong nextVal = ulong.Parse(bestNext);
                string hexStr = nextVal.ToString("x"); // 转为小写十六进制字符串
                var nextH3 = new H3Index(hexStr);
                var (nextLat, nextLon) = H3ToLatLon(nextH3);

                results.Add(new Dictionary<string, object>
                {
                    ["h3"] = bestNext,
                    ["lat"] = nextLat,
                    ["lon"] = nextLon,
                    ["h3"] = bestNext,
                    ["prob"] = bestProb
                });

                lat = nextLat;
                lon = nextLon;
            }

            return results;
        }

        // -----------------------------
        // 异常检测
        // -----------------------------
        public Dictionary<string, object> CheckAnomaly(double lat1, double lon1, double lat2, double lon2, double cog, int vtype)
        {
            string shipClass = MapVesselTypeToClass(vtype);
            if (!_models.ContainsKey(shipClass))
                throw new InvalidOperationException($"未加载模型：{shipClass}");

            var model = _models[shipClass];

            // 经纬度 -> H3 索引
            var h1 = LatLonToH3(lat1, lon1, Resolution);
            var h2 = LatLonToH3(lat2, lon2, Resolution);
            ulong h1Int = (ulong)h1;
            ulong h2Int = (ulong)h2;

            // 同一 H3 格内移动：正常
            if (h1Int == h2Int)
            {
                return new Dictionary<string, object>
                {
                    ["is_anomaly"] = false,
                    ["prob"] = 1.0,
                    ["reason"] = "同一 H3 格内移动（静止或微动）"
                };
            }

            // 构造与 Python 模型一致的键
            int dirBin = DirectionBin(cog);
            string stateKey = $"({h1Int}, {dirBin})";

            // 若当前状态从未出现
            if (!model.TryGetValue(stateKey, out var nextProbs))
            {
                return new Dictionary<string, object>
                {
                    ["is_anomaly"] = true,
                    ["prob"] = 0.0,
                    ["reason"] = $"当前状态 {stateKey} 从未出现"
                };
            }

            string h2Str = h2Int.ToString();
            if (!nextProbs.TryGetValue(h2Str, out double prob))
            {
                return new Dictionary<string, object>
                {
                    ["is_anomaly"] = true,
                    ["prob"] = 0.0,
                    ["reason"] = "该转移未在历史中出现"
                };
            }

            bool isAnomaly = prob < AnomalyThreshold;
            return new Dictionary<string, object>
            {
                ["is_anomaly"] = isAnomaly,
                ["prob"] = prob,
                ["reason"] = isAnomaly ? "低概率转移" : "正常"
            };
        }

        // -----------------------------
        // 新增：单点判定（基于指定船型 vtype 与方向 cog）
        // -----------------------------
        // -----------------------------
        // 异常检测（单位置版）
        // -----------------------------
        public Dictionary<string, object> CheckAnomaly(double lat, double lon, double cog, int vtype)
        {
            string shipClass = MapVesselTypeToClass(vtype);
            if (!_models.ContainsKey(shipClass))
                throw new InvalidOperationException($"未加载模型：{shipClass}");

            var model = _models[shipClass];

            // 经纬度 -> H3 索引
            var h = LatLonToH3(lat, lon, Resolution);
            ulong hInt = (ulong)h;

            int dirBin = DirectionBin(cog);
            string stateKey = $"({hInt}, {dirBin})";

            // 若当前状态从未出现过
            if (!model.ContainsKey(stateKey))
            {
                return new Dictionary<string, object>
                {
                    ["is_anomaly"] = true,
                    ["prob"] = 0.0,
                    ["reason"] = $"当前位置状态 {stateKey} 未在历史中出现"
                };
            }

            // 若模型中存在该状态
            // 可以根据该状态的总体概率或出现频率判断“罕见性”
            var nextProbs = model[stateKey];
            double avgProb = nextProbs.Values.Average();

            bool isAnomaly = avgProb < AnomalyThreshold;
            return new Dictionary<string, object>
            {
                ["is_anomaly"] = isAnomaly,
                ["prob"] = avgProb,
                ["reason"] = isAnomaly ? "罕见状态" : "常见状态"
            };
        }


    }
}
