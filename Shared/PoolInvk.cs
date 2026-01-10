using Microsoft.IO;

namespace Shared
{
    public static class PoolInvk
    {
        public static readonly RecyclableMemoryStreamManager msm = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
        (
            blockSize: 32 * 1024,                          // small blocks (32 КБ)
            largeBufferMultiple: 128 * 1024,               // ступень роста 128 КБ
            maximumBufferSize: 8 * 1024 * 1024,            // не кешируем >8 MB
            maximumSmallPoolFreeBytes: 64L * 1024 * 1024,  // максимальный размер пула small blocks (64 MB)
            maximumLargePoolFreeBytes: 128L * 1024 * 1024  // общий размер пула largeBufferMultiple/maximumBufferSize (128 MB)
        )
        {
            AggressiveBufferReturn = false
        });


        public static int bufferSize => 16 * 1024;

        public static int rentChunk => 8 * 1024;

        public static int rentLargeChunk => 64 * 1024;

        public static int rentMax => 5 * 1024 * 1024;


        static readonly int[] sizesRent =
        {
            16 * 1024,
            32 * 1024,
            64 * 1024,
            128 * 1024,
            256 * 1024,
            512 * 1024,
            1024 * 1024,
            2 * 1024 * 1024,
            5 * 1024 * 1024
        };

        public static int Rent(int length)
        {
            if (rentChunk >= length)
                return rentChunk;

            for (int i = 0; i < sizesRent.Length; i++)
            {
                if (sizesRent[i] >= length)
                    return sizesRent[i];
            }

            throw new ArgumentException("length > 5 MB");
        }
    }
}
