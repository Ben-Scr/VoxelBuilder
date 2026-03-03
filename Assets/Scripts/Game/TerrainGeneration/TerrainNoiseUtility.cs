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
            float continentValue = SampleFractalLayer(worldPosition, continentLayer, 4, 2f, 0.5f);
            float mountainValue = SampleFractalLayer(worldPosition, mountainLayer, 5, 2.1f, 0.48f);
            float detailValue = SampleFractalLayer(worldPosition, detailLayer, 3, 2.35f, 0.45f);
            float ridgeValue = SampleRidgedFractalLayer(worldPosition, ridgeLayer, 4, 2f, 0.5f);

            float mountainMask = math.saturate((continentValue - mountainBlendStart) * math.max(0.1f, mountainBlendSharpness * 2f));
            mountainMask = math.pow(mountainMask, 1.15f);

            float flatlands = math.pow(math.saturate(continentValue), 1.35f) * flatlandsHeightMultiplier;
            float mountainBase = math.saturate(mountainValue * 0.7f + ridgeValue * 0.95f);
            float mountainHeight = math.pow(mountainBase, 1.1f) * mountainHeightMultiplier;

            float blended = math.lerp(flatlands, mountainHeight, mountainMask);

            float valleyCarving = (1f - ridgeValue) * 0.12f * mountainMask;
            blended -= valleyCarving;

            float coastalFlattening = 1f - math.smoothstep(0.2f, 0.35f, continentValue);
            blended = math.lerp(blended, blended * 0.78f, coastalFlattening);

            float detailSigned = detailValue * 2f - 1f;
            float detailStrength = math.lerp(0.08f, 0.3f, mountainMask);
            blended += detailSigned * detailStrength;

            return math.saturate(blended);
        }

        static float SampleFractalLayer(float2 worldPosition, NoiseLayer layer, int octaves, float lacunarity, float persistence)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = layer.frequency;
            float amplitudeSum = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float2 sample = (worldPosition + layer.offset) * frequency;
                float noiseValue = noise.snoise(sample) * 0.5f + 0.5f;

                sum += noiseValue * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float noiseValue2 = amplitudeSum > 0f ? sum / amplitudeSum : 0f;

            if (math.abs(layer.redistribution - 1f) > 0.0001f)
            {
                noiseValue2 = math.pow(noiseValue2, layer.redistribution);
            }

            return noiseValue2 * layer.amplitude;
        }

        static float SampleRidgedFractalLayer(float2 worldPosition, NoiseLayer layer, int octaves, float lacunarity, float persistence)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = layer.frequency;
            float amplitudeSum = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float2 sample = (worldPosition + layer.offset) * frequency;
                float noiseValue = 1f - math.abs(noise.snoise(sample));
                noiseValue *= noiseValue;

                sum += noiseValue * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float noiseValue2 = amplitudeSum > 0f ? sum / amplitudeSum : 0f;

            if (math.abs(layer.redistribution - 1f) > 0.0001f)
            {
                noiseValue2 = math.pow(noiseValue2, layer.redistribution);
            }

            return noiseValue2 * layer.amplitude;
        }
    }
}