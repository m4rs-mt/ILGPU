// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.Runtime;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a compiled kernel that encapsulates emitted binary code.
    /// </summary>
    public abstract class CompiledKernel
    {
        #region Nested Types

        /// <summary>
        /// Contains information about functions.
        /// </summary>
        public readonly struct FunctionInfo
        {
            #region Instance

            /// <summary>
            /// Constructs a new function information object.
            /// </summary>
            public FunctionInfo(
                string name,
                MethodBase method,
                int localMemorySize)
            {
                if (localMemorySize < 0)
                    throw new ArgumentOutOfRangeException(nameof(localMemorySize));

                Name = name ?? throw new ArgumentNullException(nameof(name));
                Method = method;
                LocalMemorySize = localMemorySize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// The name of the compiled function inside the kernel.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Returns the managed method reference (if any).
            /// </summary>
            public MethodBase Method { get; }

            /// <summary>
            /// Returns the local memory size in bytes.
            /// </summary>
            public int LocalMemorySize { get; }

            #endregion
        }

        /// <summary>
        /// Provides detailed information about compiled kernels.
        /// </summary>
        public class KernelInfo : IDumpable
        {
            #region Instance

            /// <summary>
            /// Constructs a new kernel information object.
            /// </summary>
            /// <param name="sharedAllocations">All shared allocations.</param>
            /// <param name="functions">
            /// An array containing detailed function information.
            /// </param>
            public KernelInfo(
                in AllocaKindInformation sharedAllocations,
                ImmutableArray<FunctionInfo> functions)
            {
                SharedAllocations = sharedAllocations;
                Functions = functions;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns detailed information about all shared allocations.
            /// </summary>
            /// <remarks>
            /// This information will be populated if the property
            /// <see cref="ContextProperties.EnableKernelInformation"/> is enabled.
            /// </remarks>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns information about all functions in the compiled kernel.
            /// </summary>
            /// <remarks>
            /// This array will be populated if the property
            /// <see cref="ContextProperties.EnableKernelInformation"/> is enabled.
            /// </remarks>
            public ImmutableArray<FunctionInfo> Functions { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Dumps kernel information to the given text writer.
            /// </summary>
            /// <param name="writer">The text writer.</param>
            public virtual void Dump(TextWriter writer)
            {
                if (writer == null)
                    throw new ArgumentNullException(nameof(writer));

                // Shared memory
                if (SharedAllocations.TotalSize > 0)
                {
                    writer.WriteLine("Shared Memory:");
                    writer.Write("\tTotal Size: ");
                    writer.Write(SharedAllocations.TotalSize);
                    writer.WriteLine(" bytes");

                    foreach (var alloc in SharedAllocations)
                    {
                        writer.Write("\t");
                        writer.Write(alloc.ElementType.ToString());
                        writer.Write('[');
                        writer.Write(alloc.ArraySize);
                        writer.Write("] ");
                        writer.Write(alloc.TotalSize);
                        writer.WriteLine(" bytes");
                    }
                }

                // Information about methods, calls and local memory sizes
                if (!Functions.IsDefaultOrEmpty)
                {
                    writer.WriteLine("Functions:");
                    for (int i = 0, e = Functions.Length; i < e; ++i)
                    {
                        ref readonly var functionRef = ref Functions.ItemRef(i);
                        var methodName = functionRef.Method?.Name ?? functionRef.Name;
                        writer.Write('\t');
                        writer.WriteLine(methodName);

                        writer.Write("\t\tLocal Memory: ");
                        writer.WriteLine(functionRef.LocalMemorySize);
                        writer.WriteLine(" bytes");
                    }
                }
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new compiled kernel.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="info">Detailed kernel information.</param>
        protected CompiledKernel(
            Context context,
            EntryPoint entryPoint,
            KernelInfo info)
        {
            Context = context;
            EntryPoint = entryPoint;
            Info = info;
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
        /// Returns the associated kernel function name.
        /// </summary>
        public string Name => EntryPoint.Name;

        /// <summary>
        /// Returns the index type of the entry point.
        /// </summary>
        public IndexType IndexType => EntryPoint.IndexType;

        /// <summary>
        /// Returns the associated kernel specialization.
        /// </summary>
        public KernelSpecialization Specialization => EntryPoint.Specialization;

        /// <summary>
        /// Returns the number of uniform parameters.
        /// </summary>
        public int NumParameters => EntryPoint.Parameters.Count;

        /// <summary>
        /// Returns the internally used entry point.
        /// </summary>
        internal EntryPoint EntryPoint { get; }

        /// <summary>
        /// Returns information about all functions in the compiled kernel.
        /// </summary>
        /// <remarks>
        /// This instance will be available when the property
        /// <see cref="ContextProperties.EnableKernelInformation"/> is enabled.
        /// </remarks>
        public KernelInfo Info { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this kernel.
        /// </summary>
        /// <returns>The string representation of this kernel.</returns>
        public override string ToString() =>
            $"{SourceMethod}[Specialization: {Specialization}]";

        #endregion
    }
}
