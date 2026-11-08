using System;
using BoBox.Game;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using BoBox.Rules;

namespace BoBox.Brain
{
    /// <summary>Build limits of a city from its fresh signals. Main thread only.</summary>
    internal static class CityLimits
    {
        // No readable zones: unbounded limits. AreaInfo(zones: 0) would give a maximum of 0 and forbid all building.
        public static Limits For(City city, ScenarioSignals signals, ScenarioResult result, ColonyRules rules)
        {
            if (signals.Zones <= 0)
            {
                return Limits.Unbounded;
            }

            // Zone grid is ZONE_AMOUNT_X by ZONE_AMOUNT_Y (decompiled/Config.cs lines 45, 47) and a save may be non-square.
            var area = new AreaInfo(
                zones: signals.Zones,
                landTiles: signals.LandTiles,
                adults: city.status.population_adults,
                population: signals.Population,
                mapSize: Math.Max(Config.ZONE_AMOUNT_X, Config.ZONE_AMOUNT_Y));
            return CapacityModel.Limits(area, result.Pathway, rules);
        }
    }
}
