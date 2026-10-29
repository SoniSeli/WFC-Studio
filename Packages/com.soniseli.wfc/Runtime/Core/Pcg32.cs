using System;

namespace WfcStudio.Core
{
    /// <summary>
    /// PCG32 (XSH RR) random number generator by M. E. O'Neill, pcg-random.org.
    /// Integer-only, so the same seed gives the same sequence on every platform and runtime.
    /// </summary>
    public sealed class Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        readonly ulong _increment;

        /// <param name="seed">Starting state.</param>
        /// <param name="sequence">Selects one of 2^63 independent streams.</param>
        public Pcg32(ulong seed, ulong sequence = 0)
        {
            _increment = (sequence << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }

        /// <summary>Uniform value in [0, <paramref name="bound"/>), without modulo bias.</summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0)
                throw new ArgumentOutOfRangeException(nameof(bound), "Bound must be greater than zero.");

            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                    return value % bound;
            }
        }

        /// <summary>Uniform value in [0, 1).</summary>
        public double NextDouble()
        {
            return NextUInt() * (1.0 / 4294967296.0);
        }
    }
}
