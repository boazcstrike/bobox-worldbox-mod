using System.Collections.Generic;
using BoBox.Planner;
using BoBox.Planner.Build;
using BoBox.Planner.Scenario;

namespace BoBox.Rules
{
    /// <summary>
    /// The built-in rules. Thresholds and cadence follow docs/research/terrain-signals.md; pathway order
    /// follows docs/colony-brain.md: scarce, crowded, coastal, resource types, open, default.
    /// Numbers are starting guesses, tuned through rules/bobox-rules.json.
    /// </summary>
    internal static class DefaultRules
    {
        public static readonly ColonyRules Instance = new ColonyRules(
            ColonyRules.CurrentVersion,
            new SignalRules(),
            new ScenarioThresholds(),
            Pathways(),
            new CapacityRules(JobCapacity(), BuildingCapacity()),
            MapScale());

        private static IReadOnlyList<Pathway> Pathways()
        {
            return new[]
            {
                Make("scarce", ScenarioTag.Scarce,
                    Jobs(
                        Pair(JobIds.GathererBushes, 1.3), Pair(JobIds.Hunter, 1.5),
                        Pair(JobIds.Woodcutter, 0.8), Pair(JobIds.Miner, 0.8)),
                    Priority(
                        Pair(BuildingTypes.Stockpile, 10), Pair(BuildingTypes.Windmill, 8), Pair(BuildingTypes.House, 5))),
                Make("crowded", ScenarioTag.CrowdedFrontier,
                    Jobs(Pair(JobIds.Builder, 1.2), Pair(JobIds.Miner, 1.2)),
                    Priority(
                        Pair(BuildingTypes.Barracks, 10), Pair(BuildingTypes.WatchTower, 9), Pair(BuildingTypes.House, 5))),
                Make("coastal", ScenarioTag.Coastal,
                    Jobs(Pair(JobIds.Hunter, 1.2), Pair(JobIds.Farmer, 1.1)),
                    Priority(
                        Pair(BuildingTypes.Docks, 10), Pair(BuildingTypes.Stockpile, 8), Pair(BuildingTypes.House, 5))),
                Make("rocky", ScenarioTag.Rocky,
                    Jobs(Pair(JobIds.Miner, 1.5), Pair(JobIds.MinerDeposit, 1.5), Pair(JobIds.Farmer, 0.9)),
                    Priority(
                        Pair(BuildingTypes.Mine, 10), Pair(BuildingTypes.Stockpile, 8), Pair(BuildingTypes.House, 5))),
                Make("forest", ScenarioTag.ForestRich,
                    Jobs(Pair(JobIds.Woodcutter, 1.5), Pair(JobIds.Builder, 1.2)),
                    Priority(
                        Pair(BuildingTypes.Stockpile, 10), Pair(BuildingTypes.House, 8), Pair(BuildingTypes.Hall, 6))),
                Make("fertile", ScenarioTag.FertilePlains,
                    Jobs(Pair(JobIds.Farmer, 1.5), Pair(JobIds.GathererBushes, 0.8)),
                    Priority(
                        Pair(BuildingTypes.Windmill, 10), Pair(BuildingTypes.House, 8), Pair(BuildingTypes.Market, 4))),
                Make("open_frontier", ScenarioTag.OpenFrontier,
                    Jobs(Pair(JobIds.Builder, 1.5), Pair(JobIds.Woodcutter, 1.2)),
                    Priority(
                        Pair(BuildingTypes.House, 10), Pair(BuildingTypes.Stockpile, 8), Pair(BuildingTypes.Hall, 6))),
                new Pathway(Pathway.NeutralId, PathwayCondition.Always, null,
                    Priority(
                        Pair(BuildingTypes.Stockpile, 8), Pair(BuildingTypes.House, 6), Pair(BuildingTypes.Windmill, 5))),
            };
        }

        // Food, wood and deposit ceilings also grow with population (maxPerPop), so a populous city is not
        // capped at the fixed ceiling: 200 zones and 300 adults employ about 96 % on the neutral pathway.
        private static IReadOnlyDictionary<string, CapacityBase> JobCapacity()
        {
            return new Dictionary<string, CapacityBase>
            {
                [JobIds.Builder] = new CapacityBase(minPerPop: 0, maxPerZone: 0.5, minFloor: 0, maxCeiling: 8),
                [JobIds.Farmer] = new CapacityBase(minPerPop: 0.05, maxPerZone: 2.0, minFloor: 0, maxCeiling: 60, maxPerPop: 0.4),
                [JobIds.GathererBushes] = new CapacityBase(minPerPop: 0, maxPerZone: 1.0, minFloor: 0, maxCeiling: 20, maxPerPop: 0.1),
                [JobIds.GathererHerbs] = new CapacityBase(minPerPop: 0, maxPerZone: 0.5, minFloor: 0, maxCeiling: 10, maxPerPop: 0.05),
                [JobIds.GathererHoney] = new CapacityBase(minPerPop: 0, maxPerZone: 0.25, minFloor: 0, maxCeiling: 5, maxPerPop: 0.02),
                [JobIds.Hunter] = new CapacityBase(minPerPop: 0, maxPerZone: 0.1, minFloor: 0, maxCeiling: 3),
                [JobIds.Woodcutter] = new CapacityBase(minPerPop: 0.05, maxPerZone: 1.0, minFloor: 0, maxCeiling: 30, maxPerPop: 0.3),
                [JobIds.Miner] = new CapacityBase(minPerPop: 0, maxPerZone: 0.5, minFloor: 0, maxCeiling: 15),
                [JobIds.MinerDeposit] = new CapacityBase(minPerPop: 0, maxPerZone: 0.5, minFloor: 0, maxCeiling: 15, maxPerPop: 0.05),
            };
        }

        // House minimum 0.1 per person: houses hold 3 to 7 people (tent to house_5, decompiled BuildingLibrary.cs
        // setHousingSlots), so even fully upgraded houses (7 slots, 0.14 houses per person) need more houses than
        // the minimum forces, and the rest of the house need competes by weight with other types.
        private static IReadOnlyDictionary<string, CapacityBase> BuildingCapacity()
        {
            return new Dictionary<string, CapacityBase>
            {
                [BuildingTypes.House] = new CapacityBase(minPerPop: 0.1, maxPerZone: 1.0, minFloor: 1, maxCeiling: 200),
                [BuildingTypes.Stockpile] = new CapacityBase(minPerPop: 0, maxPerZone: 0.1, minFloor: 1, maxCeiling: 4),
                [BuildingTypes.Windmill] = new CapacityBase(minPerPop: 0.02, maxPerZone: 0.1, minFloor: 0, maxCeiling: 4),
                [BuildingTypes.Mine] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 3),
                [BuildingTypes.Docks] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
                [BuildingTypes.Well] = new CapacityBase(minPerPop: 0, maxPerZone: 0.1, minFloor: 0, maxCeiling: 4),
                [BuildingTypes.Bonfire] = new CapacityBase(minPerPop: 0, maxPerZone: 1.0, minFloor: 1, maxCeiling: 1),
                [BuildingTypes.Hall] = new CapacityBase(minPerPop: 0, maxPerZone: 1.0, minFloor: 0, maxCeiling: 1),
                [BuildingTypes.Temple] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
                [BuildingTypes.Library] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
                [BuildingTypes.Market] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
                [BuildingTypes.Barracks] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
                [BuildingTypes.WatchTower] = new CapacityBase(minPerPop: 0, maxPerZone: 0.1, minFloor: 0, maxCeiling: 8),
                [BuildingTypes.Statue] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 3),
                [BuildingTypes.TrainingDummies] = new CapacityBase(minPerPop: 0, maxPerZone: 0.05, minFloor: 0, maxCeiling: 2),
            };
        }

        // Map size in 64-tile units: up to titanic (8) is the base, larger maps allow larger cities.
        private static IReadOnlyList<MapScaleStep> MapScale()
        {
            return new[]
            {
                new MapScaleStep(minSize: 0, factor: 1.0),
                new MapScaleStep(minSize: 9, factor: 1.25),
                new MapScaleStep(minSize: 12, factor: 1.5),
                new MapScaleStep(minSize: 15, factor: 1.75),
            };
        }

        private static Pathway Make(
            string id, ScenarioTag tag, IReadOnlyDictionary<string, double> jobs, IReadOnlyDictionary<string, int> priority)
        {
            return new Pathway(id, new PathwayCondition(null, new[] { tag }), jobs, priority);
        }

        private static KeyValuePair<string, T> Pair<T>(string key, T value)
        {
            return new KeyValuePair<string, T>(key, value);
        }

        private static IReadOnlyDictionary<string, double> Jobs(params KeyValuePair<string, double>[] pairs)
        {
            return ToMap(pairs);
        }

        private static IReadOnlyDictionary<string, int> Priority(params KeyValuePair<string, int>[] pairs)
        {
            return ToMap(pairs);
        }

        private static IReadOnlyDictionary<string, T> ToMap<T>(IEnumerable<KeyValuePair<string, T>> pairs)
        {
            var map = new Dictionary<string, T>();
            foreach (KeyValuePair<string, T> pair in pairs)
            {
                map[pair.Key] = pair.Value;
            }

            return map;
        }
    }
}
