using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BoBox.Bridge;

namespace BoBox.Game
{
    internal static class GameState
    {
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Both are internal in Assembly-CSharp (MapBox.cs:117, LoadingScreen.cs:30). Null when a game
        // update renames them; the transition window is then not reported (a warning says so once), the loader
        // check still is.
        private static readonly FieldInfo TransitionScreenField = typeof(MapBox).GetField("transition_screen", InstanceAny);
        private static readonly FieldInfo TransitionModeInField = typeof(LoadingScreen).GetField("modeIn", InstanceAny);

        private static bool _hasWarnedMissingField;

        /// <summary>
        /// True while a world is being built or about to be: the loader has queued steps
        /// (SmoothLoader.cs:97), or the fade-in that precedes generation is running. That fade starts in
        /// MapBox.clickGenerateNewMap (MapBox.cs:987-990); LoadingScreen.startAction clears
        /// <c>modeIn</c> and calls generateNewMap, which queues the loader steps in the same call. A fade-in only
        /// counts while the screen object is active, so a <c>modeIn</c> left set on a hidden screen cannot hold the gate shut.
        /// </summary>
        public static bool IsLoading()
        {
            return SmoothLoader.isLoading() || IsTransitionFadingIn();
        }

        public static bool IsWorldLoaded()
        {
            if (!Config.game_loaded || World.world == null || World.world.units == null || World.world.kingdoms == null)
            {
                return false;
            }

            // A world being rebuilt keeps its tile grid (clearWorld), so the grid check alone says "loaded".
            if (IsLoading())
            {
                return false;
            }

            // GetTile reads the tile grid, which is null until a map exists.
            try
            {
                return World.world.GetTile(0, 0) != null;
            }
            catch (System.NullReferenceException)
            {
                return false;
            }
        }

        public static void RequireWorld()
        {
            if (IsLoading())
            {
                throw new BridgeException(409, "world is loading");
            }

            if (!IsWorldLoaded())
            {
                throw new BridgeException(409, "no world loaded");
            }
        }

        /// <summary>For endpoints that read the asset libraries, which are null until the game has loaded.</summary>
        public static void RequireGameLoaded()
        {
            if (!Config.game_loaded || World.world == null)
            {
                throw new BridgeException(409, "game not loaded yet");
            }
        }

        public static IEnumerable<Kingdom> CivKingdoms()
        {
            return World.world.kingdoms.Where(kingdom => kingdom.isCiv());
        }

        private static bool IsTransitionFadingIn()
        {
            if (TransitionScreenField == null || TransitionModeInField == null)
            {
                WarnMissingFieldOnce();
                return false;
            }

            if (World.world == null)
            {
                return false;
            }

            var screen = TransitionScreenField.GetValue(World.world) as LoadingScreen;
            return screen != null && screen.gameObject.activeInHierarchy
                && TransitionModeInField.GetValue(screen) is bool fadingIn && fadingIn;
        }

        private static void WarnMissingFieldOnce()
        {
            if (_hasWarnedMissingField)
            {
                return;
            }

            _hasWarnedMissingField = true;
            Log.Warn("MapBox.transition_screen or LoadingScreen.modeIn not found (game update?): "
                + "the load fade-in is not detected, only the SmoothLoader check gates loading");
        }
    }
}
