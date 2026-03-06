using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace BenScr.MinecraftClone
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.UI;

    public class TerrainGenerator : MonoBehaviour
    {
        public GameObject chunkPrefab;
        public bool addColliders = false;
        public bool addTrees = true;

        [SerializeField] private int viewDistance = 5;
        [SerializeField] private int viewDistanceY = 2;
        [SerializeField] private bool shouldDisableChunks = false;

        [SerializeField] private int maxChunksCreatePerFrame = 2;
        [SerializeField] private int maxChunksGeneratePerFrame = 2;
        [SerializeField] private float addColliderDistance = 10f;
        [SerializeField] private PlayerController player;
        [SerializeField] private Image loadTerrainSlider;

        public static Action OnLoadedTerrain;
        public static TerrainGenerator instance;

        public static readonly Dictionary<Vector3Int, Chunk> chunks = new(2048);

        private readonly HashSet<Vector3Int> lastActiveChunks = new();
        private readonly HashSet<Vector3Int> currentActiveChunks = new();

        // Missing chunks that need Prepare()
        private readonly List<Vector3Int> chunksToCreate = new(512);
        private int createIndex;

        // Existing prepared chunks that still need Generate()
        private readonly List<Vector3Int> chunksToGenerate = new(512);
        private int generateIndex;

        // Prevent duplicates in work lists
        private readonly HashSet<Vector3Int> queuedForCreate = new();
        private readonly HashSet<Vector3Int> queuedForGenerate = new();

        private Transform playerTransform;
        private Vector3Int[] poses;

        private int viewDistanceXZSq;
        private int viewDistanceYSq;
        private float addColliderDistanceSq;

        private int lastViewDistance = -1;
        private int lastViewDistanceY = -1;

        private Vector3Int lastPlayerChunk = new(int.MinValue, int.MinValue, int.MinValue);
        private bool loadedTerrain;

        private void Awake()
        {
            instance = this;
            playerTransform = player.transform;

            chunks.Clear();
            UpdateViewDistance();
        }

        private void Start()
        {
            StartCoroutine(InitializeTerrain());
        }

        private IEnumerator InitializeTerrain()
        {
            yield return null;

            // poses are relative, already sorted nearest-first

            int count = 0;
            float chunksCount = poses.Length * 2.0f;

            for (int i = 0; i < poses.Length; i++)
            {
                Vector3Int pos = poses[i];

                var chunk = new Chunk(pos.x, pos.y, pos.z);
                chunk.Prepare();
                chunks.Add(chunk.coordinate, chunk);
                currentActiveChunks.Add(chunk.coordinate);

                if (++count % 10 == 0)
                {
                    yield return null;
                    loadTerrainSlider.fillAmount = count / chunksCount;
                }
            }

            yield return null;

            foreach (var chunk in chunks.Values)
            {
                chunk.Generate();

                if (++count % 5 == 0)
                {
                    yield return null;
                    loadTerrainSlider.fillAmount = count / chunksCount;
                }
            }

            yield return new WaitForSeconds(1.0f);

            Chunk highestChunk = ChunkUtility.GetHighestChunkAt(Vector3.zero);

            if (highestChunk != null)
            {
                int highestPosY = GetHighestBlockPositionYAt(0, (int)highestChunk.position.y, 0);
                playerTransform.position = new Vector3(0.5f, highestPosY + 2.0f, 0.5f);
            }
            else
            {
                Debug.LogWarning("Found no highest chunk for player");
            }

            lastActiveChunks.Clear();
            foreach (var pos in currentActiveChunks)
                lastActiveChunks.Add(pos);

            lastPlayerChunk = ChunkUtility.GetChunkCoordinateFromPosition(playerTransform.position);

            loadedTerrain = true;
            OnLoadedTerrain?.Invoke();
        }

        private int GetHighestBlockPositionYAt(int x, int y, int z)
        {
            int highest = y;

            for (int i = 0; i < Chunk.CHUNK_HEIGHT; i++)
            {
                int worldY = y + i;
                int blockId = ChunkUtility.GetBlockAtPosition(new Vector3Int(x, worldY, z));

                if (blockId != Chunk.BLOCK_AIR && worldY > highest)
                    highest = worldY;
            }

            return highest;
        }

        public void UpdateViewDistance()
        {
            viewDistanceXZSq = viewDistance * viewDistance;
            viewDistanceYSq = viewDistanceY * viewDistanceY;

            float maxColliderDistance = Chunk.CHUNK_SIZE + addColliderDistance;
            addColliderDistanceSq = maxColliderDistance * maxColliderDistance;

            GeneratePoses();

            lastViewDistance = viewDistance;
            lastViewDistanceY = viewDistanceY;

            lastPlayerChunk = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        private void GeneratePoses()
        {
            int rx = viewDistance;
            int ry = viewDistanceY;
            int rz = viewDistance;

            int cap = (2 * rx + 1) * (2 * ry + 1) * (2 * rz + 1);
            var tmp = new List<Vector3Int>(cap);

            for (int x = -rx; x <= rx; x++)
            {
                int xSq = x * x;

                for (int y = -ry; y <= ry; y++)
                {
                    int ySq = y * y;

                    for (int z = -rz; z <= rz; z++)
                    {
                        int xzSq = xSq + z * z;
                        if (xzSq > viewDistanceXZSq)
                            continue;

                        if (ySq > viewDistanceYSq)
                            continue;

                        tmp.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            // nearest-first
            tmp.Sort(static (a, b) =>
            {
                int aDist = a.x * a.x + a.y * a.y + a.z * a.z;
                int bDist = b.x * b.x + b.y * b.y + b.z * b.z;
                return aDist - bDist;
            });

            poses = tmp.ToArray();
        }

        private void Update()
        {
            if (!loadedTerrain)
                return;

            if (lastViewDistance != viewDistance || lastViewDistanceY != viewDistanceY)
                UpdateViewDistance();

            ChunkMeshGenerator.Update();

            Vector3 playerPosition = playerTransform.position;
            Vector3Int playerChunk = ChunkUtility.GetChunkCoordinateFromPosition(playerPosition);

            bool playerChunkChanged = playerChunk != lastPlayerChunk;
            bool hasPendingWork = createIndex < chunksToCreate.Count || generateIndex < chunksToGenerate.Count;

            if (!playerChunkChanged && !hasPendingWork)
                return;

            if (playerChunkChanged)
            {
                RebuildTargetChunkLists(playerChunk, playerPosition);

                if (shouldDisableChunks)
                    UpdateChunkVisibility(playerChunk);
            }
            else if (addColliders)
            {
                // still allow collider adds while work drains
                UpdateNearbyColliders(playerPosition, playerChunk);
            }

            ProcessChunkCreation();
            ProcessChunkGeneration();

            lastPlayerChunk = playerChunk;
        }

        private void RebuildTargetChunkLists(Vector3Int playerChunk, Vector3 playerPosition)
        {
            currentActiveChunks.Clear();

            chunksToCreate.Clear();
            chunksToGenerate.Clear();
            queuedForCreate.Clear();
            queuedForGenerate.Clear();
            createIndex = 0;
            generateIndex = 0;

            for (int i = 0; i < poses.Length; i++)
            {
                Vector3Int rel = poses[i];
                Vector3Int coordinate = new Vector3Int(
                    playerChunk.x + rel.x,
                    playerChunk.y + rel.y,
                    playerChunk.z + rel.z
                );

                currentActiveChunks.Add(coordinate);

                if (!chunks.TryGetValue(coordinate, out var chunk))
                {
                    if (queuedForCreate.Add(coordinate))
                        chunksToCreate.Add(coordinate);

                    continue;
                }

                if (!chunk.isGenerated)
                {
                    if (queuedForGenerate.Add(coordinate))
                        chunksToGenerate.Add(coordinate);
                }

                if (addColliders && chunk.meshCollider == null)
                {
                    float3 delta = chunk.position - playerPosition;
                    if (math.lengthsq(delta) <= addColliderDistanceSq)
                        chunk.AddMeshCollider();
                }
            }
        }

        private void UpdateNearbyColliders(Vector3 playerPosition, Vector3Int playerChunk)
        {
            for (int i = 0; i < poses.Length; i++)
            {
                Vector3Int rel = poses[i];
                Vector3Int coordinate = new Vector3Int(
                    playerChunk.x + rel.x,
                    playerChunk.y + rel.y,
                    playerChunk.z + rel.z
                );

                if (!chunks.TryGetValue(coordinate, out var chunk))
                    continue;

                if (chunk.meshCollider != null)
                    continue;

                float3 delta = chunk.position - playerPosition;
                if (math.lengthsq(delta) <= addColliderDistanceSq)
                    chunk.AddMeshCollider();
            }
        }

        private void ProcessChunkCreation()
        {
            int created = 0;

            while (createIndex < chunksToCreate.Count && created < maxChunksCreatePerFrame)
            {
                Vector3Int coordinate = chunksToCreate[createIndex++];
                queuedForCreate.Remove(coordinate);

                if (chunks.ContainsKey(coordinate))
                    continue;

                var chunk = new Chunk(coordinate.x, coordinate.y, coordinate.z);
                chunk.Prepare();
                chunks.Add(chunk.coordinate, chunk);

                if (!chunk.isGenerated && queuedForGenerate.Add(chunk.coordinate))
                    chunksToGenerate.Add(chunk.coordinate);

                created++;
            }

            // Compact list when fully consumed
            if (createIndex >= chunksToCreate.Count)
            {
                chunksToCreate.Clear();
                createIndex = 0;
            }
        }

        private void ProcessChunkGeneration()
        {
            int generated = 0;
            int inspected = 0;

            // Limit inspection so one blocked chunk doesn't cause a full scan every frame
            int maxInspect = math.min(chunksToGenerate.Count - generateIndex, maxChunksGeneratePerFrame * 8);

            while (generateIndex < chunksToGenerate.Count &&
                   generated < maxChunksGeneratePerFrame &&
                   inspected < maxInspect)
            {
                Vector3Int coordinate = chunksToGenerate[generateIndex];
                inspected++;

                if (!chunks.TryGetValue(coordinate, out var chunk) || chunk.isGenerated)
                {
                    generateIndex++;
                    continue;
                }

                if (!ChunkUtility.HasAllNeighborChunks(chunk.coordinate))
                {
                    generateIndex++;
                    continue;
                }

                chunk.Generate();
                generated++;
                generateIndex++;
            }

            if (generateIndex >= chunksToGenerate.Count)
            {
                chunksToGenerate.Clear();
                queuedForGenerate.Clear();
                generateIndex = 0;
            }
            else if (generateIndex > 64)
            {
                // Periodic compaction to avoid ever-growing consumed prefix
                int remaining = chunksToGenerate.Count - generateIndex;
                for (int i = 0; i < remaining; i++)
                    chunksToGenerate[i] = chunksToGenerate[generateIndex + i];

                chunksToGenerate.RemoveRange(remaining, generateIndex);
                generateIndex = 0;

                // rebuild queued set from remaining items
                queuedForGenerate.Clear();
                for (int i = 0; i < chunksToGenerate.Count; i++)
                    queuedForGenerate.Add(chunksToGenerate[i]);
            }
        }

        private void UpdateChunkVisibility(Vector3Int playerChunk)
        {
            // Disable chunks no longer in range
            foreach (var position in lastActiveChunks)
            {
                if (currentActiveChunks.Contains(position))
                    continue;

                if (chunks.TryGetValue(position, out var chunk))
                    chunk.SetActive(false);
            }

            // Enable chunks now in range
            foreach (var position in currentActiveChunks)
            {
                if (!chunks.TryGetValue(position, out var chunk))
                    continue;

                int dx = playerChunk.x - position.x;
                int dz = playerChunk.z - position.z;
                int dy = playerChunk.y - position.y;

                bool visibleXZ = dx * dx + dz * dz <= viewDistanceXZSq;
                bool visibleY = dy * dy <= viewDistanceYSq;

                chunk.SetActive(visibleXZ && visibleY);
            }

            lastActiveChunks.Clear();
            foreach (var pos in currentActiveChunks)
                lastActiveChunks.Add(pos);
        }
    }
}