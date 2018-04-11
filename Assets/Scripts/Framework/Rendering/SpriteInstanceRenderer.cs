using System;
using Unity.Entities;
using UnityEngine;

namespace Framework.Rendering
{
    [Serializable]
    public struct SpriteInstanceRenderer : ISharedComponentData
    {
        public Texture2D sprite;
        public Material material;
    }
    public class SpriteInstanceRendererComponent : SharedComponentDataWrapper<SpriteInstanceRenderer> { }
}