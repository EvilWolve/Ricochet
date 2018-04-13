using Unity.Entities;
using Unity.Mathematics;

namespace Ricochet.Physics
{
    // This should not change after it is initially set, and will be the same for the majority of projectiles
    public struct SpearcastData : ISharedComponentData
    {
        // I assume that using a float3 for storage is more efficient than using a float and float2.

        /// <summary>
        /// offset.z determines the forward offset of the spear tip from the center.
        /// offset.xy determine the 2D-offset of the sides of the triangle making up the spearhead.
        /// </summary>
        public float3 Offset;
    }
}
