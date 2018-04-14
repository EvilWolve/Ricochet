using Framework.Rendering;
using Ricochet.Configuration;
using Ricochet.Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms2D;
using UnityEngine;

namespace Ricochet.Bootstrap
{
    public class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] BoardConfig boardConfig;

        EntityArchetype enemyCollidableArchetype;
        EntityArchetype boundaryCollidableArchetype;
        EntityArchetype bulletArchetype;

        void Awake()
        {
            this.CreateArchetypes();

            this.CreateBoard();
        }

        void CreateArchetypes()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            // TODO: Should also have Health component
            this.enemyCollidableArchetype = entityManager.CreateArchetype(
                typeof(Position2D), typeof(Heading2D), typeof(SpriteInstanceRendererComponent),
                typeof(Collidable));

            // Boundaries don't have to be visible
            this.boundaryCollidableArchetype = entityManager.CreateArchetype(
                typeof(Position2D), typeof(Heading2D), typeof(Collidable));

            // TODO: Add damage component (Should be SharedComponentData!)
            this.bulletArchetype = entityManager.CreateArchetype(
                typeof(Position2D), typeof(Heading2D), typeof(SpriteInstanceRendererComponent),
                typeof(SpearcastData));
        }

        void CreateBoard()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            Entity boundaryPrefabEntity = entityManager.CreateEntity(this.boundaryCollidableArchetype);

            float2 heading = new float2(1f, 0f);
            float boundaryScale = this.boardConfig.BoardDimensions.y;
            float offsetFromOrigin = (this.boardConfig.BoardDimensions.x + boundaryScale) / 2f;

            Collidable collidable = new Collidable {Scale = boundaryScale};

            Entity leftBoundary = entityManager.Instantiate(boundaryPrefabEntity);
            entityManager.SetComponentData(leftBoundary, new Position2D {Value = new float2(-offsetFromOrigin, boundaryScale / 2f)});
            entityManager.SetComponentData(leftBoundary, new Heading2D {Value = heading});
            entityManager.SetComponentData(leftBoundary, collidable);

            Entity rightBoundary = entityManager.Instantiate(boundaryPrefabEntity);
            entityManager.SetComponentData(rightBoundary, new Position2D {Value = new float2(offsetFromOrigin, boundaryScale / 2f)});
            entityManager.SetComponentData(rightBoundary, new Heading2D {Value = heading});
            entityManager.SetComponentData(rightBoundary, collidable);

            boundaryScale = this.boardConfig.BoardDimensions.x * 2; // Error margin. I don't want some bullet to slip through where the two boundaries intersect.
            offsetFromOrigin = this.boardConfig.BoardDimensions.y + boundaryScale / 2f;
            collidable = new Collidable {Scale = boundaryScale};
            
            Entity topBoundary = entityManager.Instantiate(boundaryPrefabEntity);
            entityManager.SetComponentData(topBoundary, new Position2D {Value = new float2(0f, offsetFromOrigin)});
            entityManager.SetComponentData(topBoundary, new Heading2D {Value = heading});
            entityManager.SetComponentData(topBoundary, collidable);
            
            entityManager.DestroyEntity(boundaryPrefabEntity);
        }
    }
}