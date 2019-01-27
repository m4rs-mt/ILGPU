// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
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

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a kernel entry point.
    /// </summary>
    public sealed class EntryPoint
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
            public void CopyTo(Type[] target, int offset)
            {
                ParameterTypes.CopyTo(target, offset);
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new entry point targeting the given method.
        /// </summary>
        /// <param name="methodSource">The source method.</param>
        /// <param name="sharedMemorySize">The size of the shared memory in bytes.</param>
        /// <param name="specialization">The kernel specialization.</param>
        internal EntryPoint(
            MethodInfo methodSource,
            int sharedMemorySize,
            in KernelSpecialization specialization)
        {
            MethodInfo = methodSource;
            if (MethodInfo == null)
                throw new NotSupportedException("Not supported entry point without a valid .Net runtime entry");
            Specialization = specialization;
            SharedMemorySize = sharedMemorySize;

            if (!MethodInfo.IsStatic)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointInstanceKernelMethod);

            var parameters = MethodInfo.GetParameters();
            if (parameters.Length < 1)
                throw new ArgumentException(ErrorMessages.InvalidEntryPointIndexParameter);
            KernelIndexType = UngroupedIndexType = parameters[0].ParameterType;
            IndexType = KernelIndexType.GetIndexType();
            if (IndexType == IndexType.None)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointIndexParameterOfWrongType);
            UngroupedIndexType = IndexType.GetUngroupedIndexType().GetManagedIndexType();

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
        public bool IsGroupedIndexEntry
        {
            get
            {
                return
                    IndexType >= IndexType.GroupedIndex1D &&
                    IndexType <= IndexType.GroupedIndex3D;
            }
        }

        /// <summary>
        /// Returns the ungrouped index type of the index parameter.
        /// This can be <see cref="Index"/>, <see cref="Index2"/> or <see cref="Index3"/>.
        /// </summary>
        public Type UngroupedIndexType { get; }

        /// <summary>
        /// Returns the index type of the index parameter.
        /// This can also return a grouped index.
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
                        return 1;
                    case IndexType.Index2D:
                        return 2;
                    case IndexType.Index3D:
                        return 3;
                    case IndexType.GroupedIndex1D:
                        return 2;
                    case IndexType.GroupedIndex2D:
                        return 4;
                    case IndexType.GroupedIndex3D:
                        return 6;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Returns the amount of shared memory.
        /// </summary>
        public int SharedMemorySize { get; }

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
}
