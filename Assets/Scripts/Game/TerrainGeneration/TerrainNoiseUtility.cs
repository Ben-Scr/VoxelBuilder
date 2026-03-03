using Unity.Burst;
using Unity.Mathematics;

namespace BenScr.MinecraftClone
{
    public static class TerrainNoiseUtility
    {
        // --- Public API ---------------------------------------------------------

        /// <summary>
        /// Returns a deterministic normalized height in [0..1], built from a
        /// Minecraft-like "base + mountains + detail + ridges" composition.
        /// Burst-friendly (no allocations, pure math).
        /// Requires Unity.Mathematics.noise (Simplex).
        /// </summary>
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
            // 1) Continentalness: big-scale landmass distribution (mostly smooth)
            //    Use low octave count, low lacunarity persistence -> broad shapes.
            float cont = Fbm01(worldPosition, continentLayer, octaves: 4, lacunarity: 2.0f, gain: 0.5f);
            cont = Redistribute01(cont, continentLayer.redistribution);

            // 2) Mountain/erosion-like field: medium scale, used to decide where mountains can appear
            float mtn = Fbm01(worldPosition, mountainLayer, octaves: 5, lacunarity: 2.1f, gain: 0.52f);
            mtn = Redistribute01(mtn, mountainLayer.redistribution);

            // 3) Ridge/peaks: ridged multifractal-ish (sharp crests)
            float rid = Ridged01(worldPosition, ridgeLayer, octaves: 4, lacunarity: 2.05f, gain: 0.5f);
            rid = Redistribute01(rid, ridgeLayer.redistribution);

            // 4) Detail: small-scale surface variation (micro noise)
            float det = Fbm01(worldPosition, detailLayer, octaves: 6, lacunarity: 2.2f, gain: 0.48f);
            det = Redistribute01(det, detailLayer.redistribution);

            // --- Mountain blending mask -----------------------------------------
            // Use continentalness as the primary "biome" driver, then modulate by mountain field.
            // This mimics the idea of Minecraft's broad selectors feeding more complex shaping.
            float baseMask = math.saturate((cont - mountainBlendStart) * mountainBlendSharpness);
            baseMask = Smooth01(baseMask);                    // soften edges
            float mtnMask = math.saturate(baseMask * math.lerp(0.35f, 1.0f, mtn)); // let mountain noise influence where peaks show up

            // --- Compose height --------------------------------------------------
            // Base terrain (flatlands / rolling hills):
            float baseHeight = cont * flatlandsHeightMultiplier;

            // Mountains: combine "mountain field" + ridges for peaks.
            // Keep ridges contributing mostly where mountains exist.
            float mountainShape = (0.65f * mtn + 0.35f * rid);
            mountainShape = Smooth01(mountainShape);

            float mountainHeight = mountainShape * mountainHeightMultiplier * mtnMask;

            // Detail: add subtle variation everywhere, more in mountains.
            float detailAmount = math.lerp(0.06f, 0.14f, mtnMask);
            float detailHeight = (det - 0.5f) * 2.0f;         // [-1..1]
            detailHeight *= detailAmount;

            // Final unnormalized height
            float h = baseHeight + mountainHeight + detailHeight;

            // --- Normalize to [0..1] --------------------------------------------
            // We estimate a plausible min/max based on multipliers to keep output stable and predictable.
            // This avoids "hard-coded magic" while remaining deterministic.
            float estimatedMin = -0.20f; // small headroom for detail pushing below base
            float estimatedMax = flatlandsHeightMultiplier + mountainHeightMultiplier + 0.20f;

            float h01 = math.unlerp(estimatedMin, estimatedMax, h);
            h01 = math.saturate(h01);

            // Optional final shaping: a gentle contrast curve that resembles MC’s “less flat mid-range”.
            h01 = Contrast01(h01, 1.10f);

            return h01;
        }

        // --- Helpers ------------------------------------------------------------

        // Fractal Brownian Motion returning [0..1]
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
                // noise.snoise returns roughly [-1..1]
                float n = Unity.Mathematics.noise.snoise(q * freq);
                sum += n * amp;
                norm += amp;

                freq *= lacunarity;
                amp *= gain;
            }

            // Normalize to [-1..1]
            float nrm = (norm > 0f) ? (sum / norm) : 0f;

            // Map to [0..1] and apply amplitude
            float out01 = (nrm * 0.5f + 0.5f) * layer.amplitude;
            return math.saturate(out01);
        }

        // Ridged multifractal-like noise returning [0..1]
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
                float n = Unity.Mathematics.noise.snoise(q * freq); // [-1..1]
                n = 1f - math.abs(n);                                    // ridges: [0..1]
                n = n * n;                                          // sharpen

                sum += n * amp;
                norm += amp;

                freq *= lacunarity;
                amp *= gain;
            }

            float out01 = (norm > 0f) ? (sum / norm) : 0f;
            out01 *= layer.amplitude;
            return math.saturate(out01);
        }

        // Redistribution curve in [0..1] (higher => more extremes, lower => flatter)
        [BurstCompile]
        private static float Redistribute01(float x01, float redistribution)
        {
            // redistribution == 1 => identity
            // >1 => push towards 0/1, <1 => pull towards 0.5
            float r = math.max(0.0001f, redistribution);
            return math.pow(math.saturate(x01), r);
        }

        [BurstCompile]
        private static float Smooth01(float x) => x * x * (3f - 2f * x); // smoothstep(0,1,x) but cheaper

        [BurstCompile]
        private static float Contrast01(float x01, float k)
        {
            // k=1 => identity, >1 => more contrast, <1 => less
            // symmetric curve around 0.5
            float x = math.saturate(x01);
            float a = math.pow(x, k);
            float b = math.pow(1f - x, k);
            return a / (a + b + 1e-6f);
        }
    }
}