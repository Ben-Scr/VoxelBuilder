using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace BenScr.MinecraftClone
{
    public class PlayerManager : MonoBehaviour
    {
        private static readonly Vector3 halfExtents = new Vector3(0.499f, 0.499f, 0.499f);

        [SerializeField] private float maxInteractionDistance = 5;
        [SerializeField] private GameObject highlightBlock;
        [SerializeField] private float breakBlockCooldown = 0.1f;
        [SerializeField] private float placeBlockCooldown = 0.1f;
        [SerializeField] private Block selectedBlock;

        [SerializeField] private Image prevSelectedBlockPreview;
        [SerializeField] private Image selectedBlockPreview;
        [SerializeField] private Image nextSelectedBlockPreview;
        [SerializeField] private Image nextSelectedBlockPreviewAnim;
        [SerializeField] private Animator animator;

        private Vector3 highlightPosition;
        private Vector3 placeBlockPosition;
        private bool isHighlightBlockVisible = false;
        public float dur = 15;
        private bool isAnimating = false;

        private float breakBlockTimer = 0f;
        private float placeBlockTimer = 0f;

        private void Awake()
        {
            UpdateBlocksUI();
        }

        private void UpdateBlocksUI()
        {
            prevSelectedBlockPreview.sprite = GetNextBlock().preview;
            nextSelectedBlockPreview.sprite = GetPrevBlock().preview;
            selectedBlockPreview.sprite = selectedBlock.preview;
        }

        void Update()
        {
            if (PlayerController.instance.isSpectator)
            {
                breakBlockTimer = 0;
                placeBlockTimer = 0;

                highlightBlock.SetActive(false);
                return;
            }

            breakBlockTimer += Time.deltaTime;
            placeBlockTimer += Time.deltaTime;

            if (Input.mouseScrollDelta.y >= 1 && !isAnimating)
            {
                animator.SetTrigger("Next");
                isAnimating = true;

                nextSelectedBlockPreviewAnim.sprite = GetNextBlock(2).preview;

                StartCoroutine(DoAfterDelay(dur, () =>
                {
                    selectedBlock = GetNextBlock();
                    UpdateBlocksUI();
                    isAnimating = false;
                }));
            }
            else if (Input.mouseScrollDelta.y == -1 && !isAnimating)
            {
                animator.SetTrigger("Previous");
                isAnimating = true;

                nextSelectedBlockPreviewAnim.sprite = GetPrevBlock(2).preview;

                StartCoroutine(DoAfterDelay(dur, () =>
                {
                    selectedBlock = GetPrevBlock();
                    UpdateBlocksUI();
                    isAnimating = false;
                }));
            }

            if (isHighlightBlockVisible)
            {
                if (Input.GetMouseButton(0) && breakBlockTimer > breakBlockCooldown)
                {
                    breakBlockTimer = 0f;
                    TerrainGenerator.instance.SetBlock(highlightPosition, Chunk.BLOCK_AIR);
                }

                if (Input.GetMouseButton(1) && placeBlockTimer > placeBlockCooldown)
                {
                    placeBlockTimer = 0f;
                    Vector3 center = placeBlockPosition + new Vector3(0.5f, 0.5f, 0.5f);
                    bool overlapsWithPlayer = Physics.CheckBox(center, halfExtents, Quaternion.identity, LayerMask.GetMask("Player"));

                    if (!overlapsWithPlayer)
                    {
                        TerrainGenerator.instance.SetBlock(placeBlockPosition, selectedBlock.id);
                    }
                }
            }

            UpdateHighlightBlock();
        }

        private IEnumerator DoAfterDelay(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }

        private Block GetPrevBlock(int backward = 1)
        {
            int blocksCount = AssetsContainer.instance.blocks.Length;

            int n = blocksCount - 1;
            int cur = selectedBlock.id - 1;

            int prev = (cur - backward % n + n) % n;
            int id = prev + 1;

            return AssetsContainer.GetBlock(id);
        }
        private Block GetNextBlock(int forward = 1)
        {
            int blocksCount = AssetsContainer.instance.blocks.Length;
            return AssetsContainer.GetBlock(math.clamp((selectedBlock.id + forward) % blocksCount, 1, blocksCount - 1));
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