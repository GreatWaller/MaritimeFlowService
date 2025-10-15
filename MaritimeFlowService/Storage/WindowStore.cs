using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Storage
{
    internal class WindowStore
    {
        private readonly List<MaritimeEvent> events = new();
        public void Add(MaritimeEvent ev) => events.Add(ev);

        public List<MaritimeEvent> GetWindow(int seconds)
        {
            var now = DateTime.UtcNow;
            events.RemoveAll(e => (now - e.EventTime).TotalSeconds > seconds);
            return events.ToList();
        }
    }
}
