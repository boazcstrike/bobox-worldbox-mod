using System;
using System.Collections.Generic;
using BoBox.Game;
using BoBox.Planner.Scenario;

namespace BoBox.Brain
{
    /// <summary>
    /// Every living city with its kingdom and anchor zone, built once per scenario tick on first use, so the
    /// nearest-foreign-city search of each refresh in that tick walks a plain list and does no reflection.
    /// Main thread only. The list is a snapshot: a city that dies mid-tick is only a stale distance.
    /// </summary>
    internal sealed class CityCenters
    {
        private readonly struct Entry
        {
            public readonly City City;
            public readonly Kingdom Kingdom;
            public readonly TileZone Zone;

            public Entry(City city, Kingdom kingdom, TileZone zone)
            {
                City = city;
                Kingdom = kingdom;
                Zone = zone;
            }
        }

        private List<Entry> _entries;

        /// <summary>
        /// Zone distance (rounded) from <paramref name="center"/> to the nearest city of a kingdom other than
        /// <paramref name="own"/>, or <see cref="ScenarioSignals.NoForeignCity"/>. Squared distance over every
        /// candidate, then one square root.
        /// </summary>
        public int NearestForeign(City city, Kingdom own, TileZone center)
        {
            if (center == null)
            {
                return ScenarioSignals.NoForeignCity;
            }

            List<Entry> entries = _entries ?? (_entries = Build());
            long best = long.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry other = entries[i];
                if (other.City == city || (own != null && other.Kingdom == own))
                {
                    continue;
                }

                long dx = other.Zone.x - center.x;
                long dy = other.Zone.y - center.y;
                best = Math.Min(best, dx * dx + dy * dy);
            }

            return best == long.MaxValue ? ScenarioSignals.NoForeignCity : (int)Math.Round(Math.Sqrt(best));
        }

        private static List<Entry> Build()
        {
            List<City> cities = World.world.cities.list;
            var entries = new List<Entry>(cities.Count);
            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                if (!city.isAlive())
                {
                    continue;
                }

                IReadOnlyList<TileZone> zones = CityInternals.GetZones(city);
                WorldTile tile = CityAnchor.Tile(city, zones);
                TileZone zone = tile?.zone ?? (zones.Count > 0 ? zones[0] : null);
                if (zone != null)
                {
                    entries.Add(new Entry(city, CityInternals.GetKingdom(city), zone));
                }
            }

            return entries;
        }
    }
}
