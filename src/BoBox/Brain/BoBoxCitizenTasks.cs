using System;
using System.Collections.Generic;
using ai.behaviours;
using BoBox.Game;
using BoBox.Planner;

namespace BoBox.Brain
{
    /// <summary>
    /// Drop-in for the game's CityBehCheckCitizenTasks step. Runs the game's check first, so
    /// city.tasks and the default city.jobs are filled, then replaces city.jobs with the
    /// <see cref="ColonyPlanner"/> plan. On any failure before the swap the city keeps the game's default jobs.
    /// </summary>
    internal sealed class BoBoxCitizenTasks : CityBehCheckCitizenTasks
    {
        private const string KeptGameJobs = "keeping the game's jobs";
        private const string PlanNotRecorded = "its plan is applied but was not recorded";

        public override BehResult execute(City pCity)
        {
            BehResult result = base.execute(pCity);
            if (!ColonyBrain.Enabled || !DebugConfig.isOn(DebugOption.SystemCityTasks))
            {
                return result;
            }

            long cityId = 0;
            string cityName = null;
            JobPlan plan;
            try
            {
                cityId = pCity.getID();
                cityName = pCity.name;
                CityScenario scenario = ScenarioCache.Get(cityId);
                plan = ColonyPlanner.Plan(SnapshotBuilder.Build(pCity), scenario?.Pathway, scenario?.Limits);
                ApplyPlan(pCity.jobs, plan);
            }
            catch (Exception ex)
            {
                ReportError(cityId, cityName, ex, KeptGameJobs);
                return result;
            }

            // The jobs are already replaced here, so a recording failure must not claim the game's jobs were kept.
            try
            {
                ColonyBrain.RecordPlan(cityId, cityName, plan);
            }
            catch (Exception ex)
            {
                ReportError(cityId, cityName, ex, PlanNotRecorded);
            }

            return result;
        }

        // Resolves every job asset before touching city.jobs, so a bad id leaves the default jobs intact.
        private static void ApplyPlan(CitizenJobs jobs, JobPlan plan)
        {
            var resolved = new List<KeyValuePair<CitizenJobAsset, int>>();
            foreach (JobLine line in plan.Lines)
            {
                CitizenJobAsset asset = AssetManager.citizen_job_library.get(line.JobId);
                if (asset == null)
                {
                    throw new InvalidOperationException("unknown citizen job id: " + line.JobId);
                }

                resolved.Add(new KeyValuePair<CitizenJobAsset, int>(asset, line.Count));
            }

            jobs.clearJobs();
            foreach (KeyValuePair<CitizenJobAsset, int> slot in resolved)
            {
                jobs.addToJob(slot.Key, slot.Value);
            }
        }

        private static void ReportError(long cityId, string cityName, Exception ex, string outcome)
        {
            string message = ex.GetType().Name + ": " + ex.Message;
            if (ColonyBrain.RecordError(cityId, cityName, message))
            {
                Log.Error("colony brain failed for city " + cityId + ", " + outcome
                    + " (repeats of this error are not logged)", ex);
            }
        }
    }
}
