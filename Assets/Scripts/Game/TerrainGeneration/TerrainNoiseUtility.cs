using Unity.Mathematics;

namespace BenScr.MinecraftClone
{
    public static class TerrainNoiseUtility
    {
        public static float SampleNormalizedHeight(
              float2 worldPosition,
              NoiseLayer continentLayer,
              NoiseLayer mountainLayer,
              NoiseLayer detailLayer,
              NoiseLayer ridgeLayer,
              float flatlandsHeightMultiplier,
              float mountainHeightMultiplier,
              float mountainBlendStart,
              float mountainBlendSharpness)
        {
            float2 warpedPosition = ApplyDomainWarp(worldPosition, detailLayer, ridgeLayer);

            float continentValue = SampleFractalLayer(warpedPosition, continentLayer, 5, 2f, 0.5f);
            float continentErosion = SampleFractalLayer(warpedPosition, detailLayer, 2, 2.6f, 0.5f) * 0.08f;
            continentValue = math.saturate(continentValue - continentErosion);

            float baseMountain = SampleFractalLayer(warpedPosition, mountainLayer, 5, 2.1f, 0.48f);
            float peakMountain = SampleFractalLayer(warpedPosition * 1.8f, mountainLayer, 3, 2.35f, 0.45f);
            float ridgeValue = SampleRidgedFractalLayer(warpedPosition, ridgeLayer, 4, 2f, 0.5f);
            float detailValue = SampleFractalLayer(warpedPosition, detailLayer, 4, 2.35f, 0.45f);

            float blendRange = math.max(0.05f, 0.35f / math.max(0.1f, mountainBlendSharpness));
            float mountainMask = math.smoothstep(mountainBlendStart, mountainBlendStart + blendRange, continentValue);
            mountainMask *= mountainMask;

            float flatlandBase = math.pow(math.saturate(continentValue), 1.65f);
            float flatlandDetail = (detailValue * 2f - 1f) * 0.1f;
            float flatlands = math.saturate(flatlandBase + flatlandDetail) * flatlandsHeightMultiplier;

            float mountainShape = math.saturate(baseMountain * 0.65f + peakMountain * 0.6f + ridgeValue * 0.85f);
            float mountainHeight = math.pow(mountainShape, 1.05f) * mountainHeightMultiplier;

            float blended = math.lerp(flatlands, mountainHeight, mountainMask);

            float valleyCarving = (1f - ridgeValue) * 0.18f * mountainMask;
            blended -= valleyCarving;

            float inlandMask = math.smoothstep(0.25f, 0.55f, continentValue);
            float microDetail = (detailValue * 2f - 1f) * math.lerp(0.03f, 0.2f, mountainMask) * inlandMask;
            blended += microDetail;

            float coastalFlattening = 1f - math.smoothstep(0.2f, 0.35f, continentValue);
            blended = math.lerp(blended, blended * 0.75f, coastalFlattening);

            return math.saturate(blended);
        }

        static float2 ApplyDomainWarp(float2 worldPosition, NoiseLayer detailLayer, NoiseLayer ridgeLayer)
        {
            float warpFrequency = math.max(detailLayer.frequency * 0.6f, 0.00005f);

            float2 xOffset = new float2(127.1f, 311.7f) + detailLayer.offset;
            float2 yOffset = new float2(269.5f, 183.3f) + ridgeLayer.offset;

            float warpX = noise.snoise((worldPosition + xOffset) * warpFrequency);
            float warpY = noise.snoise((worldPosition + yOffset) * warpFrequency);

            float warpStrength = math.max(12f, math.max(1f / math.max(detailLayer.frequency, 0.00001f), 1f / math.max(ridgeLayer.frequency, 0.00001f)) * 0.08f);
            return worldPosition + new float2(warpX, warpY) * warpStrength;
        }

        static float SampleFractalLayer(float2 worldPosition, NoiseLayer layer, int octaves, float lacunarity, float persistence)
        {
            return SampleLayer(worldPosition, layer, octaves, lacunarity, persistence, ridged: false);
        }

        static float SampleRidgedFractalLayer(float2 worldPosition, NoiseLayer layer, int octaves, float lacunarity, float persistence)
        {
            return SampleLayer(worldPosition, layer, octaves, lacunarity, persistence, ridged: true);
        }

        static float SampleLayer(float2 worldPosition, NoiseLayer layer, int octaves, float lacunarity, float persistence, bool ridged)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = layer.frequency;
            float totalAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float2 sample = (worldPosition + layer.offset) * frequency;
                float octaveValue = noise.snoise(sample);

                if (ridged)
                {
                    octaveValue = 1f - math.abs(octaveValue);
                    octaveValue *= octaveValue;
                }
                else
                {
                    octaveValue = octaveValue * 0.5f + 0.5f;
                }

                sum += octaveValue * amplitude;
                totalAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalizedValue = totalAmplitude > 0f ? sum / totalAmplitude : 0f;

            if (math.abs(layer.redistribution - 1f) > 0.0001f)
            {
                normalizedValue = math.pow(normalizedValue, layer.redistribution);
            }

            return normalizedValue * layer.amplitude;
        }
    }
}