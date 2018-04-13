using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Transforms2D;

using UnityEngine;
using Unity.Mathematics;

namespace Ricochet.Physics
{
    // TODO: Add UpdateBefore(HandleDamageSystem)
    public class SpearcastSystem : JobComponentSystem
    {
        struct Spearcasters
        {
            public int Length;

            [ReadOnly] public SharedComponentDataArray<SpearcastData> SpearcastData;

            public ComponentDataArray<Position2D> Position;
            public ComponentDataArray<Heading2D> Heading;
        }

        [Inject] Spearcasters spearcasters;

        struct CollisionTargets
        {
            public int Length;

            [ReadOnly] public SharedComponentDataArray<Collidable> Collidable;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
        }

        [Inject] CollisionTargets collisionTargets;

        [ComputeJobOptimization]
        struct CollisionJob : IJobParallelFor
        {
            [ReadOnly] public float RoundedCornerThreshold;

            public NativeArray<float> Distance;

            [ReadOnly] public SharedComponentDataArray<SpearcastData> SpearcastData;
            public ComponentDataArray<Position2D> SpearcasterPosition;
            public ComponentDataArray<Heading2D> SpearcasterHeading;

            [ReadOnly] public SharedComponentDataArray<Collidable> Collidable;
            [ReadOnly] public ComponentDataArray<Position2D> CollidablePosition;

            struct HitInfo
            {
                public float distanceToTarget;

                public Entity hitEntity;
                public float2 hitPoint;
                public int hitBoxSideIndex;

                public void SetDefaults()
                {
                    this.distanceToTarget = Mathf.Infinity;
                    this.hitEntity = Entity.Null;
                    this.hitPoint = default (float2);
                    this.hitBoxSideIndex = -1;
                }
            }

            public void Execute(int index)
            {
                float remainingDistance = this.Distance[index];

                Position2D position = this.SpearcasterPosition[index];
                float2 center = position.Value;

                float2 leftPoint = center - this.SpearcastData[index].Offset.xy;
                float2 frontPoint = new float2 (center.x, center.y + this.SpearcastData[index].Offset.z);
                float2 rightPoint = center + this.SpearcastData[index].Offset.xy;

                float2 heading = this.SpearcasterHeading[index].Value;

                HitInfo bestHitInfo = default(HitInfo);
                bestHitInfo.SetDefaults ();

                HitInfo tempHitInfo = default (HitInfo);
                bestHitInfo.SetDefaults ();

                while (remainingDistance > 0f)
                {
                    if (this.Raycast (leftPoint, heading, out bestHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }
                    if (this.Raycast (frontPoint, heading, out bestHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }
                    if (this.Raycast (rightPoint, heading, out bestHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }

                    float2 directionToMove = heading;
                    float distanceToMove = remainingDistance;
                    if (bestHitInfo.distanceToTarget <= remainingDistance && bestHitInfo.hitEntity != Entity.Null) // We hit something!
                    {
                        distanceToMove = bestHitInfo.distanceToTarget;

                        float2 normal = this.CalculateNormal (bestHitInfo.hitEntity, bestHitInfo.hitPoint, bestHitInfo.hitBoxSideIndex, center);
                        heading = this.ReflectHeadingOnNormal (heading, normal);
                    }

                    position.Value = position.Value + distanceToMove * directionToMove;
                    this.SpearcasterPosition[index] = position;

                    remainingDistance -= distanceToMove;
                }
            }

            bool Raycast(float2 position, float2 heading, out HitInfo hitInfo)
            {
                hitInfo = default (HitInfo);
                hitInfo.SetDefaults ();

                for (int i = 0; i < this.Collidable.Length; i++)
                {
                    // TODO: Implement ray-AABB-intersection algorithm, e.g. https://gamedev.stackexchange.com/questions/18436/most-efficient-aabb-vs-ray-collision-algorithms
                }

                return false;
            }

            float2 CalculateNormal(Entity hitEntity, float2 hitPoint, int hitBoxSideIndex, float2 center)
            {
                // TODO: If hitEntity has a RoundedCornerData and the hit point is within RoundedCornerThreshold, use a sphere normal,
                // i.e. draw a vector from the center to the hit point and use that as a normal.

                // TODO: Otherwise, determine the normal based on the box side index, i.e. normal for a collision with the bottom side is (0, -1).

                return default (float2);
            }

            float2 ReflectHeadingOnNormal(float2 heading, float2 normal)
            {
                return heading - 2 * normal * math.dot (normal, heading);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // TODO: This should instead be either injected at startup or loaded from a config file or other system every frame
            // For now, we also assume that all moving objects move at the same speed, if that changes, add a Movable-component that defines speed.
            const float SPEED = 1f;

            float distance = Time.deltaTime * SPEED;
            float[] distances = new float[this.spearcasters.Length];
            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = distance;
            }

            const float ROUNDED_CORNER_THRESHOLD = 0.1f; // TODO: Load this from some config instead

            var collisionJob = new CollisionJob
            {
                Distance = new NativeArray<float> (distances, Allocator.Temp),
                RoundedCornerThreshold = ROUNDED_CORNER_THRESHOLD,
                SpearcastData = this.spearcasters.SpearcastData,
                SpearcasterPosition = this.spearcasters.Position,
                SpearcasterHeading = this.spearcasters.Heading,
                Collidable = this.collisionTargets.Collidable,
                CollidablePosition = this.collisionTargets.Position,
            }.Schedule (this.spearcasters.Length, 1, inputDeps);

            return collisionJob;
        }
    }
}