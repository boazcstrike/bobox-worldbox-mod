using BoBox.Game;

namespace BoBox.Brain
{
    /// <summary>
    /// Tells <see cref="ScenarioCache"/> when the game is loading a world, through <see cref="GameState.IsLoading"/>
    /// (the one definition: queued loader steps or the fade-in), and detects the edge when a load ends.
    /// Config.game_loaded is set once and never reset, so it cannot tell. Mid-load the islands and regions are
    /// not built, so signals read as an empty world and world time stands still: nothing read then may be kept.
    /// Main thread only.
    /// </summary>
    internal static class LoadWatch
    {
        private static bool _wasLoading;

        /// <summary>True while a world is loading: the caller must do no work.</summary>
        public static bool IsLoading()
        {
            bool isLoading = GameState.IsLoading();
            _wasLoading |= isLoading;
            return isLoading;
        }

        /// <summary>True once on the first call after a load ended, so the caller drops what it read before.</summary>
        public static bool HasJustFinished()
        {
            bool hasFinished = _wasLoading;
            _wasLoading = false;
            return hasFinished;
        }
    }
}
