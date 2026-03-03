using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace BenScr.MinecraftClone
{
    public class BlockSelectionManager : MonoBehaviour
    {
        public Block selectedBlock;

        [SerializeField] private Image prevSelectedBlockPreview;
        [SerializeField] private Image selectedBlockPreview;
        [SerializeField] private Image nextSelectedBlockPreview;
        [SerializeField] private Image nextSelectedBlockPreviewAnim;
        [SerializeField] private Animator animator;
        public float dur = 0.25f;
        private bool isAnimating = false;

        void Start()
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
    }
}
