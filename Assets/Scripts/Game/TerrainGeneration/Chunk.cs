using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static BenScr.MinecraftClone.SettingsContainer;

namespace BenScr.MinecraftClone
{
    public struct ByteVector3
    {
        public byte x;
        public byte y;
        public byte z;

        public ByteVector3(byte x, byte y, byte z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(ByteVector3 other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is ByteVector3 other && Equals(other);

        public override int GetHashCode()
            => x | (y << 8) | (z << 16);
    }

    public class DamagedBlock
    {
        public int health;
        public GameObject damageStage;

        public  DamagedBlock(int health, GameObject damageStage)
        {
            this.health = health;
            this.damageStage = damageStage;
        }
    }


    public class Chunk
    {
        public const int CHUNK_SIZE = 32;
        public const int CHUNK_HEIGHT = 32;

        // Block types
        public const int BLOCK_AIR = 0;
        public const int BLOCK_DIRT = 1;
        public const int BLOCK_GRASS = 2;
        public const int BLOCK_STONE = 3;
        public const int BLOCK_WOOD = 4;
        public const int BLOCK_LEAVES = 5;
        public const int BLOCK_SNOW_GRASS = 8;
        public const int BLOCK_WATER = 14;

        public byte[,,] blocks;

        public bool isGenerated;
        public bool isAirOnly = true;

        public short lowestGroundLevel = short.MaxValue;
        public short highestGroundLevel = short.MinValue;
        public bool IsTop => (highestGroundLevel - position.y) < CHUNK_HEIGHT;
        public bool RequireChunkBelow => lowestGroundLevel < position.y;

        public bool IsBottom => (lowestGroundLevel - position.y) <= 0;

        public GameObject gameObject;
        public MeshRenderer meshRenderer;
        public MeshFilter meshFilter;
        public MeshCollider meshCollider;

        public GameObject fluidGameObject;
        public MeshRenderer fluidRenderer;
        public MeshFilter fluidFilter;

        public GameObject transparentGameObject;
        public MeshRenderer transparentRenderer;
        public MeshFilter transparentFilter;
        public MeshCollider transparentMeshCollider;

        public Vector3Int coordinate;
        public Vector3 position;

        public Dictionary<ByteVector3, DamagedBlock> damagedBlocks = new();

        public Chunk(int x, int y, int z)
        {
            coordinate = new Vector3Int(x, y, z);
        }

        public void AddMeshCollider()
        {
            if (isGenerated)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.mesh;
                transparentMeshCollider = transparentGameObject.AddComponent<MeshCollider>();
                transparentMeshCollider.sharedMesh = transparentFilter.mesh;
            }
        }

        public Block GetBlock(Vector3 localPosition)
        {
            Vector3Int blockPosition = new Vector3Int(
                      Mathf.FloorToInt(localPosition.x),
                      Mathf.FloorToInt(localPosition.y),
                      Mathf.FloorToInt(localPosition.z)

              );

            return AssetsContainer.GetBlock(blocks[blockPosition.x, blockPosition.y, blockPosition.z]);
        }

        public void SetBlock(Vector3 localPosition, int blockId, bool update = true)
        {
            Vector3Int blockPosition = new Vector3Int(
                        Mathf.FloorToInt(localPosition.x),
                        Mathf.FloorToInt(localPosition.y),
                        Mathf.FloorToInt(localPosition.z)
                );

            if (ChunkUtility.IsInsideChunk(blockPosition))
            {
                blocks[blockPosition.x, blockPosition.y, blockPosition.z] = (byte)blockId;

                if (update)
                {
                    Generate();

                    Chunk front = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y, coordinate.z + 1));
                    Chunk back = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y, coordinate.z - 1));
                    Chunk right = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x + 1, coordinate.y, coordinate.z));
                    Chunk left = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x - 1, coordinate.y, coordinate.z));

                    if (front != null && blockPosition.z == CHUNK_SIZE - 1) front.Generate();
                    if (back != null && blockPosition.z == 0) back.Generate();
                    if (right != null && blockPosition.x == CHUNK_SIZE - 1) right.Generate();
                    if (left != null && blockPosition.x == 0) left.Generate();
                }
            }
        }

        public void Generate()
        {
            RequestMeshData();
            isGenerated = true;
        }

        public void Prepare()
        {
            gameObject = GameObject.Instantiate(TerrainGenerator.instance.chunkPrefab);
            gameObject.name = $"Chunk_{coordinate.x}_{coordinate.y}_{coordinate.z}";
            meshFilter = gameObject.GetComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();

            meshRenderer.material = AssetsContainer.instance.blockMaterial;

            gameObject.transform.position = new Vector3(coordinate.x * CHUNK_SIZE, coordinate.y * CHUNK_HEIGHT, coordinate.z * CHUNK_SIZE);
            position = gameObject.transform.position;

            fluidGameObject = gameObject.transform.GetChild(0).gameObject;
            fluidRenderer = fluidGameObject.GetComponent<MeshRenderer>();
            fluidFilter = fluidGameObject.GetComponent<MeshFilter>();
            fluidRenderer.material = AssetsContainer.instance.fluidMaterial;

            transparentGameObject = gameObject.transform.GetChild(1).gameObject;
            transparentRenderer = transparentGameObject.GetComponent<MeshRenderer>();
            transparentFilter = transparentGameObject.GetComponent<MeshFilter>();
            transparentRenderer.material = AssetsContainer.instance.transparentMaterial;

            bool isAboveTopChunk = ChunkUtility.GetChunkByCoordinate(coordinate + Vector3Int.down)?.IsTop ?? false;

            if (!isAboveTopChunk)
            {
                PrepareCubes();
                isGenerated = isAirOnly;
            }
            else
            {
                blocks = new byte[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
                isGenerated = true;
            }
        }
        private void PrepareCubes()
        {
            blocks = new byte[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
            NativeArray<int> heightMap = new NativeArray<int>(CHUNK_SIZE * CHUNK_SIZE, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> Blocks = new NativeArray<byte>(CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE, Allocator.TempJob);

            try
            {
                TerrainGenerator.instance.GetNoiseLayers(out var continentLayer, out var mountainLayer, out var detailLayer, out var ridgeLayer);

                GenerateTerrainHeightMapJob heightJob = new GenerateTerrainHeightMapJob
                {
                    heightMap = heightMap,
                    chunkSize = CHUNK_SIZE,
                    chunkOrigin = new float2(coordinate.x * CHUNK_SIZE, coordinate.z * CHUNK_SIZE),
                    continentLayer = continentLayer,
                    mountainLayer = mountainLayer,
                    detailLayer = detailLayer,
                    ridgeLayer = ridgeLayer,
                    flatlandsHeightMultiplier = TerrainGenerator.instance.flatlandsHeightMultiplier,
                    mountainHeightMultiplier = TerrainGenerator.instance.mountainHeightMultiplier,
                    mountainBlendStart = TerrainGenerator.instance.mountainBlendStart,
                    mountainBlendSharpness = TerrainGenerator.instance.mountainBlendSharpness,
                    noiseHeight = TerrainGenerator.instance.noiseHeight,
                    groundOffset = TerrainGenerator.instance.groundOffset,
                };

                JobHandle heightHandle = heightJob.Schedule(heightMap.Length, 64);
                heightHandle.Complete();


                GenerateBlocksJob generateBlocksJob = new GenerateBlocksJob
                {
                    blocks = Blocks,
                    chunkSize = CHUNK_SIZE,
                    chunkHeight = CHUNK_HEIGHT,
                    groundOffset = TerrainGenerator.instance.groundOffset,
                    heightMap = heightMap,
                    chunkCoordinate = new int3(coordinate.x, coordinate.y, coordinate.z),
                    caveNoise = TerrainGenerator.instance.caveNoise,
                    enableCaves = TerrainGenerator.instance.enableCaves,
                    noiseOffset = TerrainGenerator.instance.noiseOffset,
                    caveNoiseRuntimeOffset = TerrainGenerator.instance.caveNoiseRuntimeOffset,
                    waterLevel = TerrainGenerator.instance.waterLevel,
                };

                JobHandle blockHandle = generateBlocksJob.Schedule(Blocks.Length, 64);
                blockHandle.Complete();

                for (int x = 0; x < CHUNK_SIZE; x++)
                    for (int y = 0; y < CHUNK_HEIGHT; y++)
                        for (int z = 0; z < CHUNK_SIZE; z++)
                        {
                            int groundLevel = heightMap[x + z * CHUNK_SIZE];

                            if (groundLevel < lowestGroundLevel)
                                lowestGroundLevel = (short)groundLevel;
                            if (groundLevel > highestGroundLevel)
                                highestGroundLevel = (short)groundLevel;

                            int index = x + y * CHUNK_SIZE + z * CHUNK_SIZE * CHUNK_HEIGHT;
                            byte block = Blocks[index];
                            blocks[x, y, z] = block;

                            if (isAirOnly && block != BLOCK_AIR)
                                isAirOnly = false;
                        }

                if (!isAirOnly && IsTop)
                {
                    for (int x = 0; x < CHUNK_SIZE; x++)
                        for (int y = 0; y < CHUNK_HEIGHT; y++)
                            for (int z = 0; z < CHUNK_SIZE; z++)
                            {
                                int groundLevel = heightMap[x + z * CHUNK_SIZE];

                                if (groundLevel == (y - position.y) && TerrainGenerator.instance.addTrees && y + 13 <= CHUNK_HEIGHT)
                                {
                                    if (x > 3 && z > 3 && x < CHUNK_SIZE - 3 && z < CHUNK_SIZE - 3)
                                    {
                                        if (UnityEngine.Random.Range(0, 75) == 0)
                                        {
                                            AddTree(x, y + 1, z);
                                        }
                                    }
                                }
                            }
                }
            }
            finally
            {
                if (heightMap.IsCreated)
                {
                    heightMap.Dispose();
                }
                if (Blocks.IsCreated)
                {
                    Blocks.Dispose();
                }
            }
        }

        private void AddTree(int x, int y, int z)
        {
            int height = UnityEngine.Random.Range(4, 7);

            for (int i = 0; i < height; i++)
            {
                if (ChunkUtility.IsInsideChunk(new Vector3Int(x, y + i, z)))
                    blocks[x, y + i, z] = BLOCK_WOOD;
            }

            int treeHeadRadius = UnityEngine.Random.Range(4, 6);

            for (int relativeX = -treeHeadRadius; relativeX < treeHeadRadius + 1; relativeX++)
            {
                for (int relativeY = 0; relativeY < treeHeadRadius + 1; relativeY++)
                {
                    for (int relativeZ = -treeHeadRadius; relativeZ < treeHeadRadius + 1; relativeZ++)
                    {
                        Vector3 center = new Vector3(x, y + height + treeHeadRadius / 8.0f, z);
                        Vector3Int blockPos = new Vector3Int(x + relativeX, y + relativeY + height, z + relativeZ);

                        if ((blockPos - center).magnitude < treeHeadRadius)
                        {
                            if (ChunkUtility.IsInsideChunk(blockPos))
                                blocks[blockPos.x, blockPos.y, blockPos.z] = BLOCK_LEAVES;
                        }
                    }
                }
            }
        }

        private void RequestMeshData() // average sw time: 0 ms
        {
            ChunkMeshGenerator.RequestMeshData(BuildHaloBlockArray(), OnMeshDataReceived);
        }

        public byte[,,] BuildHaloBlockArray()
        {
            const int SX = CHUNK_SIZE, SY = CHUNK_HEIGHT, SZ = CHUNK_SIZE;

            int originX = coordinate.x * SX;
            int originY = coordinate.y * SY;
            int originZ = coordinate.z * SZ;

            var halo = new byte[SX + 2, SY + 2, SZ + 2];

            for (int x = 0; x < SX; x++)
            {
                for (int y = 0; y < SY; y++)
                {
                    for (int z = 0; z < SZ; z++)
                    {
                        halo[x + 1, y + 1, z + 1] = blocks[x, y, z];
                    }
                }
            }

            Chunk negX = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x - 1, coordinate.y, coordinate.z));
            Chunk posX = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x + 1, coordinate.y, coordinate.z));
            Chunk negY = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y - 1, coordinate.z));
            Chunk posY = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y + 1, coordinate.z));
            Chunk negZ = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y, coordinate.z - 1));
            Chunk posZ = ChunkUtility.GetChunkByCoordinate(new Vector3Int(coordinate.x, coordinate.y, coordinate.z + 1));

            // West face (x = -1 relative to chunk).
            for (int y = 0; y < SY; y++)
            {
                int worldY = originY + y;
                for (int z = 0; z < SZ; z++)
                {
                    int worldZ = originZ + z;
                    if (negX != null)
                    {
                        halo[0, y + 1, z + 1] = negX.blocks[SX - 1, y, z];
                    }
                    else
                    {
                        halo[0, y + 1, z + 1] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(originX - 1, worldY, worldZ));
                    }
                }
            }

            // East face (x = +1 relative to chunk).
            for (int y = 0; y < SY; y++)
            {
                int worldY = originY + y;
                for (int z = 0; z < SZ; z++)
                {
                    int worldZ = originZ + z;
                    if (posX != null)
                    {
                        halo[SX + 1, y + 1, z + 1] = posX.blocks[0, y, z];
                    }
                    else
                    {
                        halo[SX + 1, y + 1, z + 1] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(originX + SX, worldY, worldZ));
                    }
                }
            }

            // Bottom face (y = -1 relative to chunk).
            for (int x = 0; x < SX; x++)
            {
                int worldX = originX + x;
                for (int z = 0; z < SZ; z++)
                {
                    int worldZ = originZ + z;
                    if (negY != null)
                    {
                        halo[x + 1, 0, z + 1] = negY.blocks[x, SY - 1, z];
                    }
                    else
                    {
                        halo[x + 1, 0, z + 1] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(worldX, originY - 1, worldZ));
                    }
                }
            }

            // Top face (y = +1 relative to chunk).
            for (int x = 0; x < SX; x++)
            {
                int worldX = originX + x;
                for (int z = 0; z < SZ; z++)
                {
                    int worldZ = originZ + z;
                    if (posY != null)
                    {
                        halo[x + 1, SY + 1, z + 1] = posY.blocks[x, 0, z];
                    }
                    else
                    {
                        halo[x + 1, SY + 1, z + 1] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(worldX, originY + SY, worldZ));
                    }
                }
            }

            // South face (z = -1 relative to chunk).
            for (int x = 0; x < SX; x++)
            {
                int worldX = originX + x;
                for (int y = 0; y < SY; y++)
                {
                    int worldY = originY + y;
                    if (negZ != null)
                    {
                        halo[x + 1, y + 1, 0] = negZ.blocks[x, y, SZ - 1];
                    }
                    else
                    {
                        halo[x + 1, y + 1, 0] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(worldX, worldY, originZ - 1));
                    }
                }
            }

            // North face (z = +1 relative to chunk).
            for (int x = 0; x < SX; x++)
            {
                int worldX = originX + x;
                for (int y = 0; y < SY; y++)
                {
                    int worldY = originY + y;
                    if (posZ != null)
                    {
                        halo[x + 1, y + 1, SZ + 1] = posZ.blocks[x, y, 0];
                    }
                    else
                    {
                        halo[x + 1, y + 1, SZ + 1] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(worldX, worldY, originZ + SZ));
                    }
                }
            }

            int maxX = SX + 1;
            int maxY = SY + 1;
            int maxZ = SZ + 1;

            for (int x = 0; x <= maxX; x++)
            {
                int worldX = originX + x - 1;
                bool boundaryX = x == 0 || x == maxX;
                for (int y = 0; y <= maxY; y++)
                {
                    int worldY = originY + y - 1;
                    bool boundaryY = y == 0 || y == maxY;
                    for (int z = 0; z <= maxZ; z++)
                    {
                        bool boundaryZ = z == 0 || z == maxZ;
                        int boundaryCount = (boundaryX ? 1 : 0) + (boundaryY ? 1 : 0) + (boundaryZ ? 1 : 0);

                        if (boundaryCount >= 2)
                        {
                            int worldZ = originZ + z - 1;
                            halo[x, y, z] = (byte)ChunkUtility.GetBlockAtBlock(new Vector3Int(worldX, worldY, worldZ));
                        }
                    }
                }
            }

            return halo;
        }
        private void OnMeshDataReceived([ReadOnly] MeshData meshData)
        {
            if (meshFilter == null) return;

            MeshSection solidMeshData = meshData.solidMesh;
            MeshSection fluidMeshData = meshData.fluidMesh;
            MeshSection transparentMeshData = meshData.transparentMesh;


            Mesh solidMesh = new Mesh();

            solidMesh.vertices = solidMeshData.vertices;
            solidMesh.triangles = solidMeshData.triangles;
            solidMesh.normals = solidMeshData.normals;
            solidMesh.uv = solidMeshData.uvs;

            meshFilter.mesh = solidMesh;

            Mesh fluidMesh = new Mesh();
            fluidMesh.vertices = fluidMeshData.vertices;
            fluidMesh.triangles = fluidMeshData.triangles;
            fluidMesh.normals = fluidMeshData.normals;
            fluidMesh.uv = fluidMeshData.uvs;

            fluidFilter.sharedMesh = fluidMesh;

            Mesh transparentMesh = new Mesh();

            bool needs32 = transparentMeshData.vertices.Length > short.MaxValue || transparentMeshData.triangles.Length > short.MaxValue;
            transparentMesh.indexFormat = needs32 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            transparentMesh.vertices = transparentMeshData.vertices;
            transparentMesh.triangles = transparentMeshData.triangles;
            transparentMesh.normals = transparentMeshData.normals;
            transparentMesh.uv = transparentMeshData.uvs;

            transparentFilter.sharedMesh = transparentMesh;


            if (meshCollider != null)
            {
                if (solidMeshData.vertices.Length == 0 || solidMeshData.triangles.Length == 0)
                    meshCollider.sharedMesh = null;
                else
                    meshCollider.sharedMesh = solidMesh;

                if (transparentMeshData.vertices.Length == 0 || transparentMeshData.triangles.Length == 0)
                    transparentMeshCollider.sharedMesh = null;
                else
                    transparentMeshCollider.sharedMesh = transparentMesh;
            }
        }

        public void SetActive(bool enabled)
        {
            gameObject.SetActive(enabled);
        }

        [BurstCompile]
        public struct GenerateBlocksJob : IJobParallelFor
        {
            public NativeArray<byte> blocks;
            [ReadOnly] public NativeArray<int> heightMap;
            [ReadOnly] public int chunkSize;
            [ReadOnly] public int chunkHeight;
            [ReadOnly] public int groundOffset;
            [ReadOnly] public int3 chunkCoordinate;
            [ReadOnly] public bool enableCaves;
            [ReadOnly] public TerrainGenerator.CaveNoiseSettings caveNoise;
            [ReadOnly] public float3 caveNoiseRuntimeOffset;
            [ReadOnly] public float2 noiseOffset;
            [ReadOnly] public int waterLevel;

            public void Execute(int index)
            {
                Pcg32 rng = new Pcg32(0, (ulong)index);

                int x = index % chunkSize;
                int t = index / chunkSize;
                int y = t % chunkHeight;
                int z = t / chunkHeight;

                int heightMapIndex = z * chunkSize + x;
                int groundLevel = heightMap[heightMapIndex];

                int worldX = chunkCoordinate.x * chunkSize + x;
                int worldY = chunkCoordinate.y * chunkHeight + y;
                int worldZ = chunkCoordinate.z * chunkSize + z;

                if (blocks[index] != BLOCK_AIR)
                {
                    return;
                }

                byte blockId;

                if (worldY > groundLevel)
                {
                    int waterLevel = groundOffset + this.waterLevel;
                    if (worldY <= waterLevel)
                    {
                        blockId = BLOCK_WATER;
                    }
                    else
                        blockId = BLOCK_AIR;
                }
                else
                {
                    if (worldY == groundLevel)
                    {
                        blockId = (byte)(groundLevel >= 30 ? BLOCK_SNOW_GRASS : BLOCK_GRASS);
                    }
                    else if (worldY > groundLevel - 5)
                    {
                        blockId = BLOCK_DIRT;
                    }
                    else
                    {
                        blockId = BLOCK_STONE;
                    }

                    if (blockId != BLOCK_AIR)
                    {
                        float3 worldPosition = new float3(worldX, worldY, worldZ);
                        if (ShouldCarveCave(worldPosition, groundLevel))
                        {
                            blockId = BLOCK_AIR;
                        }
                    }
                }

                blocks[index] = blockId;
            }

            internal bool ShouldCarveCave(float3 worldPosition, int groundLevel)
            {
                if (!enableCaves)
                    return false;

                if (worldPosition.y >= groundLevel - caveNoise.surfaceClearance)
                    return false;

                float noiseValue = SampleCaveNoise01(worldPosition);
                return noiseValue > caveNoise.threshold;
            }

            internal float SampleCaveNoise01(float3 worldPosition)
            {
                float horizontalFrequency = 1f / Mathf.Max(0.0001f, caveNoise.scale);
                float verticalFrequency = 1f / Mathf.Max(0.0001f, caveNoise.verticalScale);

                float sampleX = worldPosition.x + caveNoise.offset.x + caveNoiseRuntimeOffset.x + noiseOffset.x;
                float sampleY = worldPosition.y + caveNoise.offset.y + caveNoiseRuntimeOffset.y;
                float sampleZ = worldPosition.z + caveNoise.offset.z + caveNoiseRuntimeOffset.z + noiseOffset.y;

                float3 sample = new float3(
                    sampleX * horizontalFrequency,
                    sampleY * verticalFrequency,
                    sampleZ * horizontalFrequency
                );

                float noiseValue = noise.snoise(sample);
                return noiseValue * 0.5f + 0.5f;
            }
        }
    }
}