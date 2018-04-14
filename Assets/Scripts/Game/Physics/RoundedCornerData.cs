using Unity.Entities;
using Unity.Mathematics;

namespace Ricochet.Physics
{
    public struct RoundedCornerData : IComponentData
    {
        /// <summary>
        /// Contains flags for each corner, starting bottom-right and then going clockwise.
        /// If a flag is true, the corner is rounded. Corners are not rounded if another collidable is adjacent to this corner.
        /// </summary>
        public bool4 Corners;
    }
}
