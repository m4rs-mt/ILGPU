namespace ILGPU.Backends.SPIRV
{
    public class SPIRVVariableAllocator : VariableAllocator
    {
        #region Instance

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="typeGenerator">The associated type generator.</param>
        public SPIRVVariableAllocator(SPRIVTypeGenerator typeGenerator)
        {
            TypeGenerator = typeGenerator;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type generator.
        /// </summary>
        public SPRIVTypeGenerator TypeGenerator { get; }

        #endregion
    }
}
