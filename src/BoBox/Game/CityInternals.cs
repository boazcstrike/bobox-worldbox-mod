using System;
using System.Collections.Generic;
using System.Reflection;

namespace BoBox.Game
{
    /// <summary>
    /// Reads City members that the game marks `internal` (decompiled/City.cs). Methods are bound once to
    /// typed open-instance delegates and fields once to a cached FieldInfo, so game exceptions are not
    /// wrapped. Each group has its own availability flag, false after a game update renames or reshapes
    /// a member; the feature that needs the group then leaves the game's own behaviour in place.
    /// Collections are returned as read-only views of the game's own instances: never cast and mutate them.
    /// </summary>
    internal static class CityInternals
    {
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Job step group: City.cs lines 623, 1602, 1681.
        private static readonly Func<City, bool> HasStorageBuildingCall =
            Bind<Func<City, bool>>("hasStorageBuilding", Type.EmptyTypes);

        private static readonly Func<City, bool> HasBuildingToBuildCall =
            Bind<Func<City, bool>>("hasBuildingToBuild", Type.EmptyTypes);

        private static readonly Func<City, string, bool, TileIsland, bool> HasBuildingTypeCall =
            Bind<Func<City, string, bool, TileIsland, bool>>(
                "hasBuildingType", new[] { typeof(string), typeof(bool), typeof(TileIsland) });

        // Signals group: City.cs lines 39, 51, 53, 55, 57, 61.
        private static readonly FieldInfo KingdomField = Field("kingdom", typeof(Kingdom));
        private static readonly FieldInfo ZonesField = Field("zones", typeof(List<TileZone>));
        private static readonly FieldInfo NeighbourZonesField = Field("neighbour_zones", typeof(HashSet<TileZone>));
        private static readonly FieldInfo BorderZonesField = Field("border_zones", typeof(HashSet<TileZone>));
        private static readonly FieldInfo NeighbourCitiesField = Field("neighbours_cities", typeof(HashSet<City>));
        private static readonly FieldInfo NeighbourKingdomsField = Field("neighbours_kingdoms", typeof(HashSet<Kingdom>));

        // Build group: City.cs lines 63 (under_construction_building), 1568, 1627.
        private static readonly FieldInfo UnderConstructionField = Field("under_construction_building", typeof(Building));

        private static readonly Func<City, string, List<Building>> GetBuildingListOfIdCall =
            Bind<Func<City, string, List<Building>>>("getBuildingListOfID", new[] { typeof(string) });

        private static readonly Func<City, ConstructionCost, bool> HasEnoughResourcesForCall =
            Bind<Func<City, ConstructionCost, bool>>("hasEnoughResourcesFor", new[] { typeof(ConstructionCost) });

        /// <summary>The job step group (BoBoxCitizenTasks). Name kept from before the groups existed.</summary>
        public static bool IsAvailable =>
            HasStorageBuildingCall != null && HasBuildingToBuildCall != null && HasBuildingTypeCall != null;

        /// <summary>The signals group (SignalsReader).</summary>
        public static bool IsSignalsAvailable =>
            KingdomField != null && ZonesField != null && NeighbourZonesField != null && BorderZonesField != null
            && NeighbourCitiesField != null && NeighbourKingdomsField != null;

        /// <summary>The build step group (BoBoxCityBuild). Also needs <see cref="IsAvailable"/> for HasBuildingToBuild.</summary>
        public static bool IsBuildAvailable =>
            IsAvailable && ZonesField != null && UnderConstructionField != null
            && GetBuildingListOfIdCall != null && HasEnoughResourcesForCall != null;

        // Job step group: call only when IsAvailable is true; each throws InvalidOperationException otherwise.
        public static bool HasStorageBuilding(City city)
        {
            return Bound(HasStorageBuildingCall, "hasStorageBuilding")(city);
        }

        /// <summary>Like the game, clears a stale under-construction building as a side effect.</summary>
        public static bool HasBuildingToBuild(City city)
        {
            return Bound(HasBuildingToBuildCall, "hasBuildingToBuild")(city);
        }

        public static bool HasFinishedBuildingType(City city, string buildingTypeId)
        {
            return Bound(HasBuildingTypeCall, "hasBuildingType")(city, buildingTypeId, true, null);
        }

        // Signals group: call only when IsSignalsAvailable is true; each throws InvalidOperationException otherwise.
        public static Kingdom GetKingdom(City city) => (Kingdom)Bound(KingdomField, "kingdom").GetValue(city);

        // GetZones serves both the signals group and the build group: call it when either IsSignalsAvailable
        // or IsBuildAvailable is true (both require the zones field).
        public static IReadOnlyList<TileZone> GetZones(City city) =>
            (List<TileZone>)Bound(ZonesField, "zones").GetValue(city);

        /// <summary>The ring of zones just outside the city.</summary>
        public static IReadOnlyCollection<TileZone> GetNeighbourZones(City city) =>
            (HashSet<TileZone>)Bound(NeighbourZonesField, "neighbour_zones").GetValue(city);

        public static IReadOnlyCollection<TileZone> GetBorderZones(City city) =>
            (HashSet<TileZone>)Bound(BorderZonesField, "border_zones").GetValue(city);

        public static IReadOnlyCollection<City> GetNeighbourCities(City city) =>
            (HashSet<City>)Bound(NeighbourCitiesField, "neighbours_cities").GetValue(city);

        public static IReadOnlyCollection<Kingdom> GetNeighbourKingdoms(City city) =>
            (HashSet<Kingdom>)Bound(NeighbourKingdomsField, "neighbours_kingdoms").GetValue(city);

        // Build group: call only when IsBuildAvailable is true; each throws InvalidOperationException otherwise.
        public static Building GetUnderConstructionBuilding(City city) =>
            (Building)Bound(UnderConstructionField, "under_construction_building").GetValue(city);

        /// <summary>The same write CityBehBuild.buildTick does when it finds an unfinished building.</summary>
        public static void SetUnderConstructionBuilding(City city, Building building)
        {
            Bound(UnderConstructionField, "under_construction_building").SetValue(city, building);
        }

        /// <summary>The city's buildings with this asset id, or null when it has none.</summary>
        public static IReadOnlyList<Building> GetBuildingListOfId(City city, string buildingId) =>
            Bound(GetBuildingListOfIdCall, "getBuildingListOfID")(city, buildingId);

        public static bool HasEnoughResourcesFor(City city, ConstructionCost cost) =>
            Bound(HasEnoughResourcesForCall, "hasEnoughResourcesFor")(city, cost);

        // The shared guard: a member that failed to bind is a caller bug (the availability flag was not checked).
        private static T Bound<T>(T member, string name) where T : class
        {
            if (member == null)
            {
                throw new InvalidOperationException("City." + name + " not bound");
            }

            return member;
        }

        // Delegate.CreateDelegate(type, method) with no target gives an open-instance delegate.
        // Null when the method is missing or its signature no longer matches.
        private static TDelegate Bind<TDelegate>(string methodName, Type[] parameterTypes) where TDelegate : class
        {
            MethodInfo method = typeof(City).GetMethod(methodName, InstanceAny, null, parameterTypes, null);
            return method == null
                ? null
                : (TDelegate)(object)Delegate.CreateDelegate(typeof(TDelegate), method, false);
        }

        // Null when the field is missing or its type changed.
        private static FieldInfo Field(string fieldName, Type fieldType)
        {
            FieldInfo field = typeof(City).GetField(fieldName, InstanceAny);
            return field != null && field.FieldType == fieldType ? field : null;
        }
    }
}
