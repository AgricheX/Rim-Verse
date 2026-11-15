using System;

namespace Rimverse
{
    public class UniverseRand
    {
        private ulong state;

        public UniverseRand(ulong seed)
        {
            state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public static ulong Hash(params long[] parts)
        {
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < parts.Length; i++)
            {
                h ^= (ulong)parts[i] + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
                h *= 1099511628211UL;
            }
            return Mix(h);
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            state += 0x9E3779B97F4A7C15UL;
            return Mix(state);
        }

        public float Value => (NextULong() >> 40) / (float)(1UL << 24);

        public double ValueDouble => (NextULong() >> 11) / (double)(1UL << 53);

        public float Range(float min, float max) => min + (max - min) * Value;

        public int RangeInclusive(int min, int max) => min + (int)(Value * (max - min + 1 - 1e-6f));

        public bool Chance(float p) => Value < p;

        public int SkewedSizeClass(int min, int max, float power = 2.2f)
        {
            float r = (float)Math.Pow(Value, power);
            int v = min + (int)Math.Round(r * (max - min));
            return RimverseUnits.ClampSizeClass(v);
        }

        public T Weighted<T>(T[] items, float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            float roll = Value * total;
            for (int i = 0; i < items.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return items[i];
            }
            return items[items.Length - 1];
        }
    }
}
