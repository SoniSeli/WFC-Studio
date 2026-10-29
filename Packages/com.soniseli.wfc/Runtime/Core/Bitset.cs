namespace WfcStudio.Core
{
    // Helpers for fixed-width bitsets stored inside a larger ulong[] at a word offset.
    // Bit i lives in word (i / 64), position (i % 64).
    internal static class Bitset
    {
        // De Bruijn lookup for the index of the lowest set bit (0x03F79D71B4CB0A89).
        static readonly int[] LowestBitIndex =
        {
            0, 1, 48, 2, 57, 49, 28, 3, 61, 58, 50, 42, 38, 29, 17, 4,
            62, 55, 59, 36, 53, 51, 43, 22, 45, 39, 33, 30, 24, 18, 12, 5,
            63, 47, 56, 27, 60, 41, 37, 16, 54, 35, 52, 21, 44, 32, 23, 11,
            46, 26, 40, 15, 34, 20, 31, 10, 25, 14, 19, 9, 13, 8, 7, 6,
        };

        public static int WordsFor(int bitCount)
        {
            return (bitCount + 63) >> 6;
        }

        public static bool Contains(ulong[] words, int offset, int bit)
        {
            return (words[offset + (bit >> 6)] & (1UL << (bit & 63))) != 0;
        }

        public static void Set(ulong[] words, int offset, int bit)
        {
            words[offset + (bit >> 6)] |= 1UL << (bit & 63);
        }

        public static void Clear(ulong[] words, int offset, int bit)
        {
            words[offset + (bit >> 6)] &= ~(1UL << (bit & 63));
        }

        /// <summary>Index of the lowest set bit. <paramref name="value"/> must not be zero.</summary>
        public static int TrailingZeroCount(ulong value)
        {
            ulong lowest = value & (~value + 1);
            return LowestBitIndex[(lowest * 0x03F79D71B4CB0A89UL) >> 58];
        }
    }
}
