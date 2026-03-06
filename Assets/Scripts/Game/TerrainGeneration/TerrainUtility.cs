using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BenScr.MinecraftClone
{
    public static class TerrainUtility
    {
        public static void SetBlock(Vector3 position, int blockId)
        {
            Chunk chunk = ChunkUtility.GetChunkAtPosition(position);

            if (chunk != null)
            {
                chunk.SetBlock(position - chunk.position, blockId);
            }
            else
            {
                Debug.LogWarning("Position is outside of world: " + position);
            }
        }

        public static void DestroyBlock(Vector3 position)
        {
            Chunk chunk = ChunkUtility.GetChunkAtPosition(position);
            Vector3 localPosition = position - chunk.position;
            DestroyDamageTexture(localPosition, chunk);
        }

        public static void DamageBlock(Vector3 position, int damage)
        {
            Chunk chunk = ChunkUtility.GetChunkAtPosition(position);

            Vector3 localPosition = position - chunk.position;
            BlockData hitBlock = chunk.GetBlock(localPosition);

            ByteVector3 key = new ByteVector3((byte)localPosition.x, (byte)localPosition.y, (byte)localPosition.z);

            bool destroyed = false;

            if (!chunk.damagedBlocks.ContainsKey(key))
            {
                if ((hitBlock.durability - damage) <= 1)
                {
                    destroyed = true;
                    chunk.SetBlock(localPosition, Chunk.BLOCK_AIR, true);
                }
                else
                {
                    GameObject obj = GameObject.Instantiate(AssetsContainer.instance.damageStagePrefab, position + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
                    DamagedBlock damagedBlock = new DamagedBlock(hitBlock.durability - damage, obj);
                    chunk.damagedBlocks.Add(key, damagedBlock);

                    UpdateDamageTexture(hitBlock.durability, damagedBlock);
                }
            }
            else
            {
                DamagedBlock damagedBlock = chunk.damagedBlocks[key];
                --damagedBlock.health;

                if (damagedBlock.health <= 0)
                {
                    DestroyDamageTexture(localPosition, chunk);
                    destroyed = true;
                }
                else
                {
                    UpdateDamageTexture(hitBlock.durability, damagedBlock);
                }
            }

            if (destroyed && hitBlock.destroyEffect)
            {
                GameObject.Destroy(GameObject.Instantiate(hitBlock.destroyEffect, position + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity), 1.0f);
            }
        }

        private static void DestroyDamageTexture(Vector3 localPosition, Chunk chunk)
        {
            chunk.SetBlock(localPosition, Chunk.BLOCK_AIR, true);
            ByteVector3 key = (ByteVector3)localPosition;

            if (chunk.damagedBlocks.TryGetValue(key, out DamagedBlock damagedBlock))
            {
                GameObject.Destroy(damagedBlock.damageStage);
                chunk.damagedBlocks.Remove(key);
            }
        }

        private static void UpdateDamageTexture(int durability, DamagedBlock damagedBlock)
        {
            int stagesLength = AssetsContainer.instance.damageStages.Length;

            int health = Math.Clamp(damagedBlock.health, 0, durability);
            int damaged = durability - health;

            int stageIndex = (damaged * stagesLength) / (durability + 1);
            stageIndex = Math.Clamp(stageIndex, 0, stagesLength - 1);


            damagedBlock.damageStage.GetComponent<MeshRenderer>().material.mainTexture = AssetsContainer.instance.damageStages[stageIndex].texture;
        }
    }
}
