using Unity.Entities;
using Unity.Mathematics;

namespace Ricochet.Physics
{
    public struct SpearcastData : IComponentData
    {
        // I assume that using a float3 for storage is more efficient than using a float and float2.

        /// <summary>
        /// offset.z determines the forward offset of the spear tip from the center.
        /// offset.xy determine the 2D-offset of the sides of the triangle making up the spearhead.
        /// </summary>
        public float3 Offset;
    }
}
