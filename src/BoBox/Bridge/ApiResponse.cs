using System;

namespace BoBox.Bridge
{
    internal readonly struct ApiResponse
    {
        /// <summary>What a client sees for any unexpected failure. The detail goes to Player.log only.</summary>
        public const string InternalErrorMessage = "internal error, see Player.log";

        public int Status { get; }
        public object Body { get; }

        public ApiResponse(int status, object body)
        {
            Status = status;
            Body = body;
        }

        public static ApiResponse Ok(object body) => new ApiResponse(200, body);

        /// <summary>
        /// A <see cref="BridgeException"/> keeps its own status. Anything else is logged under
        /// <paramref name="logContext"/> and becomes a 500.
        /// </summary>
        public static ApiResponse FromException(Exception ex, string logContext)
        {
            if (ex is BridgeException bridge)
            {
                return new ApiResponse(bridge.Status, new { error = bridge.Message });
            }

            Log.Error(logContext, ex);
            return new ApiResponse(500, new { error = InternalErrorMessage });
        }
    }
}
