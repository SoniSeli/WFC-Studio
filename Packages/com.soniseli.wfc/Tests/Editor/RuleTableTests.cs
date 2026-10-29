using System;
using NUnit.Framework;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class RuleTableTests
    {
        [Test]
        public void Allow_AddsTheReverseRule()
        {
            var rules = new RuleTable.Builder(2).Allow(0, Direction.Right, 1).Build();

            Assert.That(rules.Allows(0, Direction.Right, 1), Is.True);
            Assert.That(rules.Allows(1, Direction.Left, 0), Is.True);
            Assert.That(rules.Allows(1, Direction.Right, 0), Is.False);
            Assert.That(rules.Allows(0, Direction.Up, 1), Is.False);
        }

        [Test]
        public void SupportsMoreThan64Tiles()
        {
            var rules = new RuleTable.Builder(130).Allow(3, Direction.Up, 129).Build();

            Assert.That(rules.Allows(3, Direction.Up, 129), Is.True);
            Assert.That(rules.Allows(129, Direction.Down, 3), Is.True);
            Assert.That(rules.Allows(3, Direction.Up, 65), Is.False);
        }

        [Test]
        public void WeightsDefaultToOne()
        {
            var rules = new RuleTable.Builder(2).SetWeight(1, 2.5).Build();

            Assert.That(rules.GetWeight(0), Is.EqualTo(1.0));
            Assert.That(rules.GetWeight(1), Is.EqualTo(2.5));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void SetWeight_RejectsInvalidWeights(double weight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleTable.Builder(1).SetWeight(0, weight));
        }

        [Test]
        public void RejectsInvalidTileIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleTable.Builder(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleTable.Builder(2).Allow(0, Direction.Up, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleTable.Builder(2).SetWeight(-1, 1.0));
        }

        [Test]
        public void BuiltTable_IsUnaffectedByLaterBuilderChanges()
        {
            var builder = new RuleTable.Builder(2);
            var rules = builder.Build();
            builder.Allow(0, Direction.Up, 1);

            Assert.That(rules.Allows(0, Direction.Up, 1), Is.False);
        }

        [Test]
        public void TrailingZeroCount_FindsLowestBit()
        {
            for (int bit = 0; bit < 64; bit++)
                Assert.That(Bitset.TrailingZeroCount(1UL << bit), Is.EqualTo(bit));

            Assert.That(Bitset.TrailingZeroCount(0b1011000UL), Is.EqualTo(3));
            Assert.That(Bitset.TrailingZeroCount(ulong.MaxValue), Is.EqualTo(0));
        }
    }
}
