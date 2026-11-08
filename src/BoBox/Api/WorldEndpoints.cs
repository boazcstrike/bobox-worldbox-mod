using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Bridge;
using BoBox.Game;
using Newtonsoft.Json.Linq;

namespace BoBox.Api
{
    /// <summary>
    /// World endpoints: status, kingdoms, actors, speeds, spawn, powers. Every handler runs on the Unity
    /// main thread (see <see cref="WorldBoxMod"/>.Update), so it may touch the game API freely.
    /// </summary>
    internal static class WorldEndpoints
    {
        private const int DefaultSpawnCount = 1;
        private const int MaxSpawnCount = 100;

        public static readonly IReadOnlyDictionary<string, Func<ApiRequest, ApiResponse>> Routes =
            new Dictionary<string, Func<ApiRequest, ApiResponse>>
            {
                ["GET /status"] = GetStatus,
                ["GET /kingdoms"] = GetKingdoms,
                ["GET /actors/assets"] = GetActorAssets,
                ["GET /speeds"] = GetSpeeds,
                ["POST /speed"] = SetSpeed,
                ["POST /spawn"] = Spawn,
                ["GET /powers"] = GetPowers,
                ["POST /power"] = FirePower,
            };

        private static ApiResponse GetStatus(ApiRequest request)
        {
            bool loading = GameState.IsLoading();
            if (!GameState.IsWorldLoaded())
            {
                return ApiResponse.Ok(new { world_loaded = false, loading });
            }

            return ApiResponse.Ok(new
            {
                world_loaded = true,
                loading,
                unit_count = World.world.units.Count,
                kingdom_count = GameState.CivKingdoms().Count(),
                speed = Config.time_scale_asset?.id,
                map_width = MapBox.width,
                map_height = MapBox.height,
            });
        }

        private static ApiResponse GetKingdoms(ApiRequest request)
        {
            GameState.RequireWorld();
            var kingdoms = GameState.CivKingdoms().Select(kingdom => new
            {
                id = kingdom.getID(),
                name = kingdom.name,
                population = kingdom.getPopulationPeople(),
                city_count = kingdom.countCities(),
                king = kingdom.king != null ? kingdom.king.getName() : null,
            }).ToList();
            return ApiResponse.Ok(new { kingdoms });
        }

        private static ApiResponse GetActorAssets(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            var ids = AssetManager.actor_library.list.Select(asset => asset.id).ToList();
            return ApiResponse.Ok(new { assets = ids });
        }

        private static ApiResponse GetSpeeds(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            var ids = AssetManager.time_scales.list.Select(asset => asset.id).ToList();
            return ApiResponse.Ok(new { speeds = ids, current = Config.time_scale_asset?.id });
        }

        private static ApiResponse SetSpeed(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            JObject body = request.ParseJsonBody();
            string id = RequestFields.RequireString(body, "id");
            if (!AssetManager.time_scales.has(id))
            {
                throw new BridgeException(400, "unknown speed id: " + id);
            }

            Config.setWorldSpeed(id);
            return ApiResponse.Ok(new { speed = Config.time_scale_asset.id });
        }

        private static ApiResponse Spawn(ApiRequest request)
        {
            GameState.RequireWorld();
            SpawnRequest spawn = SpawnRequest.Parse(request.ParseJsonBody(), DefaultSpawnCount, MaxSpawnCount);
            int spawned = Spawner.Spawn(spawn.AssetId, spawn.Tile, spawn.Count, adult: false);

            return ApiResponse.Ok(new
            {
                asset = spawn.AssetId,
                x = spawn.Tile.x,
                y = spawn.Tile.y,
                requested = spawn.Count,
                spawned,
            });
        }

        private static ApiResponse GetPowers(ApiRequest request)
        {
            GameState.RequireGameLoaded();
            var powers = AssetManager.powers.list.Select(power => new
            {
                id = power.id,
                name = power.name,
                fireable = PowerDispatcher.IsFireable(power),
                requires_premium = power.requires_premium,
            }).ToList();
            return ApiResponse.Ok(new { has_premium = Config.hasPremium, powers });
        }

        private static ApiResponse FirePower(ApiRequest request)
        {
            GameState.RequireWorld();
            JObject body = request.ParseJsonBody();
            string id = RequestFields.RequireString(body, "id");
            GodPower power = AssetManager.powers.get(id);
            if (power == null)
            {
                throw new BridgeException(400, "unknown power id: " + id);
            }

            WorldTile tile = TileRequest.Require(body);
            PowerDispatcher.Fire(power, tile);
            return ApiResponse.Ok(new { power = id, x = tile.x, y = tile.y });
        }
    }
}
