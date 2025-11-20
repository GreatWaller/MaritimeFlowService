using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

namespace MaritimeFlowService.Alerts
{
    internal class AlertDispatcher
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        // 默认目标 URL，可改为从配置读取
        private const string DefaultAisEndpoint = "http://192.168.1.232/api/marinemind/aisAlarm";

        public async Task DispatchAsync(Alert alert)
        {
            // 本地日志
            await Task.Run(() =>
                Console.WriteLine($"[ALERT] {alert.AlertType} | Severity:{alert.Severity} | Entity:{alert.EntityId} | Time:{alert.Timestamp}")
            );

            // 尝试发送 AIS 告警到指定接口
            try
            {
                //var (lon, lat) = TryParseCoordinates(alert.Notify);
                var (lon, lat) = (alert.Lon , alert.Lat);
                string alarmTime = alert.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                string message = BuildMessage(alert, alarmTime, lon, lat);

                var payload = BuildAisPayload(alert, alarmTime, message, lon, lat);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                string json = JsonSerializer.Serialize(payload, options);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await _httpClient.PostAsync(DefaultAisEndpoint, content);
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ALERT_DISPATCH] POST failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                }
                else
                {
                    Console.WriteLine("[ALERT_DISPATCH] AIS alarm posted successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ALERT_DISPATCH] Exception sending AIS alarm: {ex.Message}");
            }
        }

        private static object BuildAisPayload(Alert alert, string alarmTime, string message, double? lon, double? lat)
        {
            return new
            {
                configId = alert.RuleId ?? string.Empty,
                mmsi = alert.EntityId ?? string.Empty,
                otherMmsi = string.Empty,
                alarmType = alert.AlertType ?? string.Empty,
                lon = lon,
                lat = lat,
                alarmTime = alarmTime,
                message = message
            };
        }

        private static string BuildMessage(Alert alert, string alarmTime, double? lon, double? lat)
        {
            // 合并 notify 文本，便于一次性解析
            string combined = string.Empty;
            if (alert.Notify != null && alert.Notify.Count > 0)
            {
                combined = string.Join(" ", alert.Notify).Trim();
            }

            // 尝试从合并文本中提取若干字段：船名、速度、区域、数量
            string shipName = null;
            double? speed = null;
            string area = null;
            int? count = null;

            if (!string.IsNullOrWhiteSpace(combined))
            {
                // 船名常见键：name / ship / 船名 / Vessel
                var nameMatch = Regex.Match(combined, @"(?:船名|name|ship|vessel)[:=]?\s*(?<name>[\p{L}\d\-\s]{2,50})", RegexOptions.IgnoreCase);
                if (nameMatch.Success) shipName = nameMatch.Groups["name"].Value.Trim();

                // 另一个常见格式：'<NAME> 以7.17節...'，尝试捕获 '<name> 以'
                if (shipName == null)
                {
                    var beforeYi = Regex.Match(combined, @"(?<name>[\p{L}\d\-\s]{2,50})\s+以\s*\d+(\.\d+)?\s*(?:节|節|kn|kt|knot)", RegexOptions.IgnoreCase);
                    if (beforeYi.Success) shipName = beforeYi.Groups["name"].Value.Trim();
                }

                // 速度：数字 + 节/kn/kt/knot，或 speed: value
                var speedMatch = Regex.Match(combined, @"(?<v>\d+(\.\d+)?)\s*(?:节|節|kn|kt|knot)|speed[:=]\s*(?<v2>\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                if (speedMatch.Success)
                {
                    var s = speedMatch.Groups["v"].Success ? speedMatch.Groups["v"].Value : speedMatch.Groups["v2"].Value;
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var sv)) speed = sv;
                }

                // 区域：'区域XXX' 或 area: XXX 或 in area XXX
                var areaMatch = Regex.Match(combined, @"区域(?<a>[\p{L}\d\-\u4e00-\u9fa5\s]{1,40})|area[:=]\s*(?<a2>[^,;，；]+)|in area\s*(?<a3>[^,;，；]+)", RegexOptions.IgnoreCase);
                if (areaMatch.Success)
                {
                    area = areaMatch.Groups["a"].Success ? areaMatch.Groups["a"].Value.Trim()
                          : areaMatch.Groups["a2"].Success ? areaMatch.Groups["a2"].Value.Trim()
                          : areaMatch.Groups["a3"].Value.Trim();
                }

                // 数量：'数量为10' / '船舶数量为10' / 'count=10'
                var countMatch = Regex.Match(combined, @"(?:数量为|船舶数量为|count[:=])\s*(?<n>\d+)", RegexOptions.IgnoreCase);
                if (countMatch.Success && int.TryParse(countMatch.Groups["n"].Value, out var cn)) count = cn;
            }

            // 组合成更详细的消息（中文）
            var subject = !string.IsNullOrWhiteSpace(shipName) ? shipName : (!string.IsNullOrWhiteSpace(alert.EntityId) ? alert.EntityId : "未知船舶");
            var sb = new StringBuilder();
            sb.Append(alarmTime).Append(' ').Append(subject);

            if (speed.HasValue)
            {
                sb.Append($" 以{speed.Value.ToString("0.##", CultureInfo.InvariantCulture)}節的速度");
            }

            if (!string.IsNullOrWhiteSpace(area))
            {
                sb.Append($" 在区域{area}");
            }
            else if (lat.HasValue && lon.HasValue)
            {
                // 使用经纬作为位置信息（保留小数）
                sb.Append($" 在经纬({lon.Value.ToString("0.######", CultureInfo.InvariantCulture)},{lat.Value.ToString("0.######", CultureInfo.InvariantCulture)})");
            }

            sb.Append($" 发生{alert.AlertType}告警");

            if (count.HasValue)
            {
                sb.Append($"，船舶数量为{count.Value}");
            }

            // 附加规则与严重级别，便于排查
            sb.Append($"。规则:{alert.RuleId ?? "N/A"} 严重级别:{alert.Severity ?? "N/A"}");

            // 若 Notify 中存在更详尽原始描述，追加以保留上下文
            if (!string.IsNullOrWhiteSpace(combined))
            {
                // 如果合并文本与已构建消息并非重复，则追加（避免重复）
                var summary = sb.ToString();
                if (!summary.Contains(combined))
                {
                    sb.Append(" 处理信息: ").Append(combined);
                }
            }

            return sb.ToString();
        }

        private static (double? lon, double? lat) TryParseCoordinates(List<string> notify)
        {
            if (notify == null || notify.Count == 0)
                return (null, null);

            double? lon = null;
            double? lat = null;

            // 先尝试找到 "lon:...,lat:..." 或 "lon=...,lat=..." 的配对
            var pairRegex = new Regex(@"(?<lon>-?\d+(\.\d+)?)[^\d\-]+(?<lat>-?\d+(\.\d+)?)");
            foreach (var s in notify)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                // 尝试显式键值
                var lonMatch = Regex.Match(s, @"lon[:=]\s*(?<v>-?\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                var latMatch = Regex.Match(s, @"lat[:=]\s*(?<v>-?\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                if (lonMatch.Success && latMatch.Success)
                {
                    if (double.TryParse(lonMatch.Groups["v"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var lx)) lon = lx;
                    if (double.TryParse(latMatch.Groups["v"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ly)) lat = ly;
                    if (lon.HasValue && lat.HasValue) return (lon, lat);
                }

                // 尝试 "lng" 或 "longitude"
                var lngMatch = Regex.Match(s, @"(lng|longitude)[:=]\s*(?<v>-?\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                var latMatch2 = Regex.Match(s, @"lat[:=]\s*(?<v>-?\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                if (lngMatch.Success && latMatch2.Success)
                {
                    if (double.TryParse(lngMatch.Groups["v"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var lx)) lon = lx;
                    if (double.TryParse(latMatch2.Groups["v"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ly)) lat = ly;
                    if (lon.HasValue && lat.HasValue) return (lon, lat);
                }

                // 尝试一行内有两个数字的形式 "114.0501203,22.2994301" 或 "114.0501203 22.2994301"
                var pair = pairRegex.Match(s);
                if (pair.Success)
                {
                    if (double.TryParse(pair.Groups["lon"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var lx)) lon = lx;
                    if (double.TryParse(pair.Groups["lat"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ly)) lat = ly;
                    if (lon.HasValue && lat.HasValue) return (lon, lat);
                }
            }

            return (lon, lat);
        }
    }
}
