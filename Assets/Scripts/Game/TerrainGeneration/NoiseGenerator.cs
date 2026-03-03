// GenerateTerrainHeightMapJob.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BenScr.MinecraftClone
{
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

            float height01 = TerrainNoiseUtility.SampleNormalizedHeight(
                worldPosition,
                continentLayer,
                mountainLayer,
                detailLayer,
                ridgeLayer,
                flatlandsHeightMultiplier,
                mountainHeightMultiplier,
                mountainBlendStart,
                mountainBlendSharpness);

            int groundLevel = (int)math.floor(height01 * noiseHeight) + groundOffset;
            heightMap[index] = groundLevel;
        }
    }
}