using ILGPU.Backends.EntryPoints;

namespace ILGPU.Backends.SPIRV
{
    public sealed class SPIRVCompiledKernel : CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel in PTX form.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="info">Detailed kernel information.</param>
        /// <param name="ptxAssembly">The assembly code.</param>
        internal SPIRVCompiledKernel(
            Context context,
            EntryPoint entryPoint,
            KernelInfo info,
            byte[] byteCode)
            : base(context, entryPoint, info)
        {
            SPIRVByteCode = byteCode;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the PTX assembly code.
        /// </summary>
        public byte[] SPIRVByteCode { get; }

        #endregion
    }
}
