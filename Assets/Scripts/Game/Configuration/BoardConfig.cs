using Unity.Mathematics;

using UnityEngine;

namespace Ricochet.Configuration
{
    [CreateAssetMenu(fileName = "New Board Configuration", menuName = "Configuration/Board Configuration")]
    public class BoardConfig : ScriptableObject
    {
        public int2 BoardDimensions;

        public float BulletSpeed;

        public float RoundedCornerThreshold;
    }
}