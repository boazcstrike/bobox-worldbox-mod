using BoBox.Bridge;
using BoBox.Game;
using Newtonsoft.Json.Linq;

namespace BoBox.Api
{
    /// <summary>A validated spawn request: actor asset, unit count, and target tile.</summary>
    internal sealed class SpawnRequest
    {
        public string AssetId { get; }
        public int Count { get; }
        public WorldTile Tile { get; }

        private SpawnRequest(string assetId, int count, WorldTile tile)
        {
            AssetId = assetId;
            Count = count;
            Tile = tile;
        }

        /// <summary>
        /// Reads "asset", optional "count" (1..<paramref name="maxCount"/>), "x" and "y".
        /// Throws a 400 <see cref="BridgeException"/> for anything invalid, including a tile the actor cannot stand on.
        /// </summary>
        public static SpawnRequest Parse(JObject body, int defaultCount, int maxCount)
        {
            string assetId = RequestFields.RequireString(body, "asset");
            int count = RequestFields.OptionalInt(body, "count", defaultCount);

            ActorAsset asset = AssetManager.actor_library.getSimple(assetId);
            if (asset == null)
            {
                throw new BridgeException(400, "unknown actor asset: " + assetId);
            }
            if (count < 1 || count > maxCount)
            {
                throw new BridgeException(400, "count must be 1.." + maxCount);
            }

            WorldTile tile = TileRequest.Require(body);
            Spawner.RequireSpawnableTile(asset, tile);
            return new SpawnRequest(assetId, count, tile);
        }
    }
}
