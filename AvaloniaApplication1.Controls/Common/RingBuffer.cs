using System;
using System.Collections;
using System.Collections.Generic;

namespace AvaloniaApplication1.Models.Common
{
    /// <summary>
    /// High-performance circular buffer with pre-allocated array and zero heap allocations on push and indexer access.
    /// Index 0 corresponds to the oldest item; Count - 1 corresponds to the newest item.
    /// </summary>
    public class RingBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _buffer;
        private int _start;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            _buffer = new T[capacity];
        }

        public void Push(T item)
        {
            if (_count < _buffer.Length)
            {
                int index = (_start + _count) % _buffer.Length;
                _buffer[index] = item;
                _count++;
            }
            else
            {
                _buffer[_start] = item;
                _start = (_start + 1) % _buffer.Length;
            }
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_start + index) % _buffer.Length];
            }
            set
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _buffer[(_start + index) % _buffer.Length] = value;
            }
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        public void PreFill(T value, int count = -1)
        {
            int toFill = count < 0 ? _buffer.Length : Math.Min(count, _buffer.Length);
            for (int i = 0; i < toFill; i++)
            {
                _buffer[i] = value;
            }
            _start = 0;
            _count = toFill;
        }

        public T? Latest => _count > 0 ? this[_count - 1] : default;
        public T? Oldest => _count > 0 ? this[0] : default;

        public struct Enumerator : IEnumerator<T>
        {
            private readonly RingBuffer<T> _buffer;
            private int _index;
            private T? _current;

            internal Enumerator(RingBuffer<T> buffer)
            {
                _buffer = buffer;
                _index = 0;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_index < _buffer.Count)
                {
                    _current = _buffer[_index];
                    _index++;
                    return true;
                }
                return false;
            }

            public T Current => _current!;
            object? IEnumerator.Current => Current;
            public void Reset() { _index = 0; _current = default; }
            public void Dispose() { }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
