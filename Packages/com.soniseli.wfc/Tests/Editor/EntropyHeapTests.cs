using NUnit.Framework;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class EntropyHeapTests
    {
        // The cell a front-to-back scan with a strict "less than" picks, which the heap must always match.
        static int ScanMin(double?[] keys)
        {
            int best = -1;
            double bestKey = double.MaxValue;
            for (int cell = 0; cell < keys.Length; cell++)
            {
                if (keys[cell] is double key && key < bestKey)
                {
                    bestKey = key;
                    best = cell;
                }
            }
            return best;
        }

        [Test]
        public void RandomSetsAndRemoves_AlwaysMatchAScan()
        {
            const int cells = 64;
            var heap = new EntropyHeap(cells);
            var keys = new double?[cells];
            var random = new Pcg32(11);

            for (int i = 0; i < 5000; i++)
            {
                int cell = (int)random.NextUInt(cells);
                if (random.NextDouble() < 0.3)
                {
                    heap.Remove(cell);
                    keys[cell] = null;
                }
                else
                {
                    // Few distinct values, so exact ties are common and the index tie-break is exercised.
                    double key = random.NextUInt(8);
                    heap.Set(cell, key);
                    keys[cell] = key;
                }

                int expected = ScanMin(keys);
                Assert.That(heap.Count > 0 ? heap.Min : -1, Is.EqualTo(expected), $"after operation {i}");
            }
        }

        [Test]
        public void Clear_EmptiesTheHeap()
        {
            var heap = new EntropyHeap(4);
            heap.Set(2, 1.0);
            heap.Set(3, 0.5);

            heap.Clear();

            Assert.That(heap.Count, Is.Zero);
            Assert.That(heap.Contains(3), Is.False);
            heap.Set(1, 2.0);
            Assert.That(heap.Min, Is.EqualTo(1));
        }
    }
}
