using Microsoft.IO;

namespace Shared
{
    public static class PoolInvk
    {
        public static readonly RecyclableMemoryStreamManager msm = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
        (
            blockSize: 32 * 1024,                         // small blocks (32 КБ)
            largeBufferMultiple: 2 * 1024 * 1024,         // ступень роста 2 MB
            maximumBufferSize: 4 * 1024 * 1024,           // не кешируем >4 MB
            maximumSmallPoolFreeBytes: 64L * 1024 * 1024, // максимальный размер пула small blocks (128 MB)
            maximumLargePoolFreeBytes: 32L * 1024 * 1024  // общий размер пула largeBufferMultiple/maximumBufferSize (64 MB)
        )
        {
            AggressiveBufferReturn = false
        });


        public static int bufferSize => 16 * 1024;

        public static int rentChunk => 8 * 1024;

        public static int rentLargeChunk => 32 * 1024;

        public static int rentMax => 5 * 1024 * 1024;

        public static int rentCharMax => rentMax / 2;


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
            4 * 1024 * 1024
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

            throw new ArgumentException("large rent");
        }
    }
}
