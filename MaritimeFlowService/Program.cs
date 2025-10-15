// See https://aka.ms/new-console-template for more information
using MaritimeFlowService.Engine;
using MaritimeFlowService.Streams;
using System.Security.Cryptography;
using System.Threading;

// 配置线程池以支持多线程规则处理
ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
Console.WriteLine($"配置线程池: 最小工作线程数 {Environment.ProcessorCount * 2}");

Console.WriteLine("Hello, World!");
var engine = new RuleEngine();
engine.HotUpdateRules(new List<Rule>
            {
                new Rule
                {
                    Id="guardzone_rule",
                    Description="Guard Zone",
                    Priority=200,
                    Enabled=true,
                    Exclusive=false,
                    Conditions={new SpatialCondition
                    {
                        Op="in_polygon",
                        Polygon=new List<(double,double)>{(22.1,114.1),(22.1,114.2),(22.2,114.2),(22.2,114.1)}
                    } },
                    Action=new RuleAction{AlertType="guard_zone_enter",Severity="medium",Notify=new List<string>{"ops_team"}}
                },
                //new Rule
                //{
                //    Id="ship_gathering",
                //    Name="Ship Gathering",
                //    Priority=150,
                //    Enabled=true,
                //    Exclusive=false,
                //    Conditions={new AggregationCondition{Op="count_in_window",Threshold=5,WindowSeconds=1800} },
                //    Action=new RuleAction{AlertType="ship_gathering",Severity="medium",Notify=new List<string>{"ops_team"}}
                //},
                new Rule
                {
                    Id = "ship_gathering",
                    Description = "在指定区域内，船舶聚集且速度超标",
                    CombineLogic = "AND",  // 空间 + 属性 + 聚合
                    Conditions = new List<Condition>
                    {
                        new SpatialCondition
                        {
                            Op = "in_circle",
                            CircleCenter = (22.15, 114.15),
                            RadiusMeters = 5000
                        },
                        new ValueCondition
                        {
                            Field = "Speed",
                            Operator = ">",
                            Value = "20"
                        },
                        new AggregationCondition
                        {
                            Op = "count_in_window",
                            Threshold = 2,
                            WindowSeconds = 1800
                        }
                    },
                    Action = new RuleAction
                    {
                        AlertType = "ship_gathering",
                        Severity = "Medium",
                        Notify=new List<string>{"ops_team"}
                    },
                    Enabled = true
                }
            });

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
for (int i = 0; i < 10; i++)
{
    processor.Enqueue(new MaritimeEvent
    {
        Id = $"EV{i}",
        MMSI = $"MMSI{i}",
        Location = (22.15 + i * 0.001, 114.15 + i * 0.001),
        EventTime = DateTime.UtcNow
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
        ["AISStatus"] = "On",
        ["VesselType"] = "Fishing"
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
        ["AISStatus"] = "On",
        ["VesselType"] = "Cargo"
    }
};
processor.Enqueue(ev1);
await Task.Delay(500);

processor.Enqueue(ev2);
await Task.Delay(500);


Console.WriteLine("Press any key to exit...");
Console.ReadKey();
cts.Cancel();