using ai.behaviours;
using BoBox.Game;

namespace BoBox.Brain
{
    /// <summary>
    /// Swaps every CityBehCheckCitizenTasks step in the city behaviour tasks for a
    /// <see cref="BoBoxCitizenTasks"/>. No patching: the task's step list is a public field.
    /// Safe to call more than once; steps that are already BoBox's are left alone.
    /// </summary>
    internal static class BrainInstaller
    {
        // Tasks that run the job check (decompiled/ai.behaviours/BehaviourTaskCityLibrary.cs).
        private static readonly string[] TaskIds = { "do_checks", "do_initial_load_check" };

        public static void Install()
        {
            if (!CityInternals.IsAvailable)
            {
                Log.Error("colony brain not installed: City internals not found (game update?)");
                return;
            }

            int swapped = 0;
            int present = 0;
            foreach (string taskId in TaskIds)
            {
                BehaviourTaskCity task = AssetManager.tasks_city.get(taskId);
                if (task == null)
                {
                    Log.Error("city task not found: " + taskId);
                    continue;
                }

                swapped += SwapSteps(task);
                present += task.list.FindAll(step => step is BoBoxCitizenTasks).Count;
            }

            if (present == 0)
            {
                Log.Error("colony brain not installed: no CityBehCheckCitizenTasks step found");
                return;
            }

            Log.Info("colony brain installed: swapped " + swapped + " job steps, " + present + " active");
        }

        private static int SwapSteps(BehaviourTaskCity task)
        {
            int swapped = 0;
            for (int i = 0; i < task.list.Count; i++)
            {
                BehaviourActionCity step = task.list[i];
                if (step == null || step.GetType() != typeof(CityBehCheckCitizenTasks))
                {
                    continue;
                }

                // Mirror BehaviourTaskBase.addBeh: keep the step id, then run create() for its setup.
                var replacement = new BoBoxCitizenTasks { id = step.id };
                replacement.create();
                task.list[i] = replacement;
                swapped++;
            }

            return swapped;
        }
    }
}
