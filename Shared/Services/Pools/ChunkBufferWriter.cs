using System.Buffers;
using System.Runtime.CompilerServices;

namespace Shared.Services.Pools;

public class ChunkBufferWriter<T> : IBufferWriter<T> where T : struct
{
    readonly IBufferWriter<T> writer;
    const int _chunk4 = 4 * 1024;
    const int _chunk16 = 16 * 1024;
    const int _chunk32 = 32 * 1024;
    const int _chunk64 = 64 * 1024;
    const int _chunk128 = 128 * 1024;

    public ChunkBufferWriter(IBufferWriter<T> writer)
    {
        this.writer = writer;
    }

    public void Advance(int count)
    {
        writer.Advance(count);
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        return writer.GetMemory(ChunkSizeHint(sizeHint));
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return writer.GetSpan(ChunkSizeHint(sizeHint));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ChunkSizeHint(int sizeHint) => sizeHint switch
    {
        <= _chunk4 => _chunk4,
        <= _chunk16 => _chunk16,
        <= _chunk32 => _chunk32,
        <= _chunk64 => _chunk64,
        _ => _chunk128
    };
}
