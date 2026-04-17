using LedgeRPG.Core.World;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Pointy-top hex layout in the XZ plane (Y is up, camera looks down).
    /// Matches the paper server's axial (q, r) coordinate system.
    public static class HexLayout
    {
        private const float Sqrt3 = 1.7320508075688772f;

        public static Vector3 ToWorld(HexCoord coord, float tileSize)
        {
            float x = Sqrt3 * (coord.Q + coord.R * 0.5f) * tileSize;
            float z = -1.5f * coord.R * tileSize;
            return new Vector3(x, 0f, z);
        }
    }
}
