using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoBox.Brain
{
    /// <summary>
    /// Logs failed scenario refreshes without flooding Player.log. A city logs at most once until it refreshes
    /// again, and across all cities a line logs only when its key (exception type plus first stack frame, the message
    /// when there is no stack) differs from the last logged one AND <see cref="IntervalSeconds"/> passed since, so a
    /// systemic failure on 500 cities logs one stack trace even when each message names its own city.
    /// Main thread only.
    /// </summary>
    internal static class ScenarioErrorLog
    {
        private const float IntervalSeconds = 10f;

        private static readonly HashSet<long> Logged = new HashSet<long>();
        private static string _lastKey;
        private static float _lastLoggedAt = float.NegativeInfinity;

        // Runs inside a catch block, so it must never throw: a failure to log is dropped.
        public static void Report(long id, City city, Exception ex)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                string key = KeyOf(ex);
                if (Logged.Contains(id) || key == _lastKey || now - _lastLoggedAt < IntervalSeconds)
                {
                    return;
                }

                Logged.Add(id);
                _lastKey = key;
                _lastLoggedAt = now;
                Log.Error("scenario refresh failed for city " + id + " (" + NameOf(city) + "), repeats are not logged, "
                    + "new messages at most every " + IntervalSeconds + "s", ex);
            }
            catch (Exception)
            {
                // Logging is best effort; a failing logger must not take the game down.
            }
        }

        private static string KeyOf(Exception ex)
        {
            string firstFrame = FirstLine(ex.StackTrace);
            return ex.GetType().Name + " " + (firstFrame.Length > 0 ? firstFrame : ex.Message);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int end = text.IndexOf('\n');
            return (end < 0 ? text : text.Substring(0, end)).Trim();
        }

        // A dead city can throw on name; the name is only for the log line.
        private static string NameOf(City city)
        {
            try
            {
                return city.name;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        /// <summary>A city refreshed fine: its next failure logs again, and so does a failure that came back.</summary>
        public static void Succeeded(long id)
        {
            if (Logged.Remove(id))
            {
                _lastKey = null;
            }
        }

        public static void Forget(ISet<long> aliveIds) => Logged.RemoveWhere(id => !aliveIds.Contains(id));

        public static void Clear()
        {
            Logged.Clear();
            _lastKey = null;
        }
    }
}
