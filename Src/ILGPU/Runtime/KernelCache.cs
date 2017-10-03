// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelCache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Kernel Cache

        /// <summary>
        /// A cached kernel key.
        /// </summary>
        private struct CachedKernelKey : IEquatable<CachedKernelKey>
        {
            #region Instance

            /// <summary>
            /// Constructs a new kernel key.
            /// </summary>
            /// <param name="method">The kernel method.</param>
            /// <param name="implicitGroupSize">The implicit group size (if any).</param>
            public CachedKernelKey(MethodInfo method, int? implicitGroupSize)
            {
                Method = method;
                ImplicitGroupSize = implicitGroupSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated kernel method.
            /// </summary>
            public MethodInfo Method { get; }

            /// <summary>
            /// Returns the associated implicit group size (if any).
            /// </summary>
            public int? ImplicitGroupSize { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true iff the given cached key is equal to the current one.
            /// </summary>
            /// <param name="key">The other key.</param>
            /// <returns>True, iff the given cached key is equal to the current one.</returns>
            public bool Equals(CachedKernelKey key)
            {
                return key.Method == Method &&
                    key.ImplicitGroupSize == ImplicitGroupSize;
            }

            #endregion

            #region Object

            public override int GetHashCode()
            {
                return Method.GetHashCode() ^ (ImplicitGroupSize ?? 1);
            }

            public override bool Equals(object obj)
            {
                if (obj is CachedKernelKey)
                    return Equals((CachedKernelKey)obj);
                return false;
            }

            public override string ToString()
            {
                return Method.ToString();
            }

            #endregion
        }

        /// <summary>
        /// A cached kernel.
        /// </summary>
        private struct CachedKernel
        {
            #region Instance

            /// <summary>
            /// Constructs a new cached kernel.
            /// </summary>
            /// <param name="kernel">The kernel to cache.</param>
            public CachedKernel(Kernel kernel)
            {
                Kernel = kernel;
                GroupSize = 0;
                MinGridSize = 0;
            }

            /// <summary>
            /// Constructs a new cached kernel.
            /// </summary>
            /// <param name="kernel">The kernel to cache.</param>
            /// <param name="groupSize">The computed group size.</param>
            /// <param name="minGridSize">The computed minimum grid size.</param>
            public CachedKernel(
                Kernel kernel,
                int groupSize,
                int minGridSize)
            {
                Kernel = kernel;
                GroupSize = groupSize;
                MinGridSize = minGridSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the cached kernel.
            /// </summary>
            public Kernel Kernel { get; }

            /// <summary>
            /// Returns the computed group size.
            /// </summary>
            public int GroupSize { get; }

            /// <summary>
            /// Returns the computed minimum grid size.
            /// </summary>
            public int MinGridSize { get; }

            #endregion
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<MethodInfo, CompiledKernel> compiledKernelCache =
            new Dictionary<MethodInfo, CompiledKernel>();

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="method">The method to compile into a <see cref="CompiledKernel"/> .</param>
        /// <returns>The compiled kernel.</returns>
        public CompiledKernel CompileKernel(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (!compiledKernelCache.TryGetValue(method, out CompiledKernel result))
            {
                result = Backend.Compile(CompileUnit, method);
                compiledKernelCache.Add(method, result);
            }
            return result;
        }

        #endregion
    }
}
