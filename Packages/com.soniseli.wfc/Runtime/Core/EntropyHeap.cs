namespace WfcStudio.Core
{
    /// <summary>
    /// Indexed binary min-heap of cells by key, for finding the lowest-entropy cell without scanning the grid.
    /// Equal keys go to the lower cell index, so it picks exactly the cell a front-to-back scan with a strict
    /// "less than" would. Fixed capacity: nothing allocates after construction.
    /// </summary>
    internal sealed class EntropyHeap
    {
        readonly int[] _heap;
        readonly int[] _position;
        readonly double[] _key;
        int _count;

        public EntropyHeap(int cellCount)
        {
            _heap = new int[cellCount];
            _position = new int[cellCount];
            _key = new double[cellCount];
            for (int cell = 0; cell < cellCount; cell++)
                _position[cell] = -1;
        }

        public int Count => _count;

        /// <summary>The cell with the lowest key. Only valid while <see cref="Count"/> is above zero.</summary>
        public int Min => _heap[0];

        public bool Contains(int cell)
        {
            return _position[cell] >= 0;
        }

        /// <summary>Adds the cell, or moves it if it is already in the heap.</summary>
        public void Set(int cell, double key)
        {
            int index = _position[cell];
            if (index < 0)
            {
                _key[cell] = key;
                index = _count++;
                Place(cell, index);
                SiftUp(index);
                return;
            }

            double old = _key[cell];
            _key[cell] = key;
            if (key < old)
                SiftUp(index);
            else if (key > old)
                SiftDown(index);
        }

        public void Remove(int cell)
        {
            int index = _position[cell];
            if (index < 0)
                return;

            _position[cell] = -1;
            int last = _heap[--_count];
            if (index == _count)
                return;

            Place(last, index);
            SiftUp(index);
            SiftDown(_position[last]);
        }

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
                _position[_heap[i]] = -1;
            _count = 0;
        }

        bool Less(int a, int b)
        {
            double keyA = _key[a], keyB = _key[b];
            return keyA < keyB || (keyA == keyB && a < b);
        }

        void SiftUp(int index)
        {
            int cell = _heap[index];
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (!Less(cell, _heap[parent]))
                    break;
                Place(_heap[parent], index);
                index = parent;
            }
            Place(cell, index);
        }

        void SiftDown(int index)
        {
            int cell = _heap[index];
            while (true)
            {
                int child = 2 * index + 1;
                if (child >= _count)
                    break;
                if (child + 1 < _count && Less(_heap[child + 1], _heap[child]))
                    child++;
                if (!Less(_heap[child], cell))
                    break;
                Place(_heap[child], index);
                index = child;
            }
            Place(cell, index);
        }

        void Place(int cell, int index)
        {
            _heap[index] = cell;
            _position[cell] = index;
        }
    }
}
