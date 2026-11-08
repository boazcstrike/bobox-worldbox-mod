using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Bridge;
using BoBox.Brain;
using BoBox.Game;
using BoBox.Planner;
using Newtonsoft.Json.Linq;

namespace BoBox.Api
{
    /// <summary>
    /// Colony brain endpoints. Like every handler in <see cref="WorldEndpoints"/>, these run on the Unity main thread.
    /// </summary>
    internal static class ColonyEndpoints
    {
        private const int DefaultSettlerCount = 5;
        private const int MaxSettlerCount = 20;

        public static readonly IReadOnlyDictionary<string, Func<ApiRequest, ApiResponse>> Routes =
            new Dictionary<string, Func<ApiRequest, ApiResponse>>
            {
                ["POST /scenario/settlers"] = SpawnSettlers,
                ["GET /colony"] = GetColony,
                ["GET /colony/log"] = GetColonyLog,
                ["POST /brain"] = SetBrain,
            };

        private static ApiResponse SpawnSettlers(ApiRequest request)
        {
            GameState.RequireWorld();
            SpawnRequest spawn = SpawnRequest.Parse(request.ParseJsonBody(), DefaultSettlerCount, MaxSettlerCount);

            int spawned = Spawner.Spawn(spawn.AssetId, spawn.Tile, spawn.Count, adult: true);

            // Units without a city found one themselves only while this law is on (BehCheckBuildCity).
            // The law is saved with the world, so it stays on after the scenario. It is switched on only
            // after a spawn that created units, so a failed spawn leaves the world's laws untouched.
            WorldLawAsset expansion = WorldLawLibrary.world_law_kingdom_expansion;
            bool wasExpansionOn = expansion.isEnabled();
            if (!wasExpansionOn && spawned > 0)
            {
                expansion.toggle(true);
            }

            return ApiResponse.Ok(new
            {
                asset = spawn.AssetId,
                x = spawn.Tile.x,
                y = spawn.Tile.y,
                requested = spawn.Count,
                spawned,
                kingdom_expansion_enabled_now = !wasExpansionOn && spawned > 0,
            });
        }

        private static ApiResponse GetColony(ApiRequest request)
        {
            GameState.RequireWorld();
            var cities = World.world.cities.Select(city =>
            {
                JobPlan plan = ColonyBrain.LastPlanFor(city.getID());
                return new
                {
                    id = city.getID(),
                    name = city.name,
                    adults = city.status.population_adults,
                    food = city.getTotalFood(),
                    wood = city.getResourcesAmount("wood"),
                    stone = city.getResourcesAmount("stone"),
                    plan = PlanView(plan),
                    idle = IdleView(plan),
                };
            }).ToList();
            return ApiResponse.Ok(new { brain_enabled = ColonyBrain.Enabled, cities });
        }

        private static ApiResponse GetColonyLog(ApiRequest request)
        {
            var decisions = ColonyBrain.Decisions.Snapshot().Select(decision => new
            {
                time_utc = decision.TimeUtc.ToString("o"),
                city_id = decision.CityId,
                city = decision.CityName,
                kind = decision.Kind,
                summary = decision.Summary,
            }).ToList();
            return ApiResponse.Ok(new { capacity = ColonyBrain.LogCapacity, decisions });
        }

        private static ApiResponse SetBrain(ApiRequest request)
        {
            JObject body = request.ParseJsonBody();
            ColonyBrain.Enabled = RequestFields.RequireBool(body, "enabled");
            return ApiResponse.Ok(new { brain_enabled = ColonyBrain.Enabled });
        }

        private static IEnumerable<object> PlanView(JobPlan plan)
        {
            if (plan == null)
            {
                return null;
            }

            return plan.Lines.Select(line => new { job = line.JobId, slots = line.Count, reason = line.Reason }).ToList();
        }

        private static object IdleView(JobPlan plan)
        {
            return plan == null ? null : new { adults = plan.Idle, reason = plan.IdleReason };
        }
    }
}
