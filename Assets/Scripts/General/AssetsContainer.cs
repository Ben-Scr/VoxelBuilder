using UnityEngine;

namespace BenScr.MinecraftClone
{
    public class AssetsContainer : MonoBehaviour
    {
        public BlockData[] blocks;

        public Material blockMaterial;
        public Sprite[] damageStages;

        public Material fluidMaterial;
        public Material transparentMaterial;
        public GameObject damageStagePrefab;

        public static AssetsContainer instance;

        private void Awake()
        {
            instance = this;

            InitBlocks();
        }


        private void InitBlocks()
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].id = (ushort)i;
                blocks[i].RebuildFaceTextureCache();
            }
        }

        public static BlockData GetBlock(int id)
        {
            if (id < 0 || id >= instance.blocks.Length)
            {
                Debug.LogWarning("Block ID out of range: " + id);
                return null;
            }

            return instance.blocks[id];
        }
    }
}