// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXAssembly.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Collection of PTX modules that are used to build a Cuda kernel.
    /// </summary>
    public sealed class PTXAssembly
    {
        #region Nested Types

        /// <summary>
        /// A builder for a collection of PTX modules.
        /// </summary>
        public class Builder
        {
            #region Instance

            /// <summary>
            /// List of PTX modules.
            /// </summary>
            private readonly ImmutableArray<string>.Builder modules;

            /// <summary>
            /// Constructs a new builder.
            /// </summary>
            internal Builder()
            {
                KernelBuilder = new StringBuilder();
                modules = ImmutableArray.CreateBuilder<string>(1);

                // Add placeholder for kernel module.
                modules.Add(string.Empty);
            }

            #endregion

            #region Properties

            /// <summary>
            /// Contains the definition of the kernel module.
            /// </summary>
            public StringBuilder KernelBuilder { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Adds the PTX modules to the collection.
            /// </summary>
            public void AddModule(ReadOnlySpan<string> ptxModules) =>
#if NET7_0_OR_GREATER
                modules.AddRange(ptxModules);
#else
                modules.AddRange(ptxModules.ToArray());
#endif

            /// <summary>
            /// Constructs the completed collection of PTX modules.
            /// </summary>
            public PTXAssembly Seal()
            {
                // Replace placeholder string, so that the kernel is always at index 0.
                modules[0] = KernelBuilder.ToString();
                return new PTXAssembly(modules.ToImmutable());
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Collection of PTX modules.
        /// </summary>
        public ImmutableArray<string> Modules { get; }

        /// <summary>
        /// Constructs the list of PTX modules.
        /// </summary>
        internal PTXAssembly(ImmutableArray<string> modules)
        {
            Modules = modules;
        }

        #endregion
    }
}
