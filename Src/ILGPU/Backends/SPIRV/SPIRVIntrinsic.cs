using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using System;
using System.Reflection;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Represents a specific handler for user defined code-generation functionality
    /// that is compatible with the <see cref="SPIRVBackend"/>.
    /// </summary>
    public class SPIRVIntrinsic : IntrinsicImplementation
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            SPIRVBackend backend,
            SPIRVCodeGenerator codeGenerator,
            Value value);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new SPIR-V intrinsic.
        /// </summary>
        /// <param name="targetMethod">The associated target method.</param>
        /// <param name="mode">The code-generation mode.</param>
        public SPIRVIntrinsic(MethodInfo targetMethod, IntrinsicImplementationMode mode)
            : base(
                  BackendType.SPRIV,
                  targetMethod,
                  mode)
        { }

        /// <summary>
        /// Constructs a new SPIR-V intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="mode">The code-generation mode.</param>
        public SPIRVIntrinsic(Type handlerType, IntrinsicImplementationMode mode)
            : base(
                  BackendType.SPRIV,
                  handlerType,
                  null,
                  mode)
        { }

        /// <summary>
        /// Constructs a new SPIR-V intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        public SPIRVIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode)
            : base(
                  BackendType.OpenCL,
                  handlerType,
                  methodName,
                  mode)
        { }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
        protected internal override bool CanHandleBackend(Backend backend) =>
            backend is SPIRVBackend;

        #endregion
    }
}
