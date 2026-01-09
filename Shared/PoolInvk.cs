using Microsoft.IO;

namespace Shared
{
    public static class PoolInvk
    {
        public static readonly RecyclableMemoryStreamManager msm = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
        (
            blockSize: 512 * 1024,                         // small blocks (512 КБ)
            largeBufferMultiple: 2 * 1024 * 1024,          // ступень роста если blockSize недостаточно
            maximumBufferSize: 8 * 1024 * 1024,            // не кешируем >8 MB
            maximumSmallPoolFreeBytes: 256L * 1024 * 1024, // хранить не более 512 small blocks (256 MB)
            maximumLargePoolFreeBytes: 256L * 1024 * 1024  // общий размер блоков largeBufferMultiple/maximumBufferSize (256 MB)
        )
        {
            AggressiveBufferReturn = true
        });


        static readonly int[] sizesMemoryOwner =
        {
            512 * 1024,
            2 * 1024 * 1024,
            4 * 1024 * 1024,
            6 * 1024 * 1024,
            8 * 1024 * 1024,
            10 * 1024 * 1024
        };

        public static int MemoryOwner(int length)
        {
            for (int i = 0; i < sizesMemoryOwner.Length; i++)
            {
                if (sizesMemoryOwner[i] >= length)
                    return sizesMemoryOwner[i];
            }

            throw new ArgumentException("length > 10 MB");
        }


        public static int bufferSize => 16 * 1024;

        public static int rentChunk => 8 * 1024;


        public static int Rent(int length)
        {
            if (rentChunk >= length)
                return rentChunk;

            int smallSize = 64 * 1024;
            if (smallSize >= length)
                return smallSize;

            smallSize = 128 * 1024;
            if (smallSize >= length)
                return smallSize;

            int blockSize = 512 * 1024;
            if (blockSize >= length)
                return blockSize;

            int largeSize = 2 * 1024 * 1024;
            if (largeSize >= length)
                return largeSize;

            int maximumSize = 5 * 1024 * 1024;
            if (maximumSize >= length)
                return maximumSize;

            throw new ArgumentException("length > 5 MB");
        }
    }
}
