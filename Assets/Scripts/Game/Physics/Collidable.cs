using Unity.Entities;

namespace Ricochet.Physics
{
    // The scale of a collidable shouldn't change after its initialisation, and the majority of collidables will have the same scale.
    // TODO: Make this ISharedComponentData again once you figure out why it didn't work!
    public struct Collidable : IComponentData
    {
        /// <summary>
        /// For simplicity, collidables are assumed to always be square.
        /// </summary>
        public float Scale;
    }
}
