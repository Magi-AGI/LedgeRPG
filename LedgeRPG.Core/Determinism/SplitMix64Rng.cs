using System;

namespace LedgeRPG.Core.Determinism
{
    /// Deterministic 64-bit PRNG. We do NOT use System.Random because its
    /// algorithm changed between .NET Framework / Core 2.x / Core 3.x / .NET 6+
    /// — any seed-dependent reproducibility across toolchain upgrades is a
    /// regression waiting to happen. SplitMix64 is fixed by publication
    /// (Steele/Lea/Flood 2014), has no state beyond a single ulong, and passes
    /// TestU01's BigCrush — more than adequate for world-gen shuffles.
    ///
    /// Note: this does NOT reproduce Python's Mersenne Twister output. Two
    /// implementations seeded with the same value will diverge on the first
    /// draw. Cross-language parity with the Python server is intentionally out
    /// of scope for M1 — the Python server stays authoritative for the paper
    /// experiment, and post-paper the C# server becomes its own ground truth.
    public sealed class SplitMix64Rng
    {
        private ulong _state;

        public SplitMix64Rng(long seed)
        {
            // Cast via unchecked so negative seeds map onto the upper half of
            // the ulong state space rather than throwing.
            _state = unchecked((ulong)seed);
        }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// Returns an integer in [0, exclusiveUpperBound). Rejection-sampled to
        /// remove modulo bias — for small bounds (e.g. shuffle indices on an
        /// 8x8 = 64-element array) the rejection rate is negligible, and for
        /// larger bounds correctness is worth the occasional redraw.
        public int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveUpperBound), "must be positive");

            ulong bound = (ulong)exclusiveUpperBound;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
            while (true)
            {
                ulong r = NextULong();
                if (r < limit)
                    return (int)(r % bound);
            }
        }

        /// In-place Fisher-Yates shuffle using NextInt. Matches the standard
        /// Durstenfeld variant: iterate from the end, swap each element with a
        /// random earlier-or-same position. Deterministic given a fixed seed.
        public void Shuffle<T>(T[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                T tmp = array[i];
                array[i] = array[j];
                array[j] = tmp;
            }
        }
    }
}
