using NUnit.Framework;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class Pcg32Tests
    {
        [Test]
        public void MatchesReferenceSequence()
        {
            // First outputs of pcg32-demo from the reference C implementation, seeded with (42, 54).
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

            var random = new Pcg32(42, 54);
            foreach (uint value in expected)
                Assert.That(random.NextUInt(), Is.EqualTo(value));
        }

        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new Pcg32(1234);
            var b = new Pcg32(1234);
            for (int i = 0; i < 100; i++)
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void BoundedValues_StayBelowBound()
        {
            var random = new Pcg32(7);
            for (int i = 0; i < 1000; i++)
                Assert.That(random.NextUInt(10), Is.LessThan(10u));
        }

        [Test]
        public void NextDouble_IsInUnitInterval()
        {
            var random = new Pcg32(7);
            for (int i = 0; i < 1000; i++)
                Assert.That(random.NextDouble(), Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
        }
    }
}
