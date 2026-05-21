using Shared.Services.Native;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services.Pools;

public enum BufferWriterPoolType
{
    Tiny = 1,
    Small = 2,
    Large = 3
}

public sealed class BufferWriterPool<T> : IBufferWriter<T>, IDisposable where T : unmanaged
{
    #region pool
    public const int sizeTiny = 128 * 1024;
    public const int sizePool = 1 * 1024 * 1024;
    public const int sizeLargePool = 10 * 1024 * 1024;

    static readonly ConcurrentDictionary<byte, BufferPoolInfo<T>> pool = new ConcurrentDictionary<byte, BufferPoolInfo<T>>
    {
        [1] = new BufferPoolInfo<T>(sizeTiny, 100),
        [2] = new BufferPoolInfo<T>(sizePool, CoreInit.conf.pool.BufferWriterSmallMaxCount),
        [3] = new BufferPoolInfo<T>(sizeLargePool, CoreInit.conf.pool.BufferWriterLargeMaxCount)
    };
    #endregion

    #region OpenStat
    public static long FreeTiny
        => pool[1].currentCount;

    public static long Free
        => pool[2].currentCount;

    public static long FreeLarge
        => pool[3].currentCount;

    public static long DisposeCount
        => pool.Sum(i => i.Value.disposeCount);
    #endregion

    private NativeBuffer<T> _nbuf;
    private int _index;
    private int _disposed;
    private BufferWriterPoolType _type;

    public BufferWriterPool(BufferWriterPoolType type = BufferWriterPoolType.Small)
    {
        _type = type;
        if (CoreInit.conf.lowMemoryMode == false && type == BufferWriterPoolType.Large)
            _type = BufferWriterPoolType.Small;
    }

    public ReadOnlySpan<T> WrittenSpan
        => _nbuf.Memory.Span.Slice(0, _index);

    public ReadOnlyMemory<T> WrittenMemory
        => _nbuf.Memory.Slice(0, _index);

    public int WrittenCount => _index;

    public void Advance(int count)
    {
        _index += count;
    }

    public void SetLength(int index)
    {
        _index = index;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _nbuf.Memory.Slice(_index);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _nbuf.Memory.Span.Slice(_index);
    }

    private void Ensure(int sizeHint)
    {
        if (sizeHint <= 0)
            sizeHint = 1;

        if (_nbuf == null)
        {
            const int minSize = 128 * 1024;

            if (minSize > sizeHint)
                sizeHint = minSize;

            var p = pool[(byte)_type];
            _nbuf = p.Rent(sizeHint);
        }

        int newsize = _index + sizeHint;
        if (newsize <= _nbuf.Memory.Length)
            return;

        _nbuf.Ensure(newsize * 2);
    }

    public void Dispose()
    {
        if (_nbuf == null || Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var p = pool[(byte)_type];
        p.Return(_nbuf);
    }
}
