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

                    if(PlayerController.instance.isFlying)
                    TerrainGenerator.instance.SetBlock(highlightPosition, Chunk.BLOCK_AIR);
                    else
                        TerrainGenerator.instance.DamageBlock(highlightPosition);
                }

                if (Input.GetMouseButton(1) && placeBlockTimer > placeBlockCooldown)
                {
                    placeBlockTimer = 0f;
                    Vector3 center = placeBlockPosition + new Vector3(0.5f, 0.5f, 0.5f);
                    bool overlapsWithPlayer = Physics.CheckBox(center, halfExtents, Quaternion.identity, LayerMask.GetMask("Player"));

                    if (!overlapsWithPlayer)
                    {
                        TerrainGenerator.instance.SetBlock(placeBlockPosition, blockSelectionManager.selectedBlock.id);
                    }
                }
            }

            UpdateHighlightBlock();
        }

        private void UpdateHighlightBlock()
        {
            float distance = 0;

            isHighlightBlockVisible = false;
            highlightBlock.SetActive(false);

            Vector3 lastPosition = Vector3.zero;

            while (distance < maxInteractionDistance)
            {
                Vector3 position = Camera.main.transform.position +
                    Camera.main.transform.forward * distance;

                highlightPosition = new Vector3(
                       Mathf.FloorToInt(position.x),
                       Mathf.FloorToInt(position.y),
                       Mathf.FloorToInt(position.z)
                       );

                int blockID = ChunkUtility.GetBlockAtPosition(highlightPosition);
                if (blockID != Chunk.BLOCK_AIR && blockID != Chunk.BLOCK_WATER)
                {
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        Chunk chunk = ChunkUtility.GetChunkByPosition(highlightPosition);
                        Debug.Log("Highlighted block: " + AssetsContainer.GetBlock(blockID).name);
                        Debug.Log("In Chunk at position " + chunk.coordinate + " AirOnly:" + chunk.isAirOnly
                            + " HighestGroundlevel:" + chunk.highestGroundLevel + " LowestGroundlevel:"
                            + chunk.lowestGroundLevel + " IsTop:" + chunk.IsTop
                            + " IsGenerated:" + chunk.isGenerated);
                    }

                    highlightBlock.transform.position = highlightPosition + new Vector3(0.5f, 0.5f, 0.5f);

                    isHighlightBlockVisible = true;
                    highlightBlock.SetActive(true);

                    placeBlockPosition = lastPosition;
                    break;
                }

                lastPosition = highlightPosition;
                distance += 0.1f;
            }
        }
    }
}