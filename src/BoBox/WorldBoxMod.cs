using System;
using BoBox.Api;
using BoBox.Bridge;
using BoBox.Brain;
using BoBox.Game;
using UnityEngine;

namespace BoBox
{
    /// <summary>
    /// Entry point. The game's ModLoader (decompiled/ModLoader.cs) loads BoBox.dll and adds
    /// the type "BoBox.WorldBoxMod" to a new GameObject, so the name of this class is fixed.
    /// </summary>
    public class WorldBoxMod : MonoBehaviour
    {
        private const string PortEnvVar = "BOBOX_PORT";
        private const int DefaultPort = 8765;
        private const int MinPort = 1024;
        private const int MaxPort = 65535;
        private const float TickErrorLogIntervalSeconds = 10f;

        // Cached once, so the per-frame ticks allocate no delegates.
        private static readonly Action MapSizesTick = MapSizes.Tick;
        private static readonly Action ScenarioCacheTick = ScenarioCache.Tick;

        private BridgeServer _server;
        private bool _isBrainInstallAttempted;
        private bool _isMapSizesInstalled;
        private bool _isScenarioCacheEnabled;
        private readonly TickErrorState _mapSizesTickError = new TickErrorState();
        private readonly TickErrorState _scenarioTickError = new TickErrorState();

        private void Awake()
        {
            try
            {
                var server = new BridgeServer(ResolvePort(), Router.Handle);
                server.Start();
                _server = server;
            }
            catch (Exception ex)
            {
                Log.Error("failed to start, the game keeps running without the bridge", ex);
            }

            try
            {
                ActiveRules.Reload();
            }
            catch (Exception ex)
            {
                Log.Error("rules load failed, using the built-in rules", ex);
            }
        }

        private void Update()
        {
            InstallBrainOnce();
            RunTicks();
            _server?.ProcessPending();
        }

        // City behaviour and map size assets exist once the game has loaded; install everything one time only.
        // Each install has its own try/catch, so one failure leaves the others working.
        private void InstallBrainOnce()
        {
            if (_isBrainInstallAttempted || !Config.game_loaded)
            {
                return;
            }

            _isBrainInstallAttempted = true;
            try
            {
                BrainInstaller.Install();
            }
            catch (Exception ex)
            {
                Log.Error("colony brain install failed, the game keeps its own job logic", ex);
            }

            try
            {
                BuildInstaller.Install();
            }
            catch (Exception ex)
            {
                Log.Error("build brain install failed, the game keeps its own build step", ex);
            }

            try
            {
                MapSizes.Install();
                _isMapSizesInstalled = true;
            }
            catch (Exception ex)
            {
                // Sizes added before the throw stay in the game: keep Tick running so their names are repaired.
                _isMapSizesInstalled = MapSizes.HasAddedSizes;
                Log.Error("map sizes install failed, the game keeps its own sizes", ex);
            }

            // Checked once here, so a game update that renames a City signals member logs one line, not one per frame.
            try
            {
                _isScenarioCacheEnabled = CityInternals.IsSignalsAvailable;
                if (!_isScenarioCacheEnabled)
                {
                    Log.Warn("City signals members not found, the scenario cache stays off and cities plan on the default pathway");
                }
            }
            catch (Exception ex)
            {
                Log.Error("scenario cache check failed, the scenario cache stays off", ex);
            }
        }

        private void RunTicks()
        {
            if (!Config.game_loaded)
            {
                return;
            }

            if (_isMapSizesInstalled)
            {
                RunTick(MapSizesTick, "map sizes", _mapSizesTickError);
            }

            if (_isScenarioCacheEnabled)
            {
                RunTick(ScenarioCacheTick, "scenario cache", _scenarioTickError);
            }
        }

        // A tick that throws every frame would flood Player.log. The first failure logs at once. After that an
        // error logs only when its message differs from the last logged one AND at least TickErrorLogIntervalSeconds
        // passed since the last log, so alternating or varying messages log at most once per interval.
        // A successful tick clears the last message, so a recover-then-fail logs again once the interval is over.
        private static void RunTick(Action tick, string name, TickErrorState state)
        {
            try
            {
                tick();
                state.LastMessage = null;
            }
            catch (Exception ex)
            {
                ReportTickError(name, state, ex);
            }
        }

        // Runs inside a catch block, so it must never throw: a failure to log is dropped.
        private static void ReportTickError(string name, TickErrorState state, Exception ex)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (ex.Message == state.LastMessage || now - state.LastLoggedAt < TickErrorLogIntervalSeconds)
                {
                    return;
                }

                state.LastMessage = ex.Message;
                state.LastLoggedAt = now;
                Log.Error(name + " tick failed (repeats are not logged, new messages at most every "
                    + TickErrorLogIntervalSeconds + "s)", ex);
            }
            catch (Exception)
            {
                // Logging is best effort; a failing logger must not take the game down.
            }
        }

        // Per tick name: the last logged message and when it was logged.
        private sealed class TickErrorState
        {
            public string LastMessage;
            public float LastLoggedAt = float.NegativeInfinity;
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void StopServer()
        {
            _server?.Stop();
            _server = null;
        }

        private static int ResolvePort()
        {
            string raw = Environment.GetEnvironmentVariable(PortEnvVar);
            if (string.IsNullOrEmpty(raw))
            {
                return DefaultPort;
            }

            if (int.TryParse(raw, out int port) && port >= MinPort && port <= MaxPort)
            {
                return port;
            }

            Log.Warn("ignoring invalid " + PortEnvVar + "=" + raw + ", using " + DefaultPort);
            return DefaultPort;
        }
    }
}
