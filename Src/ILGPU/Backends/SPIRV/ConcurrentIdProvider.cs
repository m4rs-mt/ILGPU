using System.Threading;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Provides IDs in a thread safe fashion.
    /// </summary>
    public class ConcurrentIdProvider
    {
        private long _id = 0;

        /// <summary>
        /// Atomically calculates and returns the next ID.
        /// </summary>
        /// <returns>The next ID.</returns>
        public uint Next() => (uint)Interlocked.Increment(ref _id);
    }
}
