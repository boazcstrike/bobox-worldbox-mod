using System;

namespace BoBox.Bridge
{
    /// <summary>A failure that maps to a specific HTTP status and a JSON {"error"} body.</summary>
    internal sealed class BridgeException : Exception
    {
        public int Status { get; }

        public BridgeException(int status, string message) : base(message)
        {
            Status = status;
        }
    }
}
