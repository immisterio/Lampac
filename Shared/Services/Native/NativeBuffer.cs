using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shared.Services.Native;

public static class NativeBufferStats
{
    private static long _created;
    private static long _disposed;

    public static long Created
        => Volatile.Read(ref _created);

    public static long Disposed
        => Volatile.Read(ref _disposed);

    internal static void IncrementCreated()
        => Interlocked.Increment(ref _created);

    internal static void IncrementDisposed()
        => Interlocked.Increment(ref _disposed);
}

public sealed unsafe class NativeBuffer<T> : MemoryManager<T> where T : unmanaged
{
    private IntPtr _ptr;
    private int _len;

    private int _disposed;
    private int _pinCount;
    private DateTimeOffset _expires = default;

    public bool IsExpires
        => _expires != default && DateTimeOffset.UtcNow > _expires;

    private bool IsDisposed
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
                return true;

            return Interlocked.CompareExchange(ref _ptr, IntPtr.Zero, IntPtr.Zero) == IntPtr.Zero;
        }
    }

    public NativeBuffer(int len)
    {
        if (len <= 0)
            throw new ArgumentOutOfRangeException(nameof(len));

        _len = len;

        nuint bytes;
        try
        {
            bytes = checked((nuint)len * (nuint)sizeof(T));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(len), len, "Requested buffer size is too large.");
        }

        _ptr = (IntPtr)NativeMemory.Alloc(bytes);

        if (_ptr == IntPtr.Zero)
            throw new OutOfMemoryException();

        if (CoreInit.conf.pool.BufferValidityMinutes > 0)
            _expires = DateTimeOffset.UtcNow.AddMinutes(CoreInit.conf.pool.BufferValidityMinutes);

        NativeBufferStats.IncrementCreated();
    }

    public override Span<T> GetSpan()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(NativeBuffer<T>));

        return new Span<T>((void*)_ptr, _len);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(NativeBuffer<T>));

        if ((uint)elementIndex > (uint)_len)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        Interlocked.Increment(ref _pinCount);

        void* p = (byte*)_ptr + (nuint)elementIndex * (nuint)sizeof(T);
        return new MemoryHandle(p, pinnable: this);
    }

    public override void Unpin()
    {
        int value = Interlocked.Decrement(ref _pinCount);
        if (value < 0)
        {
            Interlocked.Increment(ref _pinCount);
            throw new InvalidOperationException("Unbalanced Unpin.");
        }
    }

    public void Ensure(int sizeHint)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(NativeBuffer<T>));

        if (sizeHint <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        if (sizeHint <= _len)
            return;

        if (Volatile.Read(ref _pinCount) != 0)
            throw new InvalidOperationException("Buffer is pinned.");

        nuint bytes;
        try
        {
            bytes = checked((nuint)sizeHint * (nuint)sizeof(T));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "Requested buffer size is too large.");
        }

        void* newPtr = NativeMemory.Realloc((void*)_ptr, bytes);
        if (newPtr == null)
            throw new OutOfMemoryException();

        _ptr = (IntPtr)newPtr;
        _len = sizeHint;
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (Volatile.Read(ref _pinCount) != 0)
        {
            Volatile.Write(ref _disposed, 0);
            throw new InvalidOperationException("Buffer is pinned.");
        }

        IntPtr ptr = Interlocked.Exchange(ref _ptr, IntPtr.Zero);

        if (ptr != IntPtr.Zero)
        {
            NativeMemory.Free((void*)ptr);
            NativeBufferStats.IncrementDisposed();
        }
    }
}
