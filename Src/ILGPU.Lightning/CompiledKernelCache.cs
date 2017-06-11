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
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a cache for compiled kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class CompiledKernelCache : LightningContextObject
    {
        #region Instance

        private readonly Dictionary<MethodInfo, CompiledKernel> cache =
            new Dictionary<MethodInfo, CompiledKernel>();

        /// <summary>
        /// Constructs a new lightning kernel cache for lightning kernels.
        /// </summary>
        /// <param name="lightningContext">The current lightning context.</param>
        internal CompiledKernelCache(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
        }

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="method">The method to compile into a <see cref="CompiledKernel"/> .</param>
        /// <returns>The compiled kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompiledKernel CompileKernel(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (!cache.TryGetValue(method, out CompiledKernel result))
            {
                result = LightningContext.Backend.Compile(
                    LightningContext.CompileUnit, method);
                cache.Add(method, result);
            }
            return result;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            ClearCache();
        }

        #endregion
    }
}
