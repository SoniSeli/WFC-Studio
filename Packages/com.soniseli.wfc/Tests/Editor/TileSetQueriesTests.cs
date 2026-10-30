using NUnit.Framework;
using WfcStudio.Core;
using WfcStudio.Editor;

namespace WfcStudio.Tests
{
    public class TileSetQueriesTests
    {
        static TileEntry Entry(string name, float weight)
        {
            return new TileEntry(name, null, weight, "a", "a", "a", "a");
        }

        [Test]
        public void Neighbours_ListsAllowedTilesInOrder_FromEitherSide()
        {
            var rules = new RuleTable.Builder(3)
                .Allow(0, Direction.Right, 2)
                .Allow(0, Direction.Right, 1)
                .Build();

            Assert.That(TileSetQueries.Neighbours(rules, 0, Direction.Right), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(TileSetQueries.Neighbours(rules, 2, Direction.Left), Is.EqualTo(new[] { 0 }));
            Assert.That(TileSetQueries.Neighbours(rules, 0, Direction.Up), Is.Empty);
        }

        [Test]
        public void WeightShare_IsTheTilesPartOfTheTotal_IgnoringInvalidWeights()
        {
            var tiles = new[] { Entry("Grass", 3f), Entry("Flowers", 1f), Entry("Broken", -2f) };

            Assert.That(TileSetQueries.WeightShare(tiles, 0), Is.EqualTo(0.75f));
            Assert.That(TileSetQueries.WeightShare(tiles, 2), Is.EqualTo(0f));
        }

        [Test]
        public void Filter_MatchesNamePartsIgnoringCase()
        {
            var tiles = new[] { Entry("Forest Top", 1f), Entry("Grass", 1f), Entry("Forest Centre", 1f) };

            Assert.That(TileSetQueries.Filter(tiles, " forest "), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(TileSetQueries.Filter(tiles, ""), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(TileSetQueries.Filter(tiles, "lake"), Is.Empty);
        }
    }
}
