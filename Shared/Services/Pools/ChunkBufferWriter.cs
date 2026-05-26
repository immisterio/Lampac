using System.Buffers;
using System.Runtime.CompilerServices;

namespace Shared.Services.Pools;

public class ChunkBufferWriter<T> : IBufferWriter<T> where T : struct
{
    readonly IBufferWriter<T> writer;

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
        <= PoolInvk._chunk4 => PoolInvk._chunk4,
        <= PoolInvk._chunk8 => PoolInvk._chunk8,
        <= PoolInvk._chunk16 => PoolInvk._chunk16,
        <= PoolInvk._chunk32 => PoolInvk._chunk32,
        <= PoolInvk.msmBlockSize => PoolInvk.msmBlockSize,
        _ => sizeHint
    };
}
