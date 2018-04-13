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

            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public SharedComponentDataArray<Collidable> Collidable;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
        }

        [Inject] CollisionTargets collisionTargets;
        
        [Inject] ComponentDataFromEntity<RoundedCornerData> roundedCorners;

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
                RoundedCorners = this.roundedCorners,
                SquaredRoundedCornerThreshold = ROUNDED_CORNER_THRESHOLD * ROUNDED_CORNER_THRESHOLD,
                SpearcastData = this.spearcasters.SpearcastData,
                SpearcasterPosition = this.spearcasters.Position,
                SpearcasterHeading = this.spearcasters.Heading,
                CollidableEntities = this.collisionTargets.Entities,
                Collidable = this.collisionTargets.Collidable,
                CollidablePosition = this.collisionTargets.Position,
            }.Schedule (this.spearcasters.Length, 1, inputDeps);

            return collisionJob;
        }

        [ComputeJobOptimization]
        struct CollisionJob : IJobParallelFor
        {
            [ReadOnly] public float SquaredRoundedCornerThreshold;

            public NativeArray<float> Distance;
            
            public ComponentDataFromEntity<RoundedCornerData> RoundedCorners;

            [ReadOnly] public SharedComponentDataArray<SpearcastData> SpearcastData;
            public ComponentDataArray<Position2D> SpearcasterPosition;
            public ComponentDataArray<Heading2D> SpearcasterHeading;

            [ReadOnly] public EntityArray CollidableEntities;
            [ReadOnly] public SharedComponentDataArray<Collidable> Collidable;
            [ReadOnly] public ComponentDataArray<Position2D> CollidablePosition;

            struct HitInfo
            {
                public float distanceToTarget;

                public Entity hitEntity;
                public float2 hitPoint;

                public void SetDefaults()
                {
                    this.distanceToTarget = Mathf.Infinity;
                    this.hitEntity = Entity.Null;
                    this.hitPoint = default (float2);
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
                    if (bestHitInfo.distanceToTarget <= remainingDistance && bestHitInfo.hitEntity != Entity.Null) // We hit something!
                    {
                        distanceToMove = bestHitInfo.distanceToTarget;

                        float2 normal = this.CalculateNormal (index, bestHitInfo, center);
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
                            hitInfo.hitEntity = this.CollidableEntities[i];
                        }
                    }
                }

                hitInfo.hitPoint = rayOrigin + hitInfo.distanceToTarget / reciprocalHeading;

                return hitInfo.hitEntity != Entity.Null;
            }

            bool RaycastCollidable(int index, float2 rayOrigin, float2 reciprocalHeading, out float distance)
            {
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

            float2 CalculateNormal(int index, HitInfo hitInfo, float2 center)
            {
                float2 direction = hitInfo.hitPoint - center;
                if (this.IsHitPointOnRoundedCorner(index, hitInfo, center))
                {
                    return math.normalize(direction);
                }
                else
                {
                    return math.select(new float2(1, 0) * math.sign(direction.x), new float2(0, 1) * math.sign(direction.y),
                        math.abs(direction.x) >= math.abs(direction.y));
                }
            }

            bool IsHitPointOnRoundedCorner(int index, HitInfo hitInfo, float2 center)
            {
                if (!this.RoundedCorners.Exists(hitInfo.hitEntity))
                    return false;

                RoundedCornerData roundedCornerData = this.RoundedCorners[hitInfo.hitEntity];
                float scale = this.Collidable[index].Scale;

                for (int i = 0; i < roundedCornerData.Corners.Length; i++)
                {
                    float2 corner = new float2(center.x + ((i / 2 == 0) ? -scale : scale), center.y + ((i % 3 == 0) ? -scale : scale));
                    if (math.lengthSquared(corner - hitInfo.hitPoint) <= this.SquaredRoundedCornerThreshold)
                        return true;
                }

                return false;
            }
        }
    }
}