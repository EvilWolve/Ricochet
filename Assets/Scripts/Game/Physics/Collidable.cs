using Unity.Entities;

namespace Ricochet.Physics
{
    // The scale of a collidable shouldn't change after its initialisation, and the majority of collidables will have the same scale.
    public struct Collidable : ISharedComponentData
    {
        /// <summary>
        /// For simplicity, collidables are assumed to always be square.
        /// </summary>
        public float Scale;
    }
}
