using System;
using System.Diagnostics;

namespace BoBox.Bridge
{
    /// <summary>
    /// Delay and warning rate for a failing accept loop, so a persistent listener error neither spins a
    /// core nor floods the log. Used by the single accept thread only.
    /// </summary>
    internal sealed class AcceptBackoff
    {
        private const int MinDelayMs = 50;
        private const int MaxDelayMs = 2000;
        private const int WarnIntervalMs = 30000;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _delayMs;
        private long _lastWarnMs = -WarnIntervalMs;

        public void Reset()
        {
            _delayMs = 0;
        }

        /// <summary>Doubles the delay on each consecutive failure, from 50 ms up to 2 s.</summary>
        public int NextDelayMs()
        {
            _delayMs = _delayMs == 0 ? MinDelayMs : Math.Min(_delayMs * 2, MaxDelayMs);
            return _delayMs;
        }

        /// <summary>True at most once per 30 s.</summary>
        public bool ShouldWarn()
        {
            long now = _clock.ElapsedMilliseconds;
            if (now - _lastWarnMs < WarnIntervalMs)
            {
                return false;
            }

            _lastWarnMs = now;
            return true;
        }
    }
}
