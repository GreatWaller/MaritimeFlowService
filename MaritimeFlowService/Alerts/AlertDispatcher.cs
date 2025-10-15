using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Alerts
{
    internal class AlertDispatcher
    {
        public async Task DispatchAsync(Alert alert)
        {
            // TODO: Kafka/Webhook/数据库推送
            await Task.Run(() =>
                Console.WriteLine($"[ALERT] {alert.AlertType} | Severity:{alert.Severity} | Entity:{alert.EntityId} | Time:{alert.Timestamp}")
            );
        }
    }
}
