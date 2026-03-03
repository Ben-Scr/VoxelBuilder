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
            float2 macroWarpedPosition = ApplyMacroWarp(worldPosition, continentLayer, detailLayer);

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
            float plainsMask = math.saturate(1f - mountainMask);

            float rollingHills = SampleFractalLayer(macroWarpedPosition, detailLayer, 3, 1.9f, 0.56f);
            rollingHills = (rollingHills * 2f - 1f) * 0.15f;

            float hummockNoise = SampleFractalLayer(warpedPosition * 1.35f, detailLayer, 5, 2.45f, 0.47f);
            hummockNoise = (hummockNoise * 2f - 1f) * 0.07f;

            float vegetationNoise = SampleFractalLayer(macroWarpedPosition * 0.8f, ridgeLayer, 3, 2.15f, 0.52f);
            float vegetationMask = math.smoothstep(0.35f, 0.78f, vegetationNoise);
            float biomeModulation = math.lerp(-0.03f, 0.09f, vegetationMask);

            float riverNoise = math.abs(noise.snoise((macroWarpedPosition + new float2(901.6f, 173.2f)) * math.max(continentLayer.frequency * 2.8f, 0.0001f)));
            float riverMask = 1f - math.smoothstep(0.1f, 0.2f, riverNoise);
            float riverCarving = riverMask * plainsMask * math.smoothstep(0.18f, 0.68f, continentValue) * 0.14f;

            float flatlandDetail = rollingHills + hummockNoise + biomeModulation;
            float flatlands = math.saturate(flatlandBase + flatlandDetail) * flatlandsHeightMultiplier;
            flatlands = math.max(0f, flatlands - riverCarving);

            float mountainShape = math.saturate(baseMountain * 0.65f + peakMountain * 0.6f + ridgeValue * 0.85f);
            float mountainHeight = math.pow(mountainShape, 1.05f) * mountainHeightMultiplier;

            float blended = math.lerp(flatlands, mountainHeight, mountainMask);

            float valleyCarving = (1f - ridgeValue) * 0.18f * mountainMask;
            blended -= valleyCarving;

            float inlandMask = math.smoothstep(0.25f, 0.55f, continentValue);
            float microDetail = (detailValue * 2f - 1f) * math.lerp(0.03f, 0.2f, mountainMask) * inlandMask;
            blended += microDetail;

            float floodplainSmoothing = (1f - vegetationMask) * plainsMask * 0.04f;
            blended = math.lerp(blended, flatlands, floodplainSmoothing);

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

        static float2 ApplyMacroWarp(float2 worldPosition, NoiseLayer continentLayer, NoiseLayer detailLayer)
        {
            float macroFrequency = math.max(continentLayer.frequency * 0.8f, 0.00002f);
            float detailFrequency = math.max(detailLayer.frequency * 0.5f, 0.00002f);

            float2 xOffset = new float2(412.4f, 155.8f) + continentLayer.offset;
            float2 yOffset = new float2(93.1f, 641.7f) + detailLayer.offset;

            float warpX = noise.snoise((worldPosition + xOffset) * macroFrequency);
            float warpY = noise.snoise((worldPosition + yOffset) * detailFrequency);

            float warpStrength = math.max(18f, 1f / math.max(continentLayer.frequency, 0.00001f) * 0.045f);
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