using System;
using Unity.Mathematics;

namespace Unity.Entities
{
    public static class MathematicsExtensions
    {
        public static bool GetAtIndex(this bool4 value, int index)
        {
            if (index == 0)
                return value.x;
            if (index == 1)
                return value.y;
            if (index == 2)
                return value.z;
            if (index == 3)
                return value.w;

            throw new ArgumentOutOfRangeException();
        }
    }
}