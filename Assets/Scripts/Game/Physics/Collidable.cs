using Unity.Entities;

namespace Ricochet.Physics
{
    public struct Collidable : IComponentData
    {
        /// <summary>
        /// For simplicity, collidables are assumed to always be square.
        /// </summary>
        public float Scale;
    }
}
