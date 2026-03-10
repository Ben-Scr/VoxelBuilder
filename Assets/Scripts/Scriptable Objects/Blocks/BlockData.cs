using System;
using UnityEngine;

namespace BenScr.MinecraftClone
{

    [CreateAssetMenu(fileName = "BlockData", menuName = "Scriptable Objects/Blocks/Block")]
    public class BlockData : ScriptableObject
    {
        public enum BlockFace
        {
            Back = 0,
            Front = 1,
            Top = 2,
            Bottom = 3,
            Left = 4,
            Right = 5,
        }

        [Serializable]
        public struct FaceTextureData
        {
            public Sprite texture;

            [HideInInspector] public Vector2 uvMin;
            [HideInInspector] public Vector2 uvMax;

            public void RebuildUvCache()
            {
                if (texture == null || texture.texture == null)
                {
                    uvMin = Vector2.zero;
                    uvMax = Vector2.one;
                    return;
                }

                Rect rect = texture.textureRect;
                Texture2D atlasTexture = texture.texture;

                uvMin = new Vector2(rect.xMin / atlasTexture.width, rect.yMin / atlasTexture.height);
                uvMax = new Vector2(rect.xMax / atlasTexture.width, rect.yMax / atlasTexture.height);
            }

            public readonly bool Matches(in FaceTextureData other)
            {
                return uvMin == other.uvMin && uvMax == other.uvMax;
            }
        }

        internal ushort id;
        public int durability = 5;
        public bool isTransparent;
        public bool isFluid;

        public ItemData itemData;

        public GameObject destroyEffect;

        [Serializable]
        private struct FaceTextureSet
        {
            public FaceTextureData back;
            public FaceTextureData front;
            public FaceTextureData top;
            public FaceTextureData bottom;
            public FaceTextureData left;
            public FaceTextureData right;

            public FaceTextureData Get(int face)
            {
                return (BlockFace)face switch
                {
                    BlockFace.Back => back,
                    BlockFace.Front => front,
                    BlockFace.Top => top,
                    BlockFace.Bottom => bottom,
                    BlockFace.Left => left,
                    BlockFace.Right => right,
                    _ => back,
                };
            }

            public void Set(int face, FaceTextureData data)
            {
                switch ((BlockFace)face)
                {
                    case BlockFace.Back:
                        back = data;
                        break;
                    case BlockFace.Front:
                        front = data;
                        break;
                    case BlockFace.Top:
                        top = data;
                        break;
                    case BlockFace.Bottom:
                        bottom = data;
                        break;
                    case BlockFace.Left:
                        left = data;
                        break;
                    case BlockFace.Right:
                        right = data;
                        break;
                }
            }
        }

        [SerializeField] private FaceTextureSet faceTextures;

        public FaceTextureData GetTexture(int face)
        {
            if (face < 0 || face > (int)BlockFace.Right)
            {
                Debug.LogWarning("Invalid face: " + face);
                return faceTextures.Get((int)BlockFace.Back);
            }

            return faceTextures.Get(face);
        }

        public void RebuildFaceTextureCache()
        {
            for (int i = 0; i <= (int)BlockFace.Right; i++)
            {
                FaceTextureData faceTexture = faceTextures.Get(i);
                faceTexture.RebuildUvCache();
                faceTextures.Set(i, faceTexture);
            }
        }

        private void OnValidate()
        {
            RebuildFaceTextureCache();
        }
    }
}