using ai.behaviours;
using BoBox.Game;

namespace BoBox.Brain
{
    /// <summary>
    /// Swaps the CityBehBuild step in tasks_city["build"] for <see cref="BoBoxCityBuild"/>, the same way
    /// <see cref="BrainInstaller"/> swaps the job step. When Game.CityInternals.IsBuildAvailable is false
    /// the stock step stays. Safe to call more than once; steps that are already BoBox's are left alone.
    /// </summary>
    internal static class BuildInstaller
    {
        // The task that runs the build step (decompiled/ai.behaviours/BehaviourTaskCityLibrary.cs, id "build").
        private const string TaskId = "build";

        public static void Install()
        {
            if (!CityInternals.IsBuildAvailable)
            {
                Log.Error("build brain not installed: City build internals not found (game update?), the game's build step stays");
                return;
            }

            BehaviourTaskCity task = AssetManager.tasks_city.get(TaskId);
            if (task == null)
            {
                Log.Error("build brain not installed: city task not found: " + TaskId);
                return;
            }

            int swapped = SwapSteps(task);
            int present = task.list.FindAll(step => step is BoBoxCityBuild).Count;
            if (present == 0)
            {
                Log.Error("build brain not installed: no CityBehBuild step found, the game's build step stays");
                return;
            }

            Log.Info("build brain installed: swapped " + swapped + " build steps, " + present + " active");
        }

        private static int SwapSteps(BehaviourTaskCity task)
        {
            int swapped = 0;
            for (int i = 0; i < task.list.Count; i++)
            {
                BehaviourActionCity step = task.list[i];
                if (step == null || step.GetType() != typeof(CityBehBuild))
                {
                    continue;
                }

                // Mirror BehaviourTaskBase.addBeh: keep the step id, then run create() for its setup.
                var replacement = new BoBoxCityBuild { id = step.id };
                replacement.create();
                task.list[i] = replacement;
                swapped++;
            }

            return swapped;
        }
    }
}
