using System.Collections.Generic;

namespace QuantumServerClient
{
    public class PingQueue
    {
        private Queue<int> _queue;
        private int _maxSize;

        public PingQueue(int maxSize)
        {
            _queue = new Queue<int>(maxSize);
            _maxSize = maxSize;
        }

        public int Count => _queue.Count;
        
        public void Enqueue(int value)
        {
            if (_queue.Count >= _maxSize)
            {
                _queue.Dequeue();
            }
            _queue.Enqueue(value);
        }

        public int[] GetQueue()
        {
            return _queue.ToArray();
        }
        
        public void Clear() => _queue.Clear();
    }
}