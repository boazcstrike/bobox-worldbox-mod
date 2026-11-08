using System;
using System.Collections.Generic;
using ai.behaviours;
using BoBox.Game;
using BoBox.Planner.Build;

namespace BoBox.Brain
{
    /// <summary>
    /// CityBehBuild.cs calcPossibleBuildings (decompiled lines 166-189): every template order that passes the
    /// game's own checks. An upgrade also needs the cost of its upgrade_to asset, which upgradeBuilding
    /// (lines 150-164) checks only after the pick, so an unaffordable upgrade would waste one of the tick's attempts.
    /// Candidate order follows the template; the planner's pick maps back to its BuildOrder by instance.
    /// An order that cooled down after a failed build is left out before the game's checks, so a cooled-down
    /// dock order does not pay for the dock tile scan. An order that throws is skipped and logged once per order id.
    /// </summary>
    internal sealed class BuildCandidateFinder
    {
        private readonly HashSet<string> _skippedOrders = new HashSet<string>();

        public IReadOnlyList<KeyValuePair<BuildCandidate, BuildOrder>> Find(
            City city, long cityId, double now, BuildFailureMemory failures)
        {
            CityBuildOrderAsset template = AssetManager.city_build_orders.get(city.getActorAsset().build_order_template_id);
            var found = new List<KeyValuePair<BuildCandidate, BuildOrder>>();
            foreach (BuildOrder order in template.list)
            {
                try
                {
                    if (TryEvaluate(city, cityId, now, failures, order, out KeyValuePair<BuildCandidate, BuildOrder> pair))
                    {
                        found.Add(pair);
                    }
                }
                catch (Exception ex)
                {
                    ReportSkipped(order, cityId, ex);
                }
            }

            return found;
        }

        /// <summary>Forgets which orders were logged, for a new or loaded world.</summary>
        public void Clear()
        {
            _skippedOrders.Clear();
        }

        private static bool TryEvaluate(
            City city,
            long cityId,
            double now,
            BuildFailureMemory failures,
            BuildOrder order,
            out KeyValuePair<BuildCandidate, BuildOrder> pair)
        {
            pair = default;
            BuildingAsset asset = order.getBuildingAsset(city);
            string type = asset.type;
            if (failures.HasFailedRecently(cityId, BuildFailureMemory.KeyOf(order.upgrade, order.id, type), now))
            {
                return false;
            }

            if (!CityBehBuild.canUseBuildAsset(order, city) || !CityBehBuild.hasResourcesForBuildAsset(order, city))
            {
                return false;
            }

            if (order.upgrade && !CanAffordUpgrade(city, asset))
            {
                return false;
            }

            var candidate = new BuildCandidate(order.id, type, order.upgrade, city.countBuildingsType(type, false));
            pair = new KeyValuePair<BuildCandidate, BuildOrder>(candidate, order);
            return true;
        }

        private static bool CanAffordUpgrade(City city, BuildingAsset asset)
        {
            BuildingAsset target = string.IsNullOrEmpty(asset.upgrade_to) ? null : AssetManager.buildings.getSimple(asset.upgrade_to);
            return target != null && CityInternals.HasEnoughResourcesFor(city, target.cost);
        }

        private void ReportSkipped(BuildOrder order, long cityId, Exception ex)
        {
            string orderId = order?.id ?? "(null order)";
            if (!_skippedOrders.Add(orderId))
            {
                return;
            }

            Log.Error("build order " + orderId + " skipped for city " + cityId
                + ", the city's other orders are still offered (repeats are not logged)", ex);
        }
    }
}
