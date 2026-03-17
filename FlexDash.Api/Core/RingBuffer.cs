namespace FlexDash.Api.Core;

internal sealed class RingBuffer<T>(int capacity) {
    private readonly T[] _buffer = new T[capacity];
    private readonly Lock _lock = new();
    private int _head;
    private int _count;

    public void AddRange(IEnumerable<T> items) {
        lock (_lock) {
            foreach (T item in items) {
                _buffer[_head] = item;
                _head = (_head + 1) % capacity;
                if (_count < capacity) {
                    _count++;
                }
            }
        }
    }

    public List<T> ToList() {
        lock (_lock) {
            var result = new List<T>(_count);
            int start = _count < capacity ? 0 : _head;
            for (int i = 0; i < _count; i++) {
                result.Add(_buffer[(start + i) % capacity]);
            }
            return result;
        }
    }

    public List<T> Where(Func<T, bool> predicate) {
        lock (_lock) {
            var result = new List<T>();
            int start = _count < capacity ? 0 : _head;
            for (int i = 0; i < _count; i++) {
                T item = _buffer[(start + i) % capacity];
                if (predicate(item)) {
                    result.Add(item);
                }
            }
            return result;
        }
    }

    public bool Any(Func<T, bool> predicate) {
        lock (_lock) {
            int start = _count < capacity ? 0 : _head;
            for (int i = 0; i < _count; i++) {
                if (predicate(_buffer[(start + i) % capacity])) {
                    return true;
                }
            }
            return false;
        }
    }
}
