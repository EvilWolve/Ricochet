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

                public int hitEntityIndex;
                public float2 hitPoint;
                public int hitBoxSideIndex;

                public void SetDefaults()
                {
                    this.distanceToTarget = Mathf.Infinity;
                    this.hitEntityIndex = -1;
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
                float2 reciprocalHeading = math.rcp(heading); // TODO: Check if this handles heading of 0 correctly!

                HitInfo bestHitInfo = default(HitInfo);
                bestHitInfo.SetDefaults ();
                
                HitInfo tempHitInfo;

                while (remainingDistance > 0f)
                {
                    if (this.RaycastAllCollidables (leftPoint, reciprocalHeading, out tempHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }
                    if (this.RaycastAllCollidables (frontPoint, reciprocalHeading, out tempHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }
                    if (this.RaycastAllCollidables (rightPoint, reciprocalHeading, out tempHitInfo) && tempHitInfo.distanceToTarget < bestHitInfo.distanceToTarget)
                    {
                        bestHitInfo = tempHitInfo;
                    }

                    float2 directionToMove = heading;
                    float distanceToMove = remainingDistance;
                    if (bestHitInfo.distanceToTarget <= remainingDistance && bestHitInfo.hitEntityIndex >= 0) // We hit something!
                    {
                        distanceToMove = bestHitInfo.distanceToTarget;

                        float2 normal = this.CalculateNormal (bestHitInfo, center);
                        heading = math.reflect (heading, normal);
                        reciprocalHeading = math.rcp(heading);
                    }

                    position.Value = position.Value + distanceToMove * directionToMove;
                    this.SpearcasterPosition[index] = position;

                    remainingDistance -= distanceToMove;
                }
            }

            bool RaycastAllCollidables(float2 rayOrigin, float2 reciprocalHeading, out HitInfo hitInfo)
            {
                hitInfo = default (HitInfo);
                hitInfo.SetDefaults ();

                float minDistance = Mathf.Infinity;
                
                float tempDistance;

                for (int i = 0; i < this.Collidable.Length; i++)
                {
                    if (this.RaycastCollidable(i, rayOrigin, reciprocalHeading, out tempDistance))
                    {
                        if (tempDistance < minDistance)
                        {
                            minDistance = tempDistance;
                            
                            hitInfo.distanceToTarget = minDistance;
                        }
                    }
                }

                hitInfo.hitPoint = rayOrigin + hitInfo.distanceToTarget / reciprocalHeading;

                return hitInfo.hitEntityIndex >= 0;
            }

            bool RaycastCollidable(int index, float2 rayOrigin, float2 reciprocalHeading, out float distance)
            {
                // TODO: Determine which side the ray hit!
                
                // Implementation from: https://tavianator.com/fast-branchless-raybounding-box-intersections-part-2-nans/
                float2 offset = new float2(this.Collidable[index].Scale, this.Collidable[index].Scale);
                float2 lowerLeft = this.CollidablePosition[index].Value - offset;
                float2 upperRight = this.CollidablePosition[index].Value + offset;
                
                float left = (lowerLeft.x - rayOrigin.x) * reciprocalHeading.x;
                float right = (upperRight.x - rayOrigin.x) * reciprocalHeading.x;

                float tMin = math.min(left, right);
                float tMax = math.max(left, right);
                
                float bottom = (lowerLeft.y - rayOrigin.y) * reciprocalHeading.y;
                float top = (upperRight.y - rayOrigin.y) * reciprocalHeading.y;
                
                // Extra min/max for NaN handling!
                tMin = math.max(tMin, math.min(math.min(bottom, top), tMax));
                tMax = math.min(tMax, math.max(math.max(bottom, top), tMin));

                distance = tMin;

                return tMax > math.max(tMin, 0f);
            }

            float2 CalculateNormal(HitInfo hitInfo, float2 center)
            {
                // TODO: If hitEntity has a RoundedCornerData and the hit point is within RoundedCornerThreshold, use a sphere normal,
                // i.e. draw a vector from the center to the hit point and use that as a normal.

                // TODO: Otherwise, determine the normal based on the box side index, i.e. normal for a collision with the bottom side is (0, -1).

                return default (float2);
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