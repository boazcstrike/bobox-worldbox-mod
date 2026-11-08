using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using BoBox.Game;
using BoBox.Planner.Build;

namespace BoBox.Brain
{
    /// <summary>
    /// Drop-in for the game's CityBehBuild step in tasks_city["build"]: own 5-second timer per city,
    /// candidates from the public CityBehBuild.canUseBuildAsset and hasResourcesForBuildAsset, the pick
    /// from Planner.Build.BuildPlanner, the build through CityBehBuild.tryToBuild or upgradeBuilding.
    /// Inherits CityBehBuild so anything not overridden behaves exactly like the game's step.
    /// Gating mirrors decompiled/ai.behaviours/CityBehBuild.cs execute (lines 19-46) and buildTick (48-132).
    /// When the brain is off, or on any failure, the game's own step runs instead.
    /// </summary>
    internal sealed class BoBoxCityBuild : CityBehBuild
    {
        // CityBehBuild.cs line 39: the game's timer_build is set to 5f after the gates pass.
        private const float BuildIntervalSeconds = 5f;

        // CityBehBuild.cs line 43: the CityFastUpgrades debug option shortens the timer to 0.2f.
        private const float FastIntervalSeconds = 0.2f;

        private const int SeedCityMultiplier = 397;
        private const double MillisecondsPerSecond = 1000.0;
        private const int RoadMinBuildings = 2;

        // A repeating failure after the city acted is counted every time and logged with its count every this many.
        private const int ActedErrorLogEvery = 100;

        // World time (map_stats.world_time, advanced by City.update's pElapsed like timer_build) of the last build tick.
        private readonly Dictionary<long, double> _lastBuildAt = new Dictionary<long, double>();
        private readonly BuildFailureMemory _failures = new BuildFailureMemory();
        private readonly BuildErrorDedupe _errors = new BuildErrorDedupe();
        private readonly BuildErrorDedupe _stockErrors = new BuildErrorDedupe();
        private readonly Dictionary<long, int> _errorsAfterAction = new Dictionary<long, int>();
        private readonly BuildCandidateFinder _finder = new BuildCandidateFinder();
        private readonly BuildWorldWatch _worldWatch = new BuildWorldWatch();

        public override BehResult execute(City pCity)
        {
            // CityFastConstruction needs Building.updateBuild, which is internal: leave that debug mode to the game.
            if (!ColonyBrain.Enabled || DebugConfig.isOn(DebugOption.CityFastConstruction))
            {
                return base.execute(pCity);
            }

            long cityId = 0;
            double now = 0;
            bool hasActed = false;
            try
            {
                cityId = pCity.getID();
                now = World.world.getCurWorldTime();
                if (_worldWatch.HasChanged(now))
                {
                    ClearAll();
                }

                PruneDeadCities();
                if (!IsDue(cityId, now) || !PassesGates(pCity))
                {
                    return BehResult.Continue;
                }

                _lastBuildAt[cityId] = now;
                BuildTick(pCity, cityId, now, () => hasActed = true);
                return BehResult.Continue;
            }
            catch (Exception ex)
            {
                return OnBrainError(pCity, cityId, now, hasActed, ex);
            }
        }

        // Once the brain placed a building or started an upgrade this tick, the game's step must not act a second time.
        private BehResult OnBrainError(City city, long cityId, double now, bool hasActed, Exception ex)
        {
            if (hasActed)
            {
                ReportError(_errors, cityId, now, ex,
                    "build brain failed for city " + cityId + " after it acted, skipping the game's build step this tick");
                CountErrorAfterAction(cityId);
                return BehResult.Continue;
            }

            ReportError(_errors, cityId, now, ex,
                "build brain failed for city " + cityId + ", running the game's build step this tick");
            return RunStockStep(city, cityId, now);
        }

        // The first failure is logged by ReportError with its exception; this adds the running count, not every tick.
        private void CountErrorAfterAction(long cityId)
        {
            _errorsAfterAction.TryGetValue(cityId, out int count);
            count++;
            _errorsAfterAction[cityId] = count;
            if (count % ActedErrorLogEvery == 0)
            {
                Log.Warn("build brain for city " + cityId + " failed after acting " + count + " times");
            }
        }

        // The fallback must not throw either: a failing stock step is logged once per distinct error and the tick ends.
        private BehResult RunStockStep(City city, long cityId, double now)
        {
            try
            {
                return base.execute(city);
            }
            catch (Exception ex)
            {
                ReportError(_stockErrors, cityId, now, ex, "the game's build step failed for city " + cityId);
                return BehResult.Continue;
            }
        }

        // CityBehBuild.cs lines 23-38: SystemBuildTick debug option, captured, in danger. (Timer is IsDue.)
        private static bool PassesGates(City city)
        {
            return DebugConfig.isOn(DebugOption.SystemBuildTick) && !city.isGettingCaptured() && !city.isInDanger();
        }

        // A world time that went backwards (a load) counts as due, so a city never waits on a stale timestamp.
        private bool IsDue(long cityId, double now)
        {
            if (!_lastBuildAt.TryGetValue(cityId, out double last))
            {
                return true;
            }

            float interval = DebugConfig.isOn(DebugOption.CityFastUpgrades) ? FastIntervalSeconds : BuildIntervalSeconds;
            return now < last || now - last >= interval;
        }

        // onActed runs as soon as a building was placed or an upgrade started, so it stays seen if a later call throws.
        // Roads are not an action to protect: the game's road call is idempotent (CityBehBuild.cs line 679).
        private void BuildTick(City city, long cityId, double now, Action onActed)
        {
            MaintainRoads(city);

            // CityBehBuild.cs lines 58-61: no building while the city burns and its leader is not immune.
            if (city.isCityUnderDangerFire() && city.hasLeader() && !city.leader.isImmuneToFire())
            {
                return;
            }

            if (HasConstruction(city))
            {
                return;
            }

            var offered = _finder.Find(city, cityId, now, _failures);
            BuildAttemptResult result = BuildAttempts.Run(
                city, offered, ScenarioCache.Get(cityId), SeedFor(cityId, now), _failures, cityId, now, onActed);
            RecordChoice(cityId, city.name, result.Choice);
        }

        // CityBehBuild.cs lines 48-55: the game makes roads before it picks a building, once the city has a few.
        private static void MaintainRoads(City city)
        {
            if (city.buildings.Count <= RoadMinBuildings || !city.hasCulture() || !city.culture.canUseRoads())
            {
                return;
            }

            Building random = city.buildings.GetRandom();
            if (random == null)
            {
                return;
            }

            CityBehBuild.makeRoadsBuildings(city, random);
        }

        // CityBehBuild.cs lines 62-76: adopt an unfinished building as the city's current one; any such building blocks.
        // City.hasBuildingToBuild (City.cs line 1602) runs first: it drops a pointer to a destroyed or finished
        // building, which would otherwise block the city for good. The scan skips an unusable building, so it never
        // adopts a dying one that the next call would drop again.
        private static bool HasConstruction(City city)
        {
            if (CityInternals.HasBuildingToBuild(city))
            {
                return true;
            }

            Building unfinished = city.buildings.FirstOrDefault(building => building.isUsable() && building.isUnderConstruction());
            if (unfinished == null)
            {
                return false;
            }

            CityInternals.SetUnderConstructionBuilding(city, unfinished);
            return true;
        }

        // World time in milliseconds, so CityFastUpgrades ticks (0.2 s apart) never share a seed. Folded to 32 bits
        // through a long, because a double past int.MaxValue does not cast to int safely.
        private static int SeedFor(long cityId, double now)
        {
            long milliseconds = (long)(now * MillisecondsPerSecond);
            return unchecked((int)cityId * SeedCityMultiplier ^ (int)milliseconds ^ (int)(milliseconds >> 32));
        }

        // The choice is already acted on here, so a recording failure must not send the tick to the game's step.
        private static void RecordChoice(long cityId, string cityName, BuildChoice choice)
        {
            try
            {
                ScenarioCache.RecordBuildChoice(cityId, cityName, choice);
            }
            catch (Exception ex)
            {
                Log.Error("build choice for city " + cityId + " was acted on but not recorded", ex);
            }
        }

        // Logs a city's error at most once per BuildErrorDedupe.WindowSeconds of world time; a clean tick does not reset it.
        private static void ReportError(BuildErrorDedupe seen, long cityId, double now, Exception ex, string text)
        {
            if (!seen.ShouldLog(cityId, ex.GetType().Name + ": " + ex.Message, now))
            {
                return;
            }

            Log.Error(text + " (repeats of this error are not logged for " + BuildErrorDedupe.WindowSeconds + " s)", ex);
        }

        // Cheap check first, run every execute: each map holds one entry per city id, so it only outgrows the
        // live city count once a city is gone. Entries of dead cities can linger while a map stays at or under
        // the live count (e.g. a new city not yet ticked); a map never exceeds the live count plus the cities
        // that died since the last prune, and the next overflow clears every dead id.
        private void PruneDeadCities()
        {
            int liveCount = World.world.cities.Count;
            if (_lastBuildAt.Count <= liveCount && _errors.CityCount <= liveCount
                && _stockErrors.CityCount <= liveCount && _errorsAfterAction.Count <= liveCount
                && _failures.CityCount <= liveCount)
            {
                return;
            }

            var live = new HashSet<long>(World.world.cities.Select(city => city.getID()));
            RemoveDead(_lastBuildAt, live);
            _errors.Prune(live);
            _stockErrors.Prune(live);
            RemoveDead(_errorsAfterAction, live);
            _failures.Prune(live);
        }

        // A new or loaded world can reuse city ids: nothing keyed by a city id may carry over from the old one.
        private void ClearAll()
        {
            _lastBuildAt.Clear();
            _failures.Clear();
            _errors.Clear();
            _stockErrors.Clear();
            _errorsAfterAction.Clear();
            _finder.Clear();
        }

        private static void RemoveDead<TValue>(Dictionary<long, TValue> map, HashSet<long> live)
        {
            foreach (long id in map.Keys.Where(id => !live.Contains(id)).ToList())
            {
                map.Remove(id);
            }
        }
    }
}
