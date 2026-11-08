using System.Collections.Generic;

namespace BoBox.Game
{
    /// <summary>
    /// Adds the bobox_10 to bobox_16 map sizes (docs/research/map-sizes.md): registers the assets, calls
    /// AssetManager.map_sizes.linkAssets(), and adds the size names. <see cref="Tick"/> runs every frame and
    /// re-adds the names when the game language changes or a name key is gone, with no method patching.
    /// Main thread only.
    /// </summary>
    internal static class MapSizes
    {
        public const int TilesPerSize = 64;
        public const string IdPrefix = "bobox_";

        private const int FirstSize = 10;
        private const string IconPath = "iconIceberg";
        private const string LocaleKeyPrefix = "map_size_";

        private static readonly string[] SizeNames =
        {
            "Vast", "Enormous", "Immense", "Mammoth", "Epic", "Mythic", "Colossal",
        };

        // Built once: Tick runs every frame and must not allocate.
        private static readonly KeyValuePair<string, string>[] Names = BuildNames();

        // A language switch clears every loaded key at once, so one missing key stands for all of them.
        private static readonly string SentinelKey = Names[0].Key;

        private static string _lastLanguage;

        /// <summary>True once <see cref="Install"/> added at least one size, even when it threw afterwards.</summary>
        public static bool HasAddedSizes { get; private set; }

        public static bool IsBoBoxSize(string id)
        {
            return id != null && id.StartsWith(IdPrefix, System.StringComparison.Ordinal);
        }

        public static void Install()
        {
            int added = 0;
            try
            {
                for (int i = 0; i < SizeNames.Length; i++)
                {
                    if (AddAsset(FirstSize + i))
                    {
                        added++;
                    }
                }
            }
            finally
            {
                // Link whatever was added, so a failed AddAsset leaves no unlinked assets behind. The names go in
                // here too, and HasAddedSizes lets the caller keep Tick running to repair them after a throw.
                HasAddedSizes |= added > 0;
                if (added > 0)
                {
                    AssetManager.map_sizes.linkAssets();
                    Log.Info("map sizes added: " + added);
                }

                TryAddNames();
            }
        }

        public static void Tick()
        {
            if (LocalizedTextManager.instance == null)
            {
                return;
            }

            string language = LocalizedTextManager.current_language?.id;
            if (language == _lastLanguage && LocalizedTextManager.stringExists(SentinelKey))
            {
                return;
            }

            AddNames();
            LocalizedTextManager.updateTexts();
            _lastLanguage = language;
        }

        private static bool AddAsset(int size)
        {
            string id = IdPrefix + size;
            if (AssetManager.map_sizes.has(id))
            {
                return false;
            }

            // The size picker lists one entry per size: a second asset of the same size would be a duplicate button.
            MapSizeAsset sameSize = AssetManager.map_sizes.list.Find(existing => existing.size == size);
            if (sameSize != null)
            {
                Log.Info("map size " + id + " skipped, " + sameSize.id + " already has size " + size);
                return false;
            }

            AssetManager.map_sizes.add(new MapSizeAsset
            {
                id = id,
                size = size,
                path_icon = IconPath,
                show_warning = true,
            });
            return true;
        }

        // Runs in Install's finally block: its own failure is logged and must not replace an exception in flight.
        private static void TryAddNames()
        {
            try
            {
                AddNames();
            }
            catch (System.Exception ex)
            {
                Log.Warn("could not add map size names, Tick retries: " + ex.Message);
            }
        }

        private static void AddNames()
        {
            if (LocalizedTextManager.instance == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> name in Names)
            {
                LocalizedTextManager.add(name.Key, name.Value, true);
            }
        }

        private static KeyValuePair<string, string>[] BuildNames()
        {
            var names = new KeyValuePair<string, string>[SizeNames.Length];
            for (int i = 0; i < SizeNames.Length; i++)
            {
                int size = FirstSize + i;
                int tiles = size * TilesPerSize;
                names[i] = new KeyValuePair<string, string>(
                    LocaleKeyPrefix + IdPrefix + size,
                    SizeNames[i] + " " + size + " (" + tiles + "x" + tiles + ")");
            }

            return names;
        }
    }
}
