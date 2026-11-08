using BoBox.Bridge;

namespace BoBox.Game
{
    /// <summary>The one spawn loop behind /spawn and /scenario/settlers.</summary>
    internal static class Spawner
    {
        /// <summary>
        /// Throws a 400 when a land actor would spawn on a tile it cannot stand on. Walkable land is
        /// <c>Type.ground</c> and not <c>Type.block</c>: ocean, lava and goo have ground false, mountains
        /// are block (TileLibrary.cs:258-342; the same flags gate movement in ActorMove.cs:95 and
        /// Actor.cs:9443). Flying, ocean and boat assets are not checked.
        /// </summary>
        public static void RequireSpawnableTile(ActorAsset asset, WorldTile tile)
        {
            if (asset.flying || asset.force_ocean_creature || asset.is_boat)
            {
                return;
            }

            if (!tile.Type.ground || tile.Type.block)
            {
                throw new BridgeException(400, "tile " + tile.x + "," + tile.y + " is " + tile.Type.id
                    + ", not walkable land for " + asset.id);
            }
        }

        /// <summary>Spawns <paramref name="count"/> units of an actor asset on a tile. Returns how many the game created.</summary>
        public static int Spawn(string assetId, WorldTile tile, int count, bool adult)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (World.world.units.spawnNewUnit(assetId, tile, pAdultAge: adult) != null)
                {
                    spawned++;
                }
            }

            return spawned;
        }
    }
}
