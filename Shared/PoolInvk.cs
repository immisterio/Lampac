using Microsoft.IO;

namespace Shared
{
    public static class PoolInvk
    {
        public static readonly RecyclableMemoryStreamManager msm = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
        (
            blockSize: 512 * 1024,                         // small blocks (512 КБ)
            largeBufferMultiple: 4 * 1024 * 1024,          // ступень роста если blockSize недостаточно
            maximumBufferSize: 8 * 1024 * 1024,            // не кешируем >8 MB
            maximumSmallPoolFreeBytes: 128L * 1024 * 1024, // хранить не более 256 small blocks (128 MB)
            maximumLargePoolFreeBytes: 128L * 1024 * 1024  // общий размер блоков largeBufferMultiple/maximumBufferSize (128 MB)
        )
        {
            AggressiveBufferReturn = true
        });


        /// <summary>
        /// предварительный буфер пишется в msm, поэтому ориентируемся на его размеры 
        /// </summary>
        public static int MemoryOwner(int length)
        {
            int blockSize = 512 * 1024;
            if (blockSize > length)
                return blockSize;

            int largeSize = 4 * 1024 * 1024;
            if (largeSize > length)
                return largeSize;

            int maximumSize = 8 * 1024 * 1024;
            if (maximumSize > length)
                return maximumSize;

            throw new ArgumentException("length > 8 MB");
        }


        public static int bufferSize => 16 * 1024;

        public static int rentChunk => 8 * 1024;


        public static int Rent(int length)
        {
            if (rentChunk >= length)
                return rentChunk;

            int smallSize = 256 * 1024;
            if (smallSize >= length)
                return smallSize;

            int blockSize = 512 * 1024;
            if (blockSize > length)
                return blockSize;

            int largeSize = 4 * 1024 * 1024;
            if (largeSize > length)
                return largeSize;

            int maximumSize = 8 * 1024 * 1024;
            if (maximumSize > length)
                return maximumSize;

            throw new ArgumentException("length > 8 MB");
        }
    }
}
