using MaritimeFlowService.Alerts;
using MaritimeFlowService.Engine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Streams
{
    internal class EventProcessor
    {
        private readonly RuleEngine engine;
        private readonly BlockingCollection<MaritimeEvent> queue = new();
        private readonly AlertDispatcher dispatcher = new();

        public EventProcessor(RuleEngine engine) { this.engine = engine; }

        public void Enqueue(MaritimeEvent ev) => queue.Add(ev);

        public void Start(CancellationToken token)
        {
            Task.Run(async () =>
            {
                foreach (var ev in queue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        var alerts = engine.Evaluate(ev);
                        foreach (var alert in alerts) await dispatcher.DispatchAsync(alert);
                    }
                    catch (Exception ex) { Console.WriteLine($"Error processing: {ex.Message}"); }
                }
            }, token);
        }
    }
}
