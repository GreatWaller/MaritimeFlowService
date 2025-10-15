using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Streams
{
    internal interface IEventConsumer
    {
        Task StartAsync(Func<MaritimeEvent, Task> onEvent, CancellationToken token);
    }
}
