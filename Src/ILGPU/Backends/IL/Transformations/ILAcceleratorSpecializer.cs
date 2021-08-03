// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ILAcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;

namespace ILGPU.Backends.IL.Transformations
{
    /// <summary>
    /// The IL accelerator specializer.
    /// </summary>
    public sealed class ILAcceleratorSpecializer : AcceleratorSpecializer
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL accelerator specializer.
        /// </summary>
        /// <param name="pointerType">The actual pointer type to use.</param>
        /// <param name="warpSize">The warp size to use.</param>
        /// <param name="enableAssertions">True, if the assertions are enabled.</param>
        public ILAcceleratorSpecializer(
            PrimitiveType pointerType,
            int warpSize,
            bool enableAssertions)
            : base(
                  AcceleratorType.CPU,
                  warpSize,
                  pointerType,
                  enableAssertions)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Keeps the debug assertion operation.
        /// </summary>
        protected override void Specialize(
            in RewriterContext context,
            IRContext irContext,
            DebugAssertOperation debugAssert)
        { }

        /// <summary>
        /// Keeps the IO operation.
        /// </summary>
        protected override void Specialize(
            in RewriterContext context,
            IRContext irContext,
            WriteToOutput writeToOutput)
        { }

        #endregion
    }
}
