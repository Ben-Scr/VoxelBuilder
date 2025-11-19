using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BenScr.MinecraftClone
{
    public static class NoiseGenerator
    {
        public static byte[] GenerateNoisemapThreaded(Vector3 position, float noiseHeight, float noiseScale, Vector2 noiseOffset, int groundOffset)
        {
            const int LENGTH = Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE * Chunk.CHUNK_HEIGHT;
            var map = new NativeArray<byte>(LENGTH, Allocator.TempJob);

            GenerateNoisemapJob job = new GenerateNoisemapJob
            {
                map = map,
                position = position,
                noiseHeight = noiseHeight,
                noiseScale = noiseScale,
                noiseOffset = noiseOffset,
                groundOffset = groundOffset
            };

            job.Schedule(LENGTH, 64).Complete();

            byte[] result = map.ToArray();
            map.Dispose();
            return result;
        }


        [BurstCompile]
        public struct GenerateNoisemapJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<byte> map;

            [ReadOnly] public Vector3 position;
            [ReadOnly] public float noiseHeight;
            [ReadOnly] public float noiseScale;
            [ReadOnly] public Vector2 noiseOffset;
            [ReadOnly] public int groundOffset;

            public void Execute(int index)
            {
                int y = index % Chunk.CHUNK_HEIGHT;
                int t = index / Chunk.CHUNK_HEIGHT;
                int z = t % Chunk.CHUNK_SIZE;
                int x = t / Chunk.CHUNK_SIZE;

                short groundLevel = (short)(math.floor(PerlinNoise2D.Perlin2D((position.x + x + noiseOffset.x) / noiseScale, (position.z + z + noiseOffset.y) / noiseScale) * noiseHeight) + groundOffset);
                y += (int)position.y;

                if (y > groundLevel)
                {
                    map[index] = Chunk.BLOCK_AIR;
                }
                else
                {
                    if (y == groundLevel)
                    {
                        map[index] = Chunk.BLOCK_GRASS;
                    }
                    else
                    {
                        if (y > groundLevel - 5)
                        {
                            map[index] = Chunk.BLOCK_DIRT;
                        }
                        else
                        {
                            map[index] = Chunk.BLOCK_STONE;
                        }
                    }
                }
            }
        }
    }

    public struct NoiseLayer
    {
        public float frequency;
        public float amplitude;
        public float redistribution;
        public float2 offset;
    }


    [BurstCompile]
    public struct GenerateTerrainHeightMapJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<int> heightMap;
        [ReadOnly] public int chunkSize;
        [ReadOnly] public float2 chunkOrigin;
        [ReadOnly] public NoiseLayer continentLayer;
        [ReadOnly] public NoiseLayer mountainLayer;
        [ReadOnly] public NoiseLayer detailLayer;
        [ReadOnly] public NoiseLayer ridgeLayer;
        [ReadOnly] public float flatlandsHeightMultiplier;
        [ReadOnly] public float mountainHeightMultiplier;
        [ReadOnly] public float mountainBlendStart;
        [ReadOnly] public float mountainBlendSharpness;
        [ReadOnly] public int groundOffset;
        [ReadOnly] public float noiseHeight;

        public void Execute(int index)
        {
            int x = index % chunkSize;
            int z = index / chunkSize;

            float2 worldPosition = chunkOrigin + new float2(x, z);

            float height = TerrainNoiseUtility.SampleNormalizedHeight(
                worldPosition,
                continentLayer,
                mountainLayer,
                detailLayer,
                ridgeLayer,
                flatlandsHeightMultiplier,
                mountainHeightMultiplier,
                mountainBlendStart,
                mountainBlendSharpness);

            int groundLevel = (int)math.floor(height * noiseHeight) + groundOffset;
            heightMap[index] = groundLevel;
        }
    }
}