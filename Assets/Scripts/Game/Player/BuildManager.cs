using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace BenScr.MinecraftClone
{
    public class BuildManager : MonoBehaviour
    {
        private static readonly Vector3 halfExtents = new Vector3(0.499f, 0.499f, 0.499f);

        [SerializeField] private BlockSelectionManager blockSelectionManager;
        [SerializeField] private float maxInteractionDistance = 5;
        [SerializeField] private GameObject highlightBlock;
        [SerializeField] private GameObject damageStagePrefab;
        [SerializeField] private float breakBlockCooldown = 0.1f;
        [SerializeField] private float placeBlockCooldown = 0.1f;

        private Vector3 highlightPosition;
        private Vector3 placeBlockPosition;
        private bool isHighlightBlockVisible = false;


        private float breakBlockTimer = 0f;
        private float placeBlockTimer = 0f;


        void Update()
        {
            if (!PlayerController.instance) return;

            if (PlayerController.instance.isSpectator)
            {
                breakBlockTimer = 0;
                placeBlockTimer = 0;

                highlightBlock.SetActive(false);
                return;
            }

            breakBlockTimer += Time.deltaTime;
            placeBlockTimer += Time.deltaTime;

            if (isHighlightBlockVisible)
            {
                if ((PlayerController.instance.isFlying ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0)) && breakBlockTimer > breakBlockCooldown)
                {
                    breakBlockTimer = 0f;

                    if (PlayerController.instance.isFlying)
                        TerrainUtility.DestroyBlock(highlightPosition);
                    else
                        TerrainUtility.DamageBlock(highlightPosition, 1);
                }

                if (Input.GetMouseButton(1) && placeBlockTimer > placeBlockCooldown)
                {
                    placeBlockTimer = 0f;
                    Vector3 center = placeBlockPosition + new Vector3(0.5f, 0.5f, 0.5f);
                    bool overlapsWithPlayer = Physics.CheckBox(center, halfExtents, Quaternion.identity, LayerMask.GetMask("Player"));

                    if (!overlapsWithPlayer)
                        TerrainUtility.SetBlock(placeBlockPosition, blockSelectionManager.selectedBlock.id);
                }
            }

            UpdateHighlightBlock();
        }

        private void UpdateHighlightBlock()
        {
            Transform cam = Camera.main.transform;

            Vector3 origin = cam.position;
            Vector3 dir = cam.forward.normalized;

            isHighlightBlockVisible = false;

            Vector3Int current = new Vector3Int(
                Mathf.FloorToInt(origin.x),
                Mathf.FloorToInt(origin.y),
                Mathf.FloorToInt(origin.z)
            );

            int stepX = dir.x >= 0 ? 1 : -1;
            int stepY = dir.y >= 0 ? 1 : -1;
            int stepZ = dir.z >= 0 ? 1 : -1;

            float tDeltaX = dir.x == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.x);
            float tDeltaY = dir.y == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.y);
            float tDeltaZ = dir.z == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.z);

            float nextBoundaryX = stepX > 0 ? current.x + 1f : current.x;
            float nextBoundaryY = stepY > 0 ? current.y + 1f : current.y;
            float nextBoundaryZ = stepZ > 0 ? current.z + 1f : current.z;

            float tMaxX = dir.x == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryX - origin.x) / dir.x);
            float tMaxY = dir.y == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryY - origin.y) / dir.y);
            float tMaxZ = dir.z == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryZ - origin.z) / dir.z);

            Vector3Int previous = current;
            Vector3Int hitNormal = Vector3Int.zero;

            float traveled = 0f;

            while (traveled <= maxInteractionDistance)
            {
                int blockID = ChunkUtility.GetBlockAtPosition(current);

                if (blockID != Chunk.BLOCK_AIR && blockID != Chunk.BLOCK_WATER)
                {
                    highlightPosition = current;
                    placeBlockPosition = current + hitNormal;

                    highlightBlock.transform.position = (Vector3)current + Vector3.one * 0.5f;
                    isHighlightBlockVisible = true;

                    if (!highlightBlock.activeSelf)
                        highlightBlock.SetActive(true);

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        Chunk chunk = ChunkUtility.GetChunkAtPosition(current);
                        Debug.Log("Highlighted block: " + AssetsContainer.GetBlock(blockID).name);
                        Debug.Log("In Chunk at position " + chunk.coordinate + " AirOnly:" + chunk.isAirOnly
                            + " HighestGroundlevel:" + chunk.highestGroundLevel + " LowestGroundlevel:"
                            + chunk.lowestGroundLevel + " IsTop:" + chunk.IsTop
                            + " IsGenerated:" + chunk.isGenerated);
                    }

                    return;
                }

                previous = current;

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        current.x += stepX;
                        traveled = tMaxX;
                        tMaxX += tDeltaX;
                        hitNormal = new Vector3Int(-stepX, 0, 0);
                    }
                    else
                    {
                        current.z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                        hitNormal = new Vector3Int(0, 0, -stepZ);
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        current.y += stepY;
                        traveled = tMaxY;
                        tMaxY += tDeltaY;
                        hitNormal = new Vector3Int(0, -stepY, 0);
                    }
                    else
                    {
                        current.z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                        hitNormal = new Vector3Int(0, 0, -stepZ);
                    }
                }
            }

            if (highlightBlock.activeSelf)
                highlightBlock.SetActive(false);
        }
    }
}