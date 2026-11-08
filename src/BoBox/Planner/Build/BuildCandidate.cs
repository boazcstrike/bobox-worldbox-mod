namespace BoBox.Planner.Build
{
    /// <summary>
    /// One build order that already passed the game's own checks (canUseBuildAsset and
    /// hasResourcesForBuildAsset). The game-side step keeps the mapping back to its BuildOrder:
    /// <see cref="BuildChoice.Candidate"/> is the same instance it passed in.
    /// </summary>
    public sealed class BuildCandidate
    {
        public string OrderId { get; }

        /// <summary>
        /// Building type id (type_*), the key into capacity limits and pathway priority. For an
        /// upgrade this is the type of the building being replaced.
        /// </summary>
        public string BuildingType { get; }

        public bool IsUpgrade { get; }

        /// <summary>The city's buildings of this type, finished or under construction.</summary>
        public int CurrentCount { get; }

        public BuildCandidate(string orderId, string buildingType, bool isUpgrade = false, int currentCount = 0)
        {
            OrderId = orderId ?? string.Empty;
            BuildingType = buildingType ?? string.Empty;
            IsUpgrade = isUpgrade;
            CurrentCount = currentCount;
        }

        public override string ToString() => (IsUpgrade ? "U-" : string.Empty) + OrderId + " (" + BuildingType + ")";
    }
}
