// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: CompiledKernelCache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a cache for compiled kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [Obsolete("This class will not be available in the future. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
    public sealed class CompiledKernelCache
    {
        #region Instance

        /// <summary>
        /// Constructs a new lightning kernel cache for lightning kernels.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        internal CompiledKernelCache(Accelerator accelerator)
        {
            Accelerator = accelerator;
        }

        #endregion

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        #region Methods

        /// <summary>
        /// Clears the cache.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "The method has to be an instance method in order to ensure backwards compatibility")]
        [Obsolete("This method will not be available in the future. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public void ClearCache()
        { }

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="method">The method to compile into a <see cref="CompiledKernel"/> .</param>
        /// <returns>The compiled kernel.</returns>
        [Obsolete("Use Accelerator.CompileKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public CompiledKernel CompileKernel(MethodInfo method)
        {
            return Accelerator.CompileKernel(method);
        }

        #endregion
    }
}
