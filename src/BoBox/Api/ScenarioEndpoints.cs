using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Bridge;
using BoBox.Brain;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BoBox.Api
{
    /// <summary>
    /// Rules and per-city scenario endpoints: the active rules and their source, rules reload, and each
    /// city's signals, tags, pathway, limits and last build choice. Runs on the Unity main thread.
    /// </summary>
    internal static class ScenarioEndpoints
    {
        private const int UnprocessableStatus = 422;
        private const string KeptPreviousRules = "previous rules";

        private static readonly IReadOnlyList<string> NoMessages = Array.Empty<string>();
        private static readonly SnakeCaseNamingStrategy SnakeCase = new SnakeCaseNamingStrategy();
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(RulesJson.Settings());

        public static readonly IReadOnlyDictionary<string, Func<ApiRequest, ApiResponse>> Routes =
            new Dictionary<string, Func<ApiRequest, ApiResponse>>
            {
                ["GET /rules"] = GetRules,
                ["POST /rules/reload"] = ReloadRules,
                ["GET /colony/scenarios"] = GetScenarios,
            };

        private static ApiResponse GetRules(ApiRequest request)
        {
            ActiveRules.Snapshot state = ActiveRules.Capture();
            RulesLoadResult last = state.LastResult;
            return ApiResponse.Ok(new
            {
                source = state.Source,
                loaded_at = state.LoadedAtUtc?.ToString("o"),
                last_load_ok = state.LastLoadOk,
                errors = last?.Errors ?? NoMessages,
                warnings = last?.Warnings ?? NoMessages,
                rules = RulesJson.ToJToken(state.Rules),
            });
        }

        // The bridge rejects a POST without Content-Length (411), so send an empty body: curl -X POST -d '' <url>/rules/reload
        private static ApiResponse ReloadRules(ApiRequest request)
        {
            RulesLoadResult result = ActiveRules.Reload();
            if (!result.HasRules)
            {
                return new ApiResponse(UnprocessableStatus, new { ok = false, errors = result.Errors, warnings = result.Warnings, kept = KeptPreviousRules });
            }

            return ApiResponse.Ok(new { ok = true, source = result.Source, errors = result.Errors, warnings = result.Warnings });
        }

        private static ApiResponse GetScenarios(ApiRequest request)
        {
            var cities = ScenarioCache.All().Select(CityView).ToList();
            return ApiResponse.Ok(new { rules_source = ActiveRules.Source, cities });
        }

        private static object CityView(CityScenario city)
        {
            return new
            {
                id = city.CityId,
                name = city.CityName,
                refreshed_at = city.IsRefreshed ? (double?)city.RefreshedAt : null,
                rules_loaded_at = city.RulesLoadedAtUtc?.ToString("o"),
                signals = SignalsView(city.Signals),
                tags = TagsView(city.Result),
                pathway = new { id = city.Pathway.Id, reason = city.Result?.Reason },
                limits = new { jobs = RangesView(city.Limits.Jobs), buildings = RangesView(city.Limits.Buildings) },
                last_build = LastBuildView(city),
            };
        }

        // Every property of ScenarioSignals, derived shares included. "No foreign city" (HasForeignCity false) becomes null.
        private static JObject SignalsView(ScenarioSignals signals)
        {
            if (signals == null)
            {
                return null;
            }

            JObject view = JObject.FromObject(signals, Serializer);
            if (!signals.HasForeignCity)
            {
                view["nearest_foreign_city_zones"] = JValue.CreateNull();
            }

            return view;
        }

        private static IReadOnlyList<object> TagsView(ScenarioResult result)
        {
            if (result == null)
            {
                return Array.Empty<object>();
            }

            return result.Tags
                .Select(tag => (object)new
                {
                    tag = ToSnakeCase(tag.ToString()),
                    score = result.Scores.TryGetValue(tag, out double score) ? (double?)score : null,
                })
                .ToList();
        }

        private static string ToSnakeCase(string name)
        {
            return SnakeCase.GetPropertyName(name, false);
        }

        // A max of int.MaxValue means "no upper bound" and is sent as null.
        private static Dictionary<string, object> RangesView(IReadOnlyDictionary<string, CountRange> ranges)
        {
            return ranges.ToDictionary(
                entry => entry.Key,
                entry => (object)new { min = entry.Value.Min, max = entry.Value.Max == int.MaxValue ? (int?)null : entry.Value.Max });
        }

        // at is world time, the same clock as refreshed_at.
        private static object LastBuildView(CityScenario city)
        {
            BuildChoice choice = city.LastBuild;
            if (choice == null)
            {
                return null;
            }

            return new
            {
                order = choice.Candidate?.OrderId,
                type = choice.Candidate?.BuildingType,
                reason = choice.Reason,
                at = city.LastBuildAt,
            };
        }
    }
}
