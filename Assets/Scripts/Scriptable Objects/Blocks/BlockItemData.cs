using BenScr.MinecraftClone;
using UnityEngine;

namespace BenScr.MinecraftClone
{
    [CreateAssetMenu(fileName = "BlockItemData", menuName = "Scriptable Objects/Items/BlockItemData")]
    public class BlockItemData : ItemData
    {
        public BlockData block;
    }
}