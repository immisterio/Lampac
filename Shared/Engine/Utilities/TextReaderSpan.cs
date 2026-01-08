using System.Buffers;
using System.Threading;

namespace Shared.Engine
{
    public static class TextReaderSpan
    {
        public static async Task<(bool success, int Length)> ReadAllCharsAsync(
            IMemoryOwner<char> memoryOwner,
            TextReader reader,
            CancellationToken cancellationToken = default)
        {
            if (reader is null) 
                throw new ArgumentNullException(nameof(reader));

            const int readChunkSize = 64 * 1024;
            char[] chunk = ArrayPool<char>.Shared.Rent(readChunkSize);

            Memory<char> dest = memoryOwner.Memory;
            int total = 0;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if ((total + readChunkSize) > dest.Length)
                        return (false, 0);

                    int read = await reader.ReadAsync(chunk, 0, readChunkSize).ConfigureAwait(false);
                    if (read == 0) 
                        break;

                    chunk.CopyTo(dest.Span.Slice(total));
                    total += read;
                }

                return (true, total);
            }
            catch
            {
                return (false, 0);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chunk);
            }
        }
    }
}
