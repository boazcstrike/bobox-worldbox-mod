using BoBox.Bridge;
using Newtonsoft.Json.Linq;

namespace BoBox.Api
{
    internal static class TileRequest
    {
        /// <summary>Reads integer "x" and "y" from the body and resolves the tile. 400 when out of bounds.</summary>
        public static WorldTile Require(JObject body)
        {
            int x = RequestFields.RequireInt(body, "x");
            int y = RequestFields.RequireInt(body, "y");
            WorldTile tile = World.world.GetTile(x, y);
            if (tile == null)
            {
                throw new BridgeException(400, "tile out of bounds: " + x + "," + y
                    + " (map is " + MapBox.width + "x" + MapBox.height + ")");
            }

            return tile;
        }
    }
}
