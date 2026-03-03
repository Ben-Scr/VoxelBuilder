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

            float continentValue = SampleFractalLayer(macroWarpedPosition, continentLayer, 5, 2f, 0.5f);
            float continentErosion = SampleFractalLayer(warpedPosition, detailLayer, 2, 2.6f, 0.5f) * 0.1f;
            continentValue = math.saturate(continentValue - continentErosion);

            float baseMountain = SampleFractalLayer(warpedPosition, mountainLayer, 5, 2.1f, 0.48f);
            float peakMountain = SampleFractalLayer(warpedPosition * 1.85f, mountainLayer, 3, 2.35f, 0.45f);
            float ridgeValue = SampleRidgedFractalLayer(warpedPosition, ridgeLayer, 4, 2f, 0.5f);
            float detailValue = SampleFractalLayer(warpedPosition, detailLayer, 4, 2.35f, 0.45f);

            float blendRange = math.max(0.05f, 0.35f / math.max(0.1f, mountainBlendSharpness));
            float mountainMask = math.smoothstep(mountainBlendStart, mountainBlendStart + blendRange, continentValue);
            float plainsMask = math.saturate(1f - mountainMask);

            // Minecraft-like varied lowlands: broad continental undulation + rolling hills + local roughness.
            float continentalness = continentValue * 2f - 1f;

            float broadLowlandNoise = SampleFractalLayer(macroWarpedPosition * 0.58f, continentLayer, 3, 1.95f, 0.55f);
            broadLowlandNoise = (broadLowlandNoise * 2f - 1f) * 0.28f;

            float rollingHills = SampleFractalLayer(macroWarpedPosition * 1.05f, detailLayer, 4, 2f, 0.56f);
            rollingHills = (rollingHills * 2f - 1f) * 0.24f;

            float hummockNoise = SampleFractalLayer(warpedPosition * 1.45f, detailLayer, 5, 2.45f, 0.47f);
            hummockNoise = (hummockNoise * 2f - 1f) * 0.08f;

            float vegetationNoise = SampleFractalLayer(macroWarpedPosition * 0.9f, ridgeLayer, 3, 2.15f, 0.52f);
            float vegetationMask = math.smoothstep(0.32f, 0.82f, vegetationNoise);
            float biomeModulation = math.lerp(-0.05f, 0.12f, vegetationMask);

            // Foothills avoid giant flat regions between plains and mountains.
            float foothillMask = math.smoothstep(mountainBlendStart - 0.23f, mountainBlendStart + 0.04f, continentValue) * plainsMask;
            float foothills = (baseMountain * 0.4f + ridgeValue * 0.35f) * foothillMask * 0.5f;

            // Small river channels in lowlands.
            float riverNoise = math.abs(noise.snoise((macroWarpedPosition + new float2(901.6f, 173.2f)) * math.max(continentLayer.frequency * 3.1f, 0.0001f)));
            float riverMask = 1f - math.smoothstep(0.08f, 0.19f, riverNoise);
            float riverCarving = riverMask * plainsMask * math.smoothstep(0.16f, 0.72f, continentValue) * 0.2f;

            float flatlandBase = 0.42f + continentalness * 0.22f;
            float flatlandDetail = broadLowlandNoise + rollingHills + hummockNoise + biomeModulation + foothills;
            float flatlands = math.max(0f, flatlandBase + flatlandDetail - riverCarving) * flatlandsHeightMultiplier;

            float mountainShape = math.saturate(baseMountain * 0.62f + peakMountain * 0.62f + ridgeValue * 0.85f);
            float mountainHeight = math.pow(mountainShape, 1.03f) * mountainHeightMultiplier;

            float blended = math.lerp(flatlands, mountainHeight, mountainMask);

            float valleyCarving = (1f - ridgeValue) * 0.16f * mountainMask;
            blended -= valleyCarving;

            float inlandMask = math.smoothstep(0.22f, 0.55f, continentValue);
            float microDetail = (detailValue * 2f - 1f) * math.lerp(0.06f, 0.2f, mountainMask) * inlandMask;
            blended += microDetail;

            float floodplainSmoothing = (1f - vegetationMask) * plainsMask * 0.03f;
            blended = math.lerp(blended, flatlands, floodplainSmoothing);

            float coastalFlattening = 1f - math.smoothstep(0.14f, 0.31f, continentValue);
            blended = math.lerp(blended, blended * 0.86f, coastalFlattening);

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