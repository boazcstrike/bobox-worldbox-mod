using System;
using System.Collections.Generic;
using System.Linq;

namespace BoBox.Brain
{
    /// <summary>One brain decision: a changed plan or a failed check for a city.</summary>
    internal sealed class Decision
    {
        public DateTime TimeUtc { get; }
        public long CityId { get; }
        public string CityName { get; }
        public string Kind { get; }
        public string Summary { get; }

        public Decision(DateTime timeUtc, long cityId, string cityName, string kind, string summary)
        {
            TimeUtc = timeUtc;
            CityId = cityId;
            CityName = cityName;
            Kind = kind;
            Summary = summary;
        }
    }

    /// <summary>Thread-safe ring buffer of the most recent decisions, oldest first.</summary>
    internal sealed class DecisionLog
    {
        private readonly object _gate = new object();
        private readonly Queue<Decision> _entries = new Queue<Decision>();
        private readonly int _capacity;

        public DecisionLog(int capacity)
        {
            _capacity = capacity;
        }

        public void Add(Decision decision)
        {
            lock (_gate)
            {
                _entries.Enqueue(decision);
                while (_entries.Count > _capacity)
                {
                    _entries.Dequeue();
                }
            }
        }

        public IReadOnlyList<Decision> Snapshot()
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }
}
