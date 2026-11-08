using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Bridge;

namespace BoBox.Api
{
    /// <summary>
    /// Builds the route table from each endpoint module's own route list and picks the handler
    /// for a request. Runs on the Unity main thread (see <see cref="WorldBoxMod"/>.Update).
    /// </summary>
    internal static class Router
    {
        private static readonly Dictionary<string, Func<ApiRequest, ApiResponse>> Table =
            WorldEndpoints.Routes
                .Concat(ColonyEndpoints.Routes)
                .Concat(WorldGenEndpoints.Routes)
                .Concat(ScenarioEndpoints.Routes)
                .ToDictionary(route => route.Key, route => route.Value, StringComparer.Ordinal);

        public static ApiResponse Handle(ApiRequest request)
        {
            if (Table.TryGetValue(request.Method + " " + request.Path, out var handler))
            {
                return handler(request);
            }

            bool pathKnown = Table.Keys.Any(key => key.EndsWith(" " + request.Path, StringComparison.Ordinal));
            throw pathKnown
                ? new BridgeException(405, "method not allowed: " + request.Method + " " + request.Path)
                : new BridgeException(404, "unknown path: " + request.Path);
        }
    }
}
