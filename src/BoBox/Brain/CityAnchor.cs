using System;
using System.Collections.Generic;
using System.Reflection;

namespace BoBox.Brain
{
    /// <summary>
    /// The tile a city is anchored on (bonfire, else best building), read without City.getTile(): getTile()
    /// runs recalculateCityTile() when the cache is empty, and that loops buildings.LoopRandom(), which draws
    /// from the game RNG (decompiled/City.cs lines 386-420). A signals read must not move the RNG. Main thread only.
    /// </summary>
    internal static class CityAnchor
    {
        // Cached tile of City.getTile (decompiled/City.cs line 87, internal), null until the game asks for it.
        private static readonly FieldInfo CityTileField = FindCityTileField();

        /// <summary>
        /// The cached city tile, else the centre tile of the first land zone (the last branch of
        /// recalculateCityTile, City.cs lines 428-436), else the first zone's. Null for a city with no zones.
        /// </summary>
        public static WorldTile Tile(City city, IReadOnlyList<TileZone> zones)
        {
            var cached = (WorldTile)CityTileField?.GetValue(city);
            if (cached != null)
            {
                return cached;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                WorldTile centre = zones[i].centerTile;
                if (centre != null && !centre.Type.ocean)
                {
                    return centre;
                }
            }

            return zones.Count > 0 ? zones[0].centerTile : null;
        }

        private static FieldInfo FindCityTileField()
        {
            FieldInfo field = typeof(City).GetField(
                "_city_tile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(WorldTile) ? field : null;
        }
    }
}
