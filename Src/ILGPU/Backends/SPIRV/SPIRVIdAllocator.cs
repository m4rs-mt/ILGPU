namespace ILGPU.Backends.SPIRV
{
    /// <inheritdoc />
    public class SPIRVIdAllocator : IdAllocator
    {
        #region Instance

        private readonly ConcurrentIdProvider _provider;

        /// <summary>
        /// Creates an ID allocator that uses the given provider to generate new IDs.
        /// </summary>
        /// <param name="provider">The provider used to generate new IDs.</param>
        public SPIRVIdAllocator(ConcurrentIdProvider provider)
        {
            _provider = provider;
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        protected override uint NextId() => _provider.Next();

        #endregion
    }
}
