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
        /// <param name="methodSource">The source method.</param>
        /// <param name="sharedMemory">The shared memory specification.</param>
        /// <param name="specialization">The kernel specialization.</param>
        public EntryPoint(
            MethodInfo methodSource,
            in SharedMemorySpecification sharedMemory,
            in KernelSpecialization specialization)
        {
            MethodInfo = methodSource;
            if (MethodInfo == null)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointWithoutDotNetMethod);
            Specialization = specialization;
            SharedMemory = sharedMemory;

            if (!MethodInfo.IsStatic)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointInstanceKernelMethod);

            var parameters = MethodInfo.GetParameters();
            if (parameters.Length < 1)
                throw new ArgumentException(ErrorMessages.InvalidEntryPointIndexParameter);
            KernelIndexType = parameters[0].ParameterType;
            IndexType = KernelIndexType.GetIndexType();
            if (IndexType == IndexType.None)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointIndexParameterOfWrongType);

            // Compute the number of actual parameters
            var parameterTypes = ImmutableArray.CreateBuilder<Type>(
                parameters.Length - 1 + (!MethodInfo.IsStatic ? 1 : 0));

            // TODO: enhance performance by passing arguments by ref
            // TODO: implement additional backend support
            for (int i = 1, e = parameters.Length; i < e; ++i)
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
        /// Returns true iff the entry-point type = grouped index.
        /// </summary>
        public bool IsGroupedIndexEntry => IndexType == IndexType.GroupedIndex;

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
                    case IndexType.GroupedIndex:
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
        #region Instance

        /// <summary>
        /// Constructs a new shared memory specification.
        /// </summary>
        /// <param name="staticSize">The static shared memory size.</param>
        /// <param name="dynamicElementSize">The dynamic shared memory element size.</param>
        public SharedMemorySpecification(
            int staticSize,
            int dynamicElementSize)
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
