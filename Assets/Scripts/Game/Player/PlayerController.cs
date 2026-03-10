using System;
using UnityEngine;
using static BenScr.MinecraftClone.SettingsContainer;

namespace BenScr.MinecraftClone
{
    public enum GameMode
    {
        Survival = 0,
        Exploration = 1,
        Creative = 2,
        Spectator = 3,
    }
    public enum MovementMode
    {
        Default,
        Flying
    }

    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public GameMode gameMode = GameMode.Survival;

        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float jumpForce = 5f;
        private bool isGrounded;

        [Header("Camera")]
        [SerializeField] private float cameraSensitivity = 2f;
        [SerializeField] private float cameraLockMin = -60f;
        [SerializeField] private float cameraLockMax = 60f;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerMeshTr;

        [Header("Flying Mode")]
        [SerializeField] private float doubleSpaceThreshold = 0.2f;
        [SerializeField] private float maxFlySpeedMultiplier = 10f;
        [SerializeField] private float flySpeed = 10f;
        [SerializeField] private float flyAcceleration = 5f;

        private float curFlySpeedMultiplier = 1;
        public bool isFlying;


        [Header("Physics")]
        [SerializeField] private float maxVelocityY = 50f;
        [SerializeField] private float minVelocityY = -50f;
        [SerializeField] private Vector3 groundedSize;
        [SerializeField] private Vector3 groundedOffset;

        [Header("Fluid Movement")]
        [SerializeField] private float swimSpeed = 2.75f;
        [SerializeField] private float swimVerticalSpeed = 2f;
        [SerializeField] private float swimBuoyancy = 0.6f;
        [SerializeField] private float swimLerpSpeed = 5f;
        [SerializeField] private float swimDrag = 3f;
        [SerializeField] private float swimAngularDrag = 1.5f;

        [SerializeField] private float gravity;
        private UnderwaterPostEffect underwaterEffect;
        internal bool isHeadInFluid;
        private bool isInFluid;
        internal BlockData currentFluidBlock;
        private float defaultDrag;
        private float defaultAngularDrag;

        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;

        private float inputSpace = 0;
        public static PlayerController instance;

        public static Action<GameMode> OnSwitchGameMode;

        private static readonly Vector3[] fluidCheckDirections = new Vector3[]
        {
            Vector3.zero,
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back
        };

        private void Awake()
        {
            instance = this;

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();

            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponentInChildren<CapsuleCollider>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
            {
                playerCamera.depthTextureMode |= DepthTextureMode.Depth;

                if (!playerCamera.TryGetComponent(out underwaterEffect))
                {
                    underwaterEffect = playerCamera.gameObject.AddComponent<UnderwaterPostEffect>();
                }

                underwaterEffect.Initialize(this);
            }

            if (rb != null)
            {
                defaultDrag = rb.linearDamping;
                defaultAngularDrag = rb.angularDamping;
            }
        }

        private void OnEnable()
        {
            GameController.OnFreeze += OnPlayerFreeze;
            GameController.OnUnFreeze += OnPlayerUnFreeze;
        }
        private void OnDisable()
        {
            GameController.OnFreeze -= OnPlayerFreeze;
            GameController.OnUnFreeze -= OnPlayerUnFreeze;
        }

        private void OnPlayerFreeze(FreezeReason freezeReason)
        {
            if (GameController.IsPlayerFrozen)
                rb.constraints = RigidbodyConstraints.FreezeAll;

        }
        private void OnPlayerUnFreeze(FreezeReason freezeReason)
        {

            if (!GameController.IsPlayerFrozen)
                rb.constraints = isFlying && gameMode == GameMode.Spectator ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.FreezeRotation;
            ;
        }

        public void Update()
        {
            if (GameController.IsPlayerFrozen) return;

            isGrounded = IsGrounded();
            inputSpace += Time.deltaTime;

            UpdateFluidState();
            Movement();
            Rotation();
        }

        private void Rotation()
        {
            Vector3 eulerAnglesY = playerMeshTr.eulerAngles;
            Vector3 eulerAnglesX = playerCamera.transform.eulerAngles;

            eulerAnglesY.y += Input.GetAxis("Mouse X") * cameraSensitivity;
            eulerAnglesX.x -= Input.GetAxis("Mouse Y") * cameraSensitivity;

            playerMeshTr.rotation = Quaternion.Euler(eulerAnglesY);
            playerCamera.transform.rotation = Quaternion.Euler
                (
                Mathf.Clamp(eulerAnglesX.x > 180 ? eulerAnglesX.x - 360 : eulerAnglesX.x, cameraLockMin, cameraLockMax),
                playerMeshTr.eulerAngles.y,
                eulerAnglesX.z
                );
        }

        private void Movement()
        {
            Vector3 input = GetInput();


            if (!isGrounded && !isInFluid)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y - gravity * Time.deltaTime);
            }

            if (gameMode != GameMode.Spectator)
            {
                if (isInFluid && !isFlying)
                {
                    Vector3 currentVelocity = rb.linearVelocity;
                    Vector3 targetVelocity = new Vector3(input.x, input.y, input.z);
                    Vector3 blendedVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * swimLerpSpeed);
                    blendedVelocity.y = Mathf.Clamp(blendedVelocity.y, -swimVerticalSpeed, swimVerticalSpeed);
                    rb.linearVelocity = blendedVelocity;
                }
                else
                {
                    float velocityY = Mathf.Clamp(isFlying ? input.y : rb.linearVelocity.y, minVelocityY, maxVelocityY);
                    input.y = velocityY;
                    rb.linearVelocity = input;
                }
            }
            else
            {
                transform.position += input * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.Space) && !isInFluid && isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                Jump();
            }

            if (Input.GetKeyDown(KeyCode.Space) && gameMode == GameMode.Creative)
            {
                if (gameMode != GameMode.Spectator && inputSpace < doubleSpaceThreshold)
                {
                    SetFlyingMode();
                }
                else
                {
                    inputSpace = 0;
                }
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                SetSpectatorMode();
            }


            if (!isFlying && !isInFluid && isGrounded && rb.linearVelocity.magnitude < 0.1f)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        public void SetFlyingMode()
        {
            isFlying = !isFlying;

            if (isFlying)
            {
                rb.linearVelocity = new Vector3(0, 0, 0);
                curFlySpeedMultiplier = 1;
                rb.useGravity = false;
            }
            else
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.useGravity = !isInFluid;
            }
        }

        public void SetSpectatorMode()
        {
            isFlying = gameMode == GameMode.Spectator ? false : true;

            capsuleCollider.enabled = !isFlying;
            rb.constraints = isFlying ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.FreezeRotation;

            gameMode = gameMode == GameMode.Spectator ? GameMode.Survival : GameMode.Spectator;

            OnSwitchGameMode?.Invoke(gameMode);
        }

        public void Jump()
        {
            if (isInFluid && !isFlying)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, swimVerticalSpeed, rb.linearVelocity.z);
            }
            else
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            }
        }

        public bool IsGrounded()
        {
            return Physics.CheckBox(transform.position + groundedOffset, groundedSize / 2f, Quaternion.identity, ~LayerMask.GetMask("Player"));
        }

        public Vector3 GetInput()
        {
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            bool isCrouching = Input.GetKey(KeyCode.LeftControl);

            Vector3 moveInput = Input.GetAxis("Vertical") * playerMeshTr.forward + Input.GetAxis("Horizontal") * playerMeshTr.right;
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            if (isInFluid && !isFlying)
            {
                Vector3 velocity = moveInput * swimSpeed;

                bool ascend = Input.GetKey(KeyCode.Space);
                bool descend = Input.GetKey(KeyCode.LeftControl);

                float vertical = 0f;
                if (ascend)
                {
                    vertical += swimVerticalSpeed;
                }
                if (descend)
                {
                    vertical -= swimVerticalSpeed;
                }
                if (!ascend && !descend)
                {
                    vertical = swimBuoyancy;
                }

                velocity.y = Mathf.Clamp(vertical, -swimVerticalSpeed, swimVerticalSpeed);
                return velocity;
            }

            float speed = 0f;

            if (isFlying)
            {
                if (Input.GetKey(KeyCode.Space))
                    moveInput.y += 1;
                if (Input.GetKey(KeyCode.LeftControl))
                    moveInput.y -= 1;

                if (Input.GetKey(KeyCode.LeftShift))
                    curFlySpeedMultiplier = Mathf.Lerp(curFlySpeedMultiplier, maxFlySpeedMultiplier, Time.deltaTime * flyAcceleration);
                else if (moveInput == Vector3.zero)
                    curFlySpeedMultiplier = Mathf.Lerp(curFlySpeedMultiplier, 1, Time.deltaTime * flyAcceleration);

                speed = flySpeed * curFlySpeedMultiplier;
                moveInput *= speed;
                return moveInput;
            }

            speed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);

            moveInput *= speed;
            return moveInput;
        }

        private void UpdateFluidState()
        {
            if (rb == null || capsuleCollider == null)
                return;


            bool wasInFluid = isInFluid;
            isInFluid = TryGetFluidBlock(out currentFluidBlock);
            isHeadInFluid = CheckHeadInFluid();

            if (isFlying)
            {
                return;
            }

            if (isInFluid)
            {
                rb.useGravity = false;
                rb.linearDamping = swimDrag;
                rb.angularDamping = swimAngularDrag;
            }
            else if (wasInFluid)
            {
                ExitFluid();
            }
        }

        private void ExitFluid()
        {
            rb.useGravity = !isFlying;
            rb.linearDamping = defaultDrag;
            rb.angularDamping = defaultAngularDrag;
            currentFluidBlock = null;
            isInFluid = false;
            isHeadInFluid = false;
        }

        private bool CheckHeadInFluid()
        {
            if (playerCamera != null)
            {
                return IsPositionInFluid(playerCamera.transform.position);
            }

            if (capsuleCollider != null)
            {
                Bounds bounds = capsuleCollider.bounds;
                Vector3 headPosition = bounds.center + Vector3.up * bounds.extents.y;
                return IsPositionInFluid(headPosition);
            }

            return false;
        }

        private static bool IsPositionInFluid(Vector3 position)
        {
            int blockId = ChunkUtility.GetBlockAtPosition(position);

            if (blockId == Chunk.BLOCK_AIR)
            {
                return false;
            }

            BlockData block = AssetsContainer.GetBlock(blockId);
            return block != null && block.isFluid;
        }


        private bool TryGetFluidBlock(out BlockData fluidBlock)
        {
            fluidBlock = null;

            if (AssetsContainer.instance == null)
            {
                return false;
            }

            if (capsuleCollider == null)
            {
                return false;
            }

            Bounds bounds = capsuleCollider.bounds;
            Vector3 center = bounds.center;
            float horizontalExtent = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f;
            float verticalExtent = bounds.extents.y * 0.9f;

            for (int i = 0; i < fluidCheckDirections.Length; i++)
            {
                Vector3 dir = fluidCheckDirections[i];
                Vector3 offset = new Vector3(dir.x * horizontalExtent, dir.y * verticalExtent, dir.z * horizontalExtent);
                Vector3 samplePoint = center + offset;

                int blockId = ChunkUtility.GetBlockAtPosition(samplePoint);

                if (blockId == Chunk.BLOCK_AIR)
                {
                    continue;
                }

                BlockData block = AssetsContainer.GetBlock(blockId);

                if (block != null && block.isFluid)
                {
                    fluidBlock = block;
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (!Settings?.DebugGizmos ?? false) return;

            Gizmos.DrawWireCube(transform.position + groundedOffset, groundedSize / 2f);
        }
    }
}