// See https://aka.ms/new-console-template for more information
using MaritimeFlowService.Config;
using MaritimeFlowService.Engine;
using MaritimeFlowService.Streams;
using System.Security.Cryptography;
using System.Threading;

// 配置线程池以支持多线程规则处理
ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
Console.WriteLine($"配置线程池: 最小工作线程数 {Environment.ProcessorCount * 2}");

Console.WriteLine("Hello, World!");
var engine = new RuleEngine();

// 从 JSON 文件加载规则
var rules = RuleLoader.LoadRulesFromJson("Config/Rules.json");
// 热更新规则
engine.HotUpdateRules(rules);
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