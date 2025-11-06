// See https://aka.ms/new-console-template for more information
using MaritimeFlowService.Config;
using MaritimeFlowService.Engine;
using MaritimeFlowService.Streams;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

// 配置线程池以支持多线程规则处理
ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
Console.WriteLine($"配置线程池: 最小工作线程数 {Environment.ProcessorCount * 2}");

Console.WriteLine("Hello, World!");
var engine = new RuleEngine();

// 从 JSON 文件加载规则
//var rules = RuleLoader.LoadRulesFromJson("Config/Rules.json");

// 读取规则目录（相对于可执行目录或项目目录）
var rulesFolder = Path.Combine(AppContext.BaseDirectory, "Config", "Rules");
var rules = RuleLoader.LoadRulesFromDirectory(rulesFolder);
// 热更新规则
engine.HotUpdateRules(rules);

// 2) 启动目录监听器
var watcher = new RuleWatcher(rulesFolder, engine);
//engine.HotUpdateRules(new List<Rule>
//            {
//                //new Rule
//                //{
//                //    Id="guardzone_rule",
//                //    Description="Guard Zone",
//                //    Priority=200,
//                //    Enabled=true,
//                //    Exclusive=false,
//                //    Conditions={new SpatialCondition
//                //    {
//                //        Op="in_polygon",
//                //        Polygon=new List<(double,double)>{(22.1,114.1),(22.1,114.2),(22.2,114.2),(22.2,114.1)}
//                //    } },
//                //    Action=new RuleAction{AlertType="guard_zone_enter",Severity="medium",Notify=new List<string>{"ops_team"}}
//                //},
//                //new Rule
//                //{
//                //    Id="ship_gathering",
//                //    Name="Ship Gathering",
//                //    Priority=150,
//                //    Enabled=true,
//                //    Exclusive=false,
//                //    Conditions={new AggregationCondition{Op="count_in_window",Threshold=5,WindowSeconds=1800} },
//                //    Action=new RuleAction{AlertType="ship_gathering",Severity="medium",Notify=new List<string>{"ops_team"}}
//                //},
//                new Rule
//                {
//                    Id = "ship_gathering",
//                    Description = "在指定区域内，船舶聚集且速度超标",
//                    CombineLogic = "AND",  // 空间 + 属性 + 聚合
//                    Conditions = new List<Condition>
//                    {
//                        //new SpatialCondition
//                        //{
//                        //    Op = "in_circle",
//                        //    CircleCenter = (22.15, 114.15),
//                        //    RadiusMeters = 5000
//                        //},
//                        new ValueCondition
//                        {
//                            Field = "Speed",
//                            Operator = "<",
//                            Value = "20"
//                        },
//                        new AggregationCondition
//                        {
//                            Op = "count_in_window",
//                            Threshold = 2,
//                            WindowSeconds = 1800,
//                            // 直接使用单一 SpatialCondition 作为 Filter（不使用 CompositeCondition）
//                            Filter = new SpatialCondition
//                            {
//                                Op = "in_circle",
//                                CircleCenter = (22.15, 114.15),
//                                RadiusMeters = 1000
//                            }
//                        }
//                    },
//                    Action = new RuleAction
//                    {
//                        AlertType = "ship_gathering",
//                        Severity = "Medium",
//                        Notify=new List<string>{"ops_team"}
//                    },
//                    Enabled = true
//                },
//                // 演示：基于 H3 模型的位置异常规则（需要提供模型文件夹）
//                new Rule
//                {
//                    Id = "position_anomaly_rule",
//                    Description = "基于 H3 模型的位置异常检测",
//                    CombineLogic = "AND",
//                    Conditions = new List<Condition>
//                    {
//                        new PositionAnomalyCondition
//                        {
//                            // 请将此路径指向包含 h3_transition_model_class_*.json 的目录
//                            ModelFolder = "./model"
//                        }
//                    },
//                    Action = new RuleAction
//                    {
//                        AlertType = "position_anomaly",
//                        Severity = "High",
//                        Notify = new List<string> { "ops_team" }
//                    },
//                    Enabled = true
//                },
//            });

var processor = new EventProcessor(engine);
var cts = new CancellationTokenSource();
processor.Start(cts.Token);

//// 绑定 Kafka 消费者
//IEventConsumer kafkaConsumer = new KafkaEventConsumer(
//    broker: "localhost:9092",
//    topic: "maritime_events",
//    groupId: "maritime_processor"
//);

//// Kafka 消息到达时 → 推给 EventProcessor
//await kafkaConsumer.StartAsync(async ev =>
//{
//    processor.Enqueue(ev);
//    await Task.CompletedTask;
//}, cts.Token);
// ---------- Kafka 配置加载并构造 KafkaEventConsumer（示例） ----------
bool TryLoadKafkaConfig(out string bootstrapServers, out string topic, out string groupId, out string autoOffsetReset, out bool enableAutoCommit, out int? sessionTimeoutMs, out int? maxPollIntervalMs, out string? targetKey)
{
    bootstrapServers = topic = groupId = autoOffsetReset = string.Empty;
    enableAutoCommit = true;
    sessionTimeoutMs = maxPollIntervalMs = null;
    targetKey = null;

    string[] candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
        Path.Combine(AppContext.BaseDirectory, "Config", "appsettings.json"),
        Path.Combine(AppContext.BaseDirectory, "Config", "kafka.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "Config", "kafka.json"),
        "./Config/kafka.json"
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path)) continue;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            JsonElement kafkaEl;
            if (root.TryGetProperty("Kafka", out kafkaEl))
            {
                var consumerEl = kafkaEl.TryGetProperty("Consumer", out var c) ? c : default;
                if (!kafkaEl.TryGetProperty("Topic", out var topicEl))
                    continue;

                bootstrapServers = consumerEl.TryGetProperty("BootstrapServers", out var bs) ? bs.GetString() ?? "" : consumerEl.TryGetProperty("bootstrapServers", out bs) ? bs.GetString() ?? "" : "";
                topic = kafkaEl.TryGetProperty("Topic", out topicEl) ? topicEl.GetString() ?? "" : "";
                groupId = consumerEl.TryGetProperty("GroupId", out var gid) ? gid.GetString() ?? "" : "";

                autoOffsetReset = consumerEl.TryGetProperty("AutoOffsetReset", out var aor) ? aor.GetString() ?? "Latest" : "Latest";
                enableAutoCommit = consumerEl.TryGetProperty("EnableAutoCommit", out var eac) && eac.GetBoolean();
                sessionTimeoutMs = consumerEl.TryGetProperty("SessionTimeoutMs", out var stm) && stm.TryGetInt32(out var stmVal) ? stmVal : null;
                maxPollIntervalMs = consumerEl.TryGetProperty("MaxPollIntervalMs", out var mpi) && mpi.TryGetInt32(out var mpiVal) ? mpiVal : null;

                targetKey = kafkaEl.TryGetProperty("TargetKey", out var tk) ? (tk.ValueKind == JsonValueKind.String ? tk.GetString() : null) : null;

                // 必要字段校验
                if (string.IsNullOrWhiteSpace(bootstrapServers) || string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(groupId))
                    continue;

                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadKafkaConfig: 解析 {path} 失败: {ex.Message}");
            continue;
        }
    }

    return false;
}

IEventConsumer? kafkaConsumer = null;
if (TryLoadKafkaConfig(out var bsrv, out var ktopic, out var kgid, out var koff, out var kenableAutoCommit, out var ksessionTimeout, out var kmaxPoll, out var ktargetKey))
{
    // 使用 KafkaEventConsumer 的辅助构造器
    kafkaConsumer = new KafkaEventConsumer(
        bootstrapServers: bsrv,
        topic: ktopic,
        groupId: kgid,
        autoOffsetReset: koff,
        enableAutoCommit: kenableAutoCommit,
        sessionTimeoutMs: ksessionTimeout,
        maxPollIntervalMs: kmaxPoll,
        targetKey: ktargetKey
    );

    // 启动 Kafka consumer，将收到的事件推给 processor
    _ = kafkaConsumer.StartAsync(async ais =>
    {
        var ev = ConvertToMaritimeEvent(ais);
        processor.Enqueue(ev);
        await Task.CompletedTask;
    }, cts.Token);

    Console.WriteLine($"Kafka consumer started for topic {ktopic}");
}
else
{
    Console.WriteLine("未找到有效 Kafka 配置，跳过 Kafka 绑定（可在 Config/appsettings.json 中添加 Kafka 配置）");
}

// 模拟事件流
// 模拟事件流（示例中为每个事件添加 COG 与 VesselType 供 H3 模型使用）
for (int i = 0; i < 10; i++)
{
    processor.Enqueue(new MaritimeEvent
    {
        Id = $"EV{i}",
        MMSI = $"MMSI",
        Location = (22.15 + i * 0.001, 114.15 + i * 0.001),
        EventTime = DateTime.UtcNow,
        Attributes =
        {
            ["Speed"] = 15.0 + i,
            ["COG"] = (double)(i * 10 % 360),
            ["VesselType"] = 70, // 示例船型代码（整数），便于模型映射
            ["AISStatus"] = "On",
            ["VesselTypeLabel"] = "Cargo"
        },
    });
    await Task.Delay(500);
}

var ev1 = new MaritimeEvent
{
    Id = "Ship001",
    MMSI = $"MMSI{10}",
    Location = (22.15, 114.15),
    EventTime = DateTime.UtcNow,
    Attributes =
    {
        ["Speed"] = 25.0,
        ["COG"] = 45.0,
        ["VesselType"] = 70,
        ["AISStatus"] = "On",
        ["VesselTypeLabel"] = "Fishing"
    },
};
var ev2 = new MaritimeEvent
{
    Id = "Ship002",
    MMSI = $"MMSI{11}",
    Location = (22.15, 114.15),
    EventTime = DateTime.UtcNow,
    Attributes =
    {
        ["Speed"] = 22.0,
        ["COG"] = 90.0,
        ["VesselType"] = 70,
        ["AISStatus"] = "On",
        ["VesselTypeLabel"] = "Cargo"
    }
};
processor.Enqueue(ev1);
await Task.Delay(500);

processor.Enqueue(ev2);
await Task.Delay(500);


Console.WriteLine("Press any key to exit...");
Console.ReadKey();
cts.Cancel();

// helper: 将 AISData 转为 MaritimeEvent
//static MaritimeEvent ConvertFromAIS(MaritimeFlowService.Streams.AISData ais)
//{
//    var brf = ais?.com;
//    var ev = new MaritimeEvent
//    {
//        Id = brf?.UniqueSign ?? $"AIS_{brf?.Mmsi ?? Guid.NewGuid().ToString()}",
//        MMSI = brf?.Mmsi ?? string.Empty,
//        Location = (brf?.Latitude ?? 0.0, brf?.Longitude ?? 0.0),
//        SpeedKnots = brf?.Sog ?? 0.0,
//        State = brf != null ? brf.NavStatus.ToString() : null,
//        RadarPresent = false,
//        AISSignal = brf != null,
//    };

//    // 解析时间（兼容 string/long epoch/ISO）
//    if (brf != null)
//    {
//        var timeRaw = brf.Time.ToString() ?? string.Empty;
//        if (long.TryParse(timeRaw, out var epoch))
//        {
//            try { ev.EventTime = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime; }
//            catch { ev.EventTime = DateTime.UtcNow; }
//        }
//        else if (DateTime.TryParse(timeRaw, out var dt))
//        {
//            ev.EventTime = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
//        }
//    }

//    // 常用属性注入（便于规则条件使用）
//    ev.Attributes["Speed"] = ev.SpeedKnots;
//    ev.Attributes["COG"] = brf?.Cog ?? 0.0;
//    ev.Attributes["VesselType"] = brf?.TerminalType ?? 0;
//    ev.Attributes["AISStatus"] = brf?.Enc ?? "Unknown";
//    ev.Attributes["SourceId"] = brf?.SourceId;
//    ev.Attributes["SourceIp"] = brf?.SourceIp;
//    ev.Attributes["Name"] = brf?.Name;
//    ev.Attributes["ShipId"] = brf?.ShipId;
//    return ev;
//}
static MaritimeEvent ConvertToMaritimeEvent(AISData root)
{
    var c = root?.com;
    if (c == null) return null;

    var evt = new MaritimeEvent
    {
        Id = c.uniqueSign ?? c.globalId ?? Guid.NewGuid().ToString(),
        MMSI = c.mmsi.ToString(),
        Location = (c.lat, c.lon),
        SpeedKnots = c.sog, // AIS SOG 本身为节
        EventTime = DateTimeOffset.FromUnixTimeSeconds(c.time).UtcDateTime,
        State = c.navStatus.ToString(),
        RadarPresent = (c.fuseFlags & 0x02) != 0, // 假设 fuseFlags bit 表示 Radar
        AISSignal = !string.IsNullOrEmpty(c.aisName) && c.mmsi > 0,
        VideoDetectionClass = null // JSON 未给出
    };

    // 扩展字段
    evt.Attributes["CallSign"] = c.callSign;
    evt.Attributes["ShipName"] = c.aisName;
    evt.Attributes["VesselType"] = c.typeCode;
    evt.Attributes["IMO"] = c.imo;
    evt.Attributes["COG"] = c.cog;
    evt.Attributes["Heading"] = c.heading;
    evt.Attributes["ROT"] = c.rot;
    evt.Attributes["Length"] = c.length;
    evt.Attributes["Width"] = c.width;
    evt.Attributes["DWT"] = c.dwt;
    evt.Attributes["SourceIP"] = c.sourceIp;
    evt.Attributes["TerminalType"] = c.terminalType;
    evt.Attributes["NavStatusRaw"] = c.navStatus;

    return evt;
}