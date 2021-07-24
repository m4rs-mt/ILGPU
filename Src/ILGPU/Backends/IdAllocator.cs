using ILGPU.IR;
using System.Collections.Generic;

namespace ILGPU.Backends
{
    /// <summary>
    /// An allocator that can assign unique IDs to values.
    /// </summary>
    public abstract class IdAllocator<T>
    {
        #region Instance

        private readonly Dictionary<T, uint> lookup =
            new Dictionary<T, uint>();

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a new variable.
        /// </summary>
        /// <returns>The allocated variable.</returns>
        public uint Allocate(T value)
        {
            if (lookup.TryGetValue(value, out uint id))
                return id;
            id = NextId();
            lookup.Add(value, id);
            return id;
        }

        /// <summary>
        /// Provides the next Id.
        /// </summary>
        /// <returns>The next Id.</returns>
        protected abstract uint NextId();

        /// <summary>
        /// Loads the given node.
        /// </summary>
        /// <param name="node">The node to load.</param>
        /// <returns>The loaded variable.</returns>
        public uint Load(T node) =>
            lookup[node];

        #endregion
    }
}
