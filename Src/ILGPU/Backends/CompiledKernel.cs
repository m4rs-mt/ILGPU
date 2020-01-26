// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a compiled kernel that encapsulates emitted binary code.
    /// </summary>
    public abstract class CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        protected CompiledKernel(Context context, EntryPoint entryPoint)
        {
            Context = context;
            EntryPoint = entryPoint;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Represents the source method.
        /// </summary>
        public MethodInfo SourceMethod => EntryPoint.MethodInfo;

        /// <summary>
        /// Returns the index type of the entry point.
        /// </summary>
        public IndexType IndexType => EntryPoint.IndexType;

        /// <summary>
        /// Returns the associated kernel specialization.
        /// </summary>
        public KernelSpecialization Specialization => EntryPoint.Specialization;

        /// <summary>
        /// Returns the internally used entry point.
        /// </summary>
        internal EntryPoint EntryPoint { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this kernel.
        /// </summary>
        /// <returns>The string representation of this kernel.</returns>
        public override string ToString()
        {
            return $"{SourceMethod}[Specialization: {Specialization}]";
        }

        #endregion
    }
}
