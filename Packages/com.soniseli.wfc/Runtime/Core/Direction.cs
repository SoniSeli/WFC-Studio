namespace WfcStudio.Core
{
    /// <summary>The four grid directions. Up is +y, matching Unity's Tilemap.</summary>
    public enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    public static class DirectionExtensions
    {
        public const int Count = 4;

        public static Direction Opposite(this Direction direction)
        {
            return (Direction)(((int)direction + 2) & 3);
        }

        public static int OffsetX(this Direction direction)
        {
            return direction == Direction.Right ? 1 : direction == Direction.Left ? -1 : 0;
        }

        public static int OffsetY(this Direction direction)
        {
            return direction == Direction.Up ? 1 : direction == Direction.Down ? -1 : 0;
        }
    }
}
