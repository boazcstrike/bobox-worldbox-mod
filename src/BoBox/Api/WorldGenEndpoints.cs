using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Bridge;
using BoBox.Game;
using Newtonsoft.Json.Linq;

namespace BoBox.Api
{
    /// <summary>
    /// Map sizes and new-world generation (docs/research/map-sizes.md). Runs on the Unity main thread
    /// like every handler in <see cref="WorldEndpoints"/>.
    /// </summary>
    internal static class WorldGenEndpoints
    {
        // BoBox's own sizes start at 10 (Game/MapSizes); generating one takes long enough to log.
        private const int LogAboveSize = 9;
        private const int MinSize = 2;

        public static readonly IReadOnlyDictionary<string, Func<ApiRequest, ApiResponse>> Routes =
            new Dictionary<string, Func<ApiRequest, ApiResponse>>
            {
                ["GET /mapsizes"] = GetMapSizes,
                ["POST /world/new"] = NewWorld,
            };

        private static ApiResponse GetMapSizes(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            var sizes = AssetManager.map_sizes.list.Select(asset => new
            {
                id = asset.id,
                size = asset.size,
                tiles = asset.size * MapSizes.TilesPerSize,
                bobox = MapSizes.IsBoBoxSize(asset.id),
            }).ToList();
            return ApiResponse.Ok(new { sizes, current = Config.customMapSize });
        }

        // Validates everything before any config changes, so a rejected request leaves no state behind.
        // clickGenerateNewMap is the menu's own path (transition screen, then generateNewMap), so the
        // world is rebuilt after the response: poll /status until loading is false, then read map_width.
        private static ApiResponse NewWorld(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            JObject body = request.ParseJsonBody();
            string id = RequestFields.RequireString(body, "size");

            // getSimple, not get: get records every unknown id in the library's _not_found set (AssetLibrary.cs:23-34).
            // The smallest size the menu offers is tiny = 2 (MapSizeLibrary.cs:34-35); 1 is only the
            // MapSizeAsset field default (MapSizeAsset.cs:13).
            MapSizeAsset asset = AssetManager.map_sizes.getSimple(id);
            if (asset == null)
            {
                throw new BridgeException(400, "unknown map size id: " + id);
            }
            if (asset.size < MinSize)
            {
                throw new BridgeException(400, "map size too small: " + id + " (size " + asset.size + ", min " + MinSize + ")");
            }

            // game_loaded is set before SmoothLoader finishes, so a second request would queue a second
            // transition on top of the running load. GameState.IsLoading also covers the fade-in.
            if (GameState.IsLoading())
            {
                throw new BridgeException(409, "world is loading");
            }

            // MapBox.generateNewMap (MapBox.cs:1007-1010) resets customMapSize to customMapSizeDefault while
            // _first_gen is true, and the dragon, april-fools and test-map starts leave _first_gen true.
            // Setting the default too makes the first generation honour the requested size. The default is
            // read only while _first_gen is true, so leaving it set is harmless afterwards.
            int tiles = asset.size * MapSizes.TilesPerSize;
            string previousSize = Config.customMapSize;
            string previousDefault = Config.customMapSizeDefault;
            Config.customMapSizeDefault = id;
            Config.customMapSize = id;
            if (asset.size > LogAboveSize)
            {
                Log.Info("generating " + id + " (" + tiles + "\u00b2)");
            }

            try
            {
                World.world.clickGenerateNewMap();
            }
            catch (Exception)
            {
                Config.customMapSize = previousSize;
                Config.customMapSizeDefault = previousDefault;
                throw;
            }

            return ApiResponse.Ok(new { size = id, tiles });
        }
    }
}
