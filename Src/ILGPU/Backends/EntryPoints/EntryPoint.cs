// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: EntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// Specifies an entry point method including its associated index type.
    /// </summary>
    public readonly struct EntryPointDescription : IEquatable<EntryPointDescription>
    {
        #region Static

        /// <summary>
        /// Creates a new entry point description from the given method source that is compatible
        /// with explicitly grouped kernels.
        /// </summary>
        /// <param name="methodSource">The kernel method source.</param>
        /// <returns>The created entry point description.</returns>
        public static EntryPointDescription FromExplicitlyGroupedKernel(MethodInfo methodSource) =>
            new EntryPointDescription(methodSource, IndexType.KernelConfig);

        /// <summary>
        /// Creates a new entry point description from the given method source that is compatible
        /// with implicitly grouped kernels.
        /// </summary>
        /// <param name="methodSource">The kernel method source.</param>
        /// <returns>The created entry point description.</returns>
        public static EntryPointDescription FromImplicitlyGroupedKernel(MethodInfo methodSource)
        {
            if (methodSource == null)
                throw new ArgumentNullException(nameof(methodSource));
            var parameters = methodSource.GetParameters();
            if (parameters.Length < 1)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointIndexParameter);

            // Try to get index type from first parameter
            var firstParamType = parameters[0].ParameterType;
            var indexType = firstParamType.GetIndexType();
            if (indexType == IndexType.None)
                throw new NotSupportedException(
                    ErrorMessages.InvalidEntryPointIndexParameterOfWrongType);
            return new EntryPointDescription(methodSource, indexType);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new entry point description.
        /// </summary>
        /// <param name="methodSource">The method source.</param>
        /// <param name="indexType">The index type.</param>
        private EntryPointDescription(MethodInfo methodSource, IndexType indexType)
        {
            if (indexType == IndexType.None)
                throw new ArgumentOutOfRangeException(nameof(indexType));
            MethodSource = methodSource ?? throw new ArgumentNullException(nameof(methodSource));
            IndexType = indexType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kernel method.
        /// </summary>
        public MethodInfo MethodSource { get; }

        /// <summary>
        /// Returns the associated index type.
        /// </summary>
        public IndexType IndexType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Validates this object and throws a <see cref="NotSupportedException"/> in the case
        /// of a not supported kernel configuration.
        /// </summary>
        public void Validate()
        {
            if (MethodSource == null)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointWithoutDotNetMethod);
            if (!MethodSource.IsStatic)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointInstanceKernelMethod);
            if (IndexType == IndexType.None)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
        }

        /// <summary>
        /// Returns true if the given description is equal to the current one.
        /// </summary>
        /// <param name="other">The other description.</param>
        /// <returns>True, if the given cached key is equal to the current one.</returns>
        public bool Equals(EntryPointDescription other) =>
            other.MethodSource == MethodSource &&
            other.IndexType == IndexType;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current one.</returns>
        public override bool Equals(object obj) =>
            obj is EntryPointDescription other && Equals(other);

        /// <summary>
        /// Returns the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override int GetHashCode() =>
            MethodSource.GetHashCode() ^ IndexType.GetHashCode();

        /// <summary>
        /// Returns the string representation of this object.
        /// </summary>
        /// <returns>The string representation of this object.</returns>
        public override string ToString() => $"{MethodSource}({IndexType})";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the left and right descriptions are the same.
        /// </summary>
        /// <param name="left">The left description.</param>
        /// <param name="right">The right description.</param>
        /// <returns>True, if the left and right descriptions are the same.</returns>
        public static bool operator ==(EntryPointDescription left, EntryPointDescription right) =>
            left.Equals(right);

        /// <summary>
        /// Returns true if the left and right descriptions are not the same.
        /// </summary>
        /// <param name="left">The left description.</param>
        /// <param name="right">The right description.</param>
        /// <returns>True, if the left and right descriptions are not the same.</returns>
        public static bool operator !=(EntryPointDescription left, EntryPointDescription right) =>
            !(left == right);

        #endregion
    }

    /// <summary>
    /// Represents a kernel entry point.
    /// </summary>
    public class EntryPoint
    {
        #region Nested Types

        /// <summary>
        /// The parameter specification of an entry point.
        /// </summary>
        public readonly struct ParameterSpecification
        {
            #region Instance

            /// <summary>
            /// Constructs a new parameter specification.
            /// </summary>
            /// <param name="parameterTypes">The parameter types.</param>
            internal ParameterSpecification(ImmutableArray<Type> parameterTypes)
            {
                ParameterTypes = parameterTypes;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of parameter types.
            /// </summary>
            public int NumParameters => ParameterTypes.Length;

            /// <summary>
            /// Returns the desired kernel launcher parameter types (including references).
            /// </summary>
            public ImmutableArray<Type> ParameterTypes { get; }

            /// <summary>
            /// Returns the underlying parameter type (without references).
            /// </summary>
            /// <param name="index"></param>
            /// <returns></returns>
            public Type this[int index]
            {
                get
                {
                    var type = ParameterTypes[index];
                    return type.IsByRef ? type.GetElementType() : type;
                }
            }

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the specified parameter is passed by reference.
            /// </summary>
            /// <param name="parameterIndex">The parameter index.</param>
            /// <returns>True, if the specified parameter is passed by reference.</returns>
            public bool IsByRef(int parameterIndex) =>
                ParameterTypes[parameterIndex].IsByRef;

            /// <summary>
            /// Copies the parameter types to the given array.
            /// </summary>
            /// <param name="target">The target array.</param>
            /// <param name="offset">The target offset to copy to.</param>
            public void CopyTo(Type[] target, int offset) =>
                ParameterTypes.CopyTo(target, offset);

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new entry point targeting the given method.
        /// </summary>
        /// <param name="description">The entry point description.</param>
        /// <param name="sharedMemory">The shared memory specification.</param>
        /// <param name="specialization">The kernel specialization.</param>
        public EntryPoint(
            EntryPointDescription description,
            in SharedMemorySpecification sharedMemory,
            in KernelSpecialization specialization)
        {
            description.Validate();

            MethodInfo = description.MethodSource;
            Specialization = specialization;
            SharedMemory = sharedMemory;

            IndexType = description.IndexType;
            KernelIndexType = IndexType.GetManagedIndexType();

            // Compute the number of actual parameters
            var parameters = MethodInfo.GetParameters();
            var kernelIndexParamOffset = IndexType == IndexType.KernelConfig ? 0 : 1;
            var parameterTypes = ImmutableArray.CreateBuilder<Type>(
                parameters.Length - kernelIndexParamOffset + (!MethodInfo.IsStatic ? 1 : 0));

            // TODO: enhance performance by passing arguments by ref
            // TODO: implement additional backend support
            for (int i = kernelIndexParamOffset, e = parameters.Length; i < e; ++i)
            {
                var type = parameters[i].ParameterType;
                if (type.IsPointer || type.IsPassedViaPtr())
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.NotSupportedKernelParameterType, i));
                parameterTypes.Add(type);
            }

            Parameters = new ParameterSpecification(
                parameterTypes.MoveToImmutable());
            for (int i = 0, e = Parameters.NumParameters; i < e; ++i)
                HasByRefParameters |= Parameters.IsByRef(i);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated method info.
        /// </summary>
        public MethodInfo MethodInfo { get; }

        /// <summary>
        /// Returns the index type of the index parameter.
        /// </summary>
        public IndexType IndexType { get; }

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
        /// Returns the parameter specification of arguments that are passed to the kernel.
        /// </summary>
        public ParameterSpecification Parameters { get; }

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
        /// are flattended into scalar parameters.
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
        /// <returns>The method emitter that represents the launcher method.</returns>
        internal Context.MethodEmitter CreateLauncherMethod(Context context)
        {
            Debug.Assert(context != null, "Invalid context");
            var parameterTypes = new Type[
                Parameters.NumParameters + Kernel.KernelParameterOffset];

            // Launcher(Kernel, AcceleratorStream, [Index], ...)
            parameterTypes[Kernel.KernelInstanceParamIdx] = typeof(Kernel);
            parameterTypes[Kernel.KernelStreamParamIdx] = typeof(AcceleratorStream);
            parameterTypes[Kernel.KernelParamDimensionIdx] = KernelIndexType;
            Parameters.CopyTo(parameterTypes, Kernel.KernelParameterOffset);

            var result = context.DefineRuntimeMethod(typeof(void), parameterTypes);
            // TODO: we have to port the following snippet to .Net Core
            // in order to support "in" parameters
            //if (Parameters.IsByRef(i))
            //{
            //    var paramIndex = Kernel.KernelParameterOffset + i;
            //    result.MethodBuilder.DefineParameter(
            //        paramIndex,
            //        ParameterAttributes.In,
            //        null);
            //}

            return result;
        }

        #endregion
    }

    /// <summary>
    /// Represents a shared memory specification of a specific kernel.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
                typeof(int)
            });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new shared memory specification.
        /// </summary>
        /// <param name="staticSize">The static shared memory size.</param>
        /// <param name="dynamicElementSize">The dynamic shared memory element size.</param>
        public SharedMemorySpecification(int staticSize, int dynamicElementSize)
        {
            Debug.Assert(staticSize >= 0, "Invalid static memory size");
            Debug.Assert(dynamicElementSize >= 0, "Invalid dynamic memory element size");

            StaticSize = staticSize;
            DynamicElementSize = dynamicElementSize;
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
        /// Returns the element size of a dynamic shared memory element (if any).
        /// </summary>
        public int DynamicElementSize { get; }

        /// <summary>
        /// Returns true if this entry point required dynamic shared memory.
        /// </summary>
        public bool HasDynamicMemory => DynamicElementSize > 0;

        #endregion
    }
}
