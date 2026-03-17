using System.Collections.Concurrent;

namespace FlexDash.Api.Core;

public sealed class DataPointBuffer {
    private const int DefaultCapacity = 100;
    private readonly int _capacity;
    private readonly ConcurrentDictionary<Guid, RingBuffer<DataPointDto>> _data = [];

    public DataPointBuffer(int capacity = DefaultCapacity) {
        _capacity = capacity;
    }

    public void Add(Guid sourceId, IEnumerable<DataPointDto> points) {
        RingBuffer<DataPointDto> ring = _data.GetOrAdd(sourceId, _ => new RingBuffer<DataPointDto>(_capacity));
        ring.AddRange(points);
    }

    public List<DataPointDto> Get(Guid sourceId) {
        return _data.TryGetValue(sourceId, out var ring) ? ring.ToList() : [];
    }

    public List<DataPointDto> GetSince(Guid sourceId, DateTime since) {
        return _data.TryGetValue(sourceId, out var ring) ? ring.Where(point => point.Timestamp >= since) : [];
    }

    public bool HasDataSince(Guid sourceId, DateTime since) {
        return _data.TryGetValue(sourceId, out var ring) && ring.Any(point => point.Timestamp >= since);
    }
}
