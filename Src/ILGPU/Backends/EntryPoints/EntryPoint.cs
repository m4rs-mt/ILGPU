// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: EntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// Represents a kernel entry point.
    /// </summary>
    public partial class EntryPoint
    {
        #region Instance

        /// <summary>
        /// Constructs a new entry point targeting the given method.
        /// </summary>
        /// <param name="description">The entry point description.</param>
        /// <param name="sharedMemory">The shared memory specification.</param>
        /// <param name="specialization">The kernel specialization.</param>
        public EntryPoint(
            in EntryPointDescription description,
            in SharedMemorySpecification sharedMemory,
            in KernelSpecialization specialization)
        {
            description.Validate();

            Description = description;
            Specialization = specialization;
            SharedMemory = sharedMemory;
            KernelIndexType = IndexType.GetManagedIndexType();
            for (int i = 0, e = Parameters.Count; i < e; ++i)
                HasByRefParameters |= Parameters.IsByRef(i);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated description instance.
        /// </summary>
        public EntryPointDescription Description { get; }

        /// <summary>
        /// Returns the associated method info.
        /// </summary>
        public MethodInfo MethodInfo => Description.MethodSource;

        /// <summary>
        /// Returns the index type of the index parameter.
        /// </summary>
        public IndexType IndexType => Description.IndexType;

        /// <summary>
        /// Returns the offset for the actual parameter values while taking an implicit
        /// index argument into account.
        /// </summary>
        public int KernelIndexParameterOffset => Description.KernelIndexParameterOffset;

        /// <summary>
        /// Returns true if the entry point represents an explicitly grouped kernel.
        /// </summary>
        public bool IsExplicitlyGrouped => IndexType == IndexType.KernelConfig;

        /// <summary>
        /// Returns true if the entry point represents an implicitly grouped kernel.
        /// </summary>
        public bool IsImplictlyGrouped => !IsExplicitlyGrouped;

        /// <summary>
        /// Returns the index type of the index parameter.
        /// This can also return the <see cref="KernelConfig"/> type in the case of
        /// an explicitly grouped kernel.
        /// </summary>
        public Type KernelIndexType { get; }

        /// <summary>
        /// Returns the parameter specification of arguments that are passed to the
        /// kernel.
        /// </summary>
        public ParameterCollection Parameters => Description.Parameters;

        /// <summary>
        /// Returns true if this entry point uses specialized parameters.
        /// </summary>
        public bool HasSpecializedParameters => Parameters.HasSpecializedParameters;

        /// <summary>
        /// Returns true if the parameter specification contains by reference parameters.
        /// </summary>
        public bool HasByRefParameters { get; }

        /// <summary>
        /// Returns the associated launch specification.
        /// </summary>
        public KernelSpecialization Specialization { get; }

        /// <summary>
        /// Returns the number of index parameters when all structures
        /// are flattened into scalar parameters.
        /// </summary>
        public int NumFlattendedIndexParameters
        {
            get
            {
                switch (IndexType)
                {
                    case IndexType.Index1D:
                    case IndexType.KernelConfig:
                        return 1;
                    case IndexType.Index2D:
                        return 2;
                    case IndexType.Index3D:
                        return 3;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Returns the associated shared memory specification.
        /// </summary>
        public SharedMemorySpecification SharedMemory { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new launcher method.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="instanceType">The instance type (if any).</param>
        /// <returns>The method emitter that represents the launcher method.</returns>
        internal Context.MethodEmitter CreateLauncherMethod(
            Context context,
            Type instanceType = null) =>
            Description.CreateLauncherMethod(context, instanceType);

        #endregion
    }

    /// <summary>
    /// Represents a shared memory specification of a specific kernel.
    /// </summary>
    [Serializable]
    public readonly struct SharedMemorySpecification
    {
        #region Static

        /// <summary>
        /// Represents the associated constructor taking two integer parameters.
        /// </summary>
        internal static ConstructorInfo Constructor = typeof(SharedMemorySpecification).
            GetConstructor(new Type[]
            {
                typeof(int),
                typeof(bool)
            });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new shared memory specification.
        /// </summary>
        /// <param name="staticSize">The static shared memory size.</param>
        /// <param name="hasDynamicMemory">
        /// True, if this specification requires dynamic shared memory.
        /// </param>
        public SharedMemorySpecification(int staticSize, bool hasDynamicMemory)
        {
            Debug.Assert(staticSize >= 0, "Invalid static memory size");

            StaticSize = staticSize;
            HasDynamicMemory = hasDynamicMemory;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the current specification.
        /// </summary>
        public bool HasSharedMemory => HasStaticMemory | HasDynamicMemory;

        /// <summary>
        /// Returns the amount of shared memory.
        /// </summary>
        public int StaticSize { get; }

        /// <summary>
        /// Returns true if the current specification required static shared memory.
        /// </summary>
        public bool HasStaticMemory => StaticSize > 0;

        /// <summary>
        /// Returns true if the current specification requires dynamic shared memory.
        /// </summary>
        public bool HasDynamicMemory { get; }

        #endregion
    }
}
