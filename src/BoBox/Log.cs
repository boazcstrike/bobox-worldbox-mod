using System;
using UnityEngine;

namespace BoBox
{
    internal static class Log
    {
        private const string Prefix = "[BoBox]";

        public static void Info(string message)
        {
            Debug.Log(Prefix + " " + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + " " + message);
        }

        /// <summary>Logs the message, followed by the full exception when one is given.</summary>
        public static void Error(string message, Exception ex = null)
        {
            Debug.LogError(Prefix + " " + message + (ex == null ? string.Empty : ": " + ex));
        }
    }
}
