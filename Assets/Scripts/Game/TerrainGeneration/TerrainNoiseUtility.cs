using Unity.Burst;
using Unity.Mathematics;

namespace BenScr.MinecraftClone
{
    public static class TerrainNoiseUtility
    {
        [BurstCompile]
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
            float cont = Fbm01(worldPosition, continentLayer, octaves: 4, lacunarity: 2.0f, gain: 0.5f);
            cont = Redistribute01(cont, continentLayer.redistribution);

            float mtn = Fbm01(worldPosition, mountainLayer, octaves: 5, lacunarity: 2.1f, gain: 0.52f);
            mtn = Redistribute01(mtn, mountainLayer.redistribution);

            float rid = Ridged01(worldPosition, ridgeLayer, octaves: 4, lacunarity: 2.05f, gain: 0.5f);
            rid = Redistribute01(rid, ridgeLayer.redistribution);

            float det = Fbm01(worldPosition, detailLayer, octaves: 6, lacunarity: 2.2f, gain: 0.48f);
            det = Redistribute01(det, detailLayer.redistribution);


            float baseMask = math.saturate((cont - mountainBlendStart) * mountainBlendSharpness);
            baseMask = Smooth01(baseMask);
            float mtnMask = math.saturate(baseMask * math.lerp(0.35f, 1.0f, mtn));

            float baseHeight = cont * flatlandsHeightMultiplier;

            float mountainShape = (0.65f * mtn + 0.35f * rid);
            mountainShape = Smooth01(mountainShape);

            float mountainHeight = mountainShape * mountainHeightMultiplier * mtnMask;

            float detailAmount = math.lerp(0.06f, 0.14f, mtnMask);
            float detailHeight = (det - 0.5f) * 2.0f;
            detailHeight *= detailAmount;

            float h = baseHeight + mountainHeight + detailHeight;


            float estimatedMin = -0.20f;
            float estimatedMax = flatlandsHeightMultiplier + mountainHeightMultiplier + 0.20f;

            float h01 = math.unlerp(estimatedMin, estimatedMax, h);
            h01 = math.saturate(h01);

            h01 = Contrast01(h01, 1.10f);

            return h01;
        }

        [BurstCompile]
        private static float Fbm01(float2 p, NoiseLayer layer, int octaves, float lacunarity, float gain)
        {
            float2 q = (p + layer.offset) * layer.frequency;

            float sum = 0f;
            float amp = 1f;
            float freq = 1f;
            float norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = noise.snoise(q * freq);
                sum += n * amp;
                norm += amp;

                freq *= lacunarity;
                amp *= gain;
            }

            float nrm = (norm > 0f) ? (sum / norm) : 0f;

            float out01 = (nrm * 0.5f + 0.5f) * layer.amplitude;
            return math.saturate(out01);
        }

        [BurstCompile]
        private static float Ridged01(float2 p, NoiseLayer layer, int octaves, float lacunarity, float gain)
        {
            float2 q = (p + layer.offset) * layer.frequency;

            float sum = 0f;
            float amp = 1f;
            float freq = 1f;
            float norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = noise.snoise(q * freq);
                n = 1f - math.abs(n);
                n = n * n;

                sum += n * amp;
                norm += amp;

                freq *= lacunarity;
                amp *= gain;
            }

            float out01 = (norm > 0f) ? (sum / norm) : 0f;
            out01 *= layer.amplitude;
            return math.saturate(out01);
        }

        [BurstCompile]
        private static float Redistribute01(float x01, float redistribution)
        {
            float r = math.max(0.0001f, redistribution);
            return math.pow(math.saturate(x01), r);
        }

        [BurstCompile]
        private static float Smooth01(float x) => x * x * (3f - 2f * x);

        [BurstCompile]
        private static float Contrast01(float x01, float k)
        {
            float x = math.saturate(x01);
            float a = math.pow(x, k);
            float b = math.pow(1f - x, k);
            return a / (a + b + 1e-6f);
        }
    }
}