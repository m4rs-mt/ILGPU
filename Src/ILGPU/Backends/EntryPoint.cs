// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: EntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a kernel entry point.
    /// </summary>
    sealed class EntryPoint
    {
        #region Constants

        private static readonly Type VariableViewType = System.Type.GetType("ILGPU.VariableView`1");
        private static readonly Type ArrayViewType = System.Type.GetType("ILGPU.ArrayView`1");

        #endregion

        #region Static

        /// <summary>
        /// Resolves the element-type of a shared-memoery variable.
        /// </summary>
        /// <param name="parameterType">The given parameter type.</param>
        /// <param name="isArray">True, iff the parameter type specifies an array view.</param>
        /// <returns>The resolved element type.</returns>
        private static Type GetSharedVariableElementType(Type parameterType, out bool isArray)
        {
            var args = parameterType.GetGenericArguments();
            if (args.Length == 1 && VariableViewType.MakeGenericType(args) == parameterType)
                isArray = false;
            else if (args.Length == 1 && ArrayViewType.MakeGenericType(args) == parameterType)
                isArray = true;
            else
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedSharedMemoryVariableType, parameterType));
            return args[0];
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a single variable that is allocated
        /// in shared memory. This can be a single elementatry variable
        /// or an array of elements.
        /// </summary>
        public struct SharedMemoryVariable
        {
            public SharedMemoryVariable(
                int index,
                int sharedMemoryIndex,
                Type type,
                Type elementType,
                bool isArray,
                int? count,
                int size)
            {
                Index = index;
                SharedMemoryIndex = sharedMemoryIndex;
                Type = type;
                ElementType = elementType;
                IsArray = isArray;
                Count = count;
                ElementSize = size;
            }

            /// <summary>
            /// The parameter index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// The shared-memory index.
            /// </summary>
            public int SharedMemoryIndex { get; }

            /// <summary>
            /// Returns the type of the variable.
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Returns the element type of the variable.
            /// </summary>
            public Type ElementType { get; }

            /// <summary>
            /// Returns true iff this shared variable represents an array.
            /// </summary>
            public bool IsArray { get; }

            /// <summary>
            /// Returns null, if the number of elements is unbounded (dynamically
            /// sized in case of an array, or simply constant in case of a variable)
            /// or the actual number of requested elements.
            /// </summary>
            public int? Count { get; }

            /// <summary>
            /// Returns true iff this shared variable represents a dynamically-sized array.
            /// </summary>
            public bool IsDynamicallySizedArray => IsArray && !Count.HasValue;

            /// <summary>
            /// Returns the size of the element type in bytes.
            /// </summary>
            public int ElementSize { get; }

            /// <summary>
            /// Returns the size in bytes of this shared-memory variable.
            /// Note that an <see cref="InvalidOperationException"/> will be thrown, iff
            /// this variable is a dynamically-sized array.
            /// </summary>
            public int Size
            {
                get
                {
                    if (!IsArray)
                        return ElementSize;
                    if (!Count.HasValue)
                        throw new InvalidOperationException("Cannot query size in bytes of a dynamically sized array");
                    return ElementSize * Count.Value;
                }
            }

            /// <summary>
            /// Returns the string representation of this variable.
            /// </summary>
            /// <returns>The string representation of this variable.</returns>
            public override string ToString()
            {
                return $"Idx: '{Index}', ElementType: '{ElementType}', Count: {Count}";
            }
        }

        /// <summary>
        /// Represents a uniform variable.
        /// </summary>
        public struct UniformVariable
        {
            public UniformVariable(
                int index,
                Type variableType,
                int size)
            {
                Index = index;
                VariableType = variableType;
                Size = size;
            }

            /// <summary>
            /// The parameter index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Returns the type of the variable.
            /// </summary>
            public Type VariableType { get; }

            /// <summary>
            /// Returns the size in bytes.
            /// </summary>
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This property might be useful in the future")]
            public int Size { get; }

            /// <summary>
            /// Returns the string representation of this variable.
            /// </summary>
            /// <returns>The string representation of this variable.</returns>
            public override string ToString()
            {
                return $"Idx: '{Index}', Type: '{VariableType}'";
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new entry point targeting the given method.
        /// </summary>
        /// <param name="methodInfo">The targeted method.</param>
        /// <param name="unit">The unit in the current context.</param>
        public EntryPoint(MethodInfo methodInfo, CompileUnit unit)
        {
            MethodInfo = methodInfo;
            NumDynamicallySizedSharedMemoryVariables = 0;

            if (!methodInfo.IsStatic)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointInstanceKernelMethod);

            var @params = methodInfo.GetParameters();
            if (@params.Length < 1)
                throw new ArgumentException(ErrorMessages.InvalidEntryPointIndexParameter);
            KernelIndexType = UngroupedIndexType = @params[0].ParameterType;
            Type = KernelIndexType.GetIndexType();
            if (Type == IndexType.None)
                throw new NotSupportedException(ErrorMessages.InvalidEntryPointIndexParameterOfWrongType);
            UngroupedIndexType = Type.GetUngroupedIndexType().GetManagedIndexType();

            // Compute the number of actual parameters
            var uniformVariables = new List<UniformVariable>(@params.Length - 1 + (methodInfo.IsStatic ? 1 : 0));
            var sharedMemoryVariables = new List<SharedMemoryVariable>(@params.Length - 1);

            if (!methodInfo.IsStatic)
                uniformVariables.Add(
                    new UniformVariable(0,
                    methodInfo.DeclaringType.MakePointerType(),
                    unit.IntPtrType.SizeOf()));
            for (int i = 1, e = @params.Length; i < e; ++i)
            {
                var param = @params[i];
                if (SharedMemoryAttribute.TryGetSharedMemoryCount(param, out int? count))
                {
                    var elementType = GetSharedVariableElementType(param.ParameterType, out bool isArray);
                    var paramSize = elementType.SizeOf();
                    if (!isArray && count != null && count.Value != 1)
                        throw new NotSupportedException(ErrorMessages.InvalidUseOfVariableViewsInSharedMemory);
                    int sharedMemoryVariableIndex = -1;
                    if (isArray && count == null)
                    {
                        sharedMemoryVariableIndex = NumDynamicallySizedSharedMemoryVariables;
                        NumDynamicallySizedSharedMemoryVariables += 1;
                    }
                    sharedMemoryVariables.Add(
                        new SharedMemoryVariable(i,
                        sharedMemoryVariableIndex,
                        param.ParameterType,
                        elementType,
                        isArray,
                        count,
                        paramSize));
                }
                else
                {
                    var paramSize = param.ParameterType.SizeOf();
                    uniformVariables.Add(
                        new UniformVariable(i,
                        param.ParameterType,
                        paramSize));
                }
            }

            UniformVariables = uniformVariables.ToArray();
            SharedMemoryVariables = sharedMemoryVariables.ToArray();

            if (Type < IndexType.GroupedIndex1D && SharedMemoryVariables.Length > 0)
                throw new NotSupportedException(ErrorMessages.NotSupportedUseOfSharedMemory);

            foreach (var variable in uniformVariables)
            {
                if (variable.VariableType != variable.VariableType.GetLLVMTypeRepresentation())
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.NotSupportedKernelParameterType, variable));
            }
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
        public IndexType Type { get; }

        /// <summary>
        /// Returns true iff the entry-point type = grouped index.
        /// </summary>
        public bool IsGroupedIndexEntry
        {
            get
            {
                return
                    Type >= IndexType.GroupedIndex1D &&
                    Type <= IndexType.GroupedIndex3D;
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
        /// Returns the uniform variables that are passed to the kernel.
        /// </summary>
        public UniformVariable[] UniformVariables { get; }

        /// <summary>
        /// Returns the number of uniform parameters that have to be passed
        /// to the virtual entry point.
        /// </summary>
        public int NumUniformVariables => UniformVariables.Length;

        /// <summary>
        /// Returns the shared-memory variables that are requested by the kernel.
        /// </summary>
        public SharedMemoryVariable[] SharedMemoryVariables { get; }

        /// <summary>
        /// Returns the number of dynamically sized shared-memory variables.
        /// </summary>
        public int NumDynamicallySizedSharedMemoryVariables { get; }

        /// <summary>
        /// Returns the number of custom parameters that have to be passed
        /// to the virtual entry point.
        /// </summary>
        public int NumCustomParameters => NumUniformVariables + SharedMemoryVariables.Length;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a signature for the actual kernel entry point.
        /// </summary>
        /// <returns>A signature for the actual kernel entry point.</returns>
        public Type[] CreateCustomParameterTypes()
        {
            var argTypes = new Type[UniformVariables.Length + NumDynamicallySizedSharedMemoryVariables];

            for (int i = 0, e = UniformVariables.Length; i < e; ++i)
                argTypes[i] = UniformVariables[i].VariableType;

            // Attach length information to dynamically sized variables using runtime information
            for (int i = 0, e = NumDynamicallySizedSharedMemoryVariables; i < e; ++i)
                argTypes[i + UniformVariables.Length] = typeof(int);

            return argTypes;
        }

        #endregion
    }
}
