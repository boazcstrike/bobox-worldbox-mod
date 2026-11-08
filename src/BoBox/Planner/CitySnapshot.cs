using System.Collections.Generic;

namespace BoBox.Planner
{
    /// <summary>
    /// Immutable view of one city at the moment of a job check. Holds plain numbers only, so the
    /// planner runs without the game. Resource-type counts (bushes, plants, hives, trees, ores) are
    /// counted from the city's zones; bushes and hives count only ripe ones, and all but ores are 0
    /// while their stockpile is full, like the game.
    /// </summary>
    public sealed class CitySnapshot
    {
        private static readonly IReadOnlyDictionary<string, int> NoSlots = new Dictionary<string, int>();

        public int Adults { get; }
        public int Children { get; }
        public int Food { get; }
        public int Wood { get; }
        public int Stone { get; }
        public int Gold { get; }
        public int HousingFree { get; }
        public bool HasStorage { get; }
        public bool HasWindmill { get; }
        public bool HasMine { get; }

        /// <summary>Buildings waiting to be built. The game tracks one at a time, so this is 0 or 1.</summary>
        public int PendingBuildings { get; }

        public int Bushes { get; }
        public int Plants { get; }
        public int Hives { get; }
        public int Trees { get; }
        public int Ores { get; }
        public int CropTiles { get; }
        public int Fires { get; }

        /// <summary>Slots the game's own check gave this city, by job id. The planner keeps some of them.</summary>
        public IReadOnlyDictionary<string, int> BaseSlots { get; }

        public CitySnapshot(
            int adults = 0,
            int food = 0,
            int wood = 0,
            int stone = 0,
            int housingFree = 0,
            bool hasStorage = false,
            bool hasWindmill = false,
            bool hasMine = false,
            int pendingBuildings = 0,
            int bushes = 0,
            int plants = 0,
            int hives = 0,
            int trees = 0,
            int ores = 0,
            int cropTiles = 0,
            IReadOnlyDictionary<string, int> baseSlots = null,
            int children = 0,
            int gold = 0,
            int fires = 0)
        {
            Adults = adults;
            Children = children;
            Food = food;
            Wood = wood;
            Stone = stone;
            Gold = gold;
            HousingFree = housingFree;
            HasStorage = hasStorage;
            HasWindmill = hasWindmill;
            HasMine = hasMine;
            PendingBuildings = pendingBuildings;
            Bushes = bushes;
            Plants = plants;
            Hives = hives;
            Trees = trees;
            Ores = ores;
            CropTiles = cropTiles;
            Fires = fires;
            BaseSlots = baseSlots == null ? NoSlots : Copy(baseSlots);
        }

        public int BaseSlotsFor(string jobId)
        {
            return BaseSlots.TryGetValue(jobId, out int count) ? count : 0;
        }

        private static Dictionary<string, int> Copy(IReadOnlyDictionary<string, int> source)
        {
            var copy = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
