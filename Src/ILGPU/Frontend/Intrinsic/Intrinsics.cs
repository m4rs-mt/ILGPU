// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Intrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// disable: max_line_length

namespace ILGPU.Frontend.Intrinsic
{
    enum IntrinsicType : int
    {
        Accelerator,
        Atomic,
        Grid,
        Group,
        Interop,
        Math,
        MemoryFence,
        SharedMemory,
        View,
        Warp,
        Utility,
    }

    /// <summary>
    /// Marks methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    abstract class IntrinsicAttribute : Attribute
    {
        /// <summary>
        /// Returns the type of this intrinsic attribute.
        /// </summary>
        public abstract IntrinsicType Type { get; }
    }

    /// <summary>
    /// Contains default ILGPU intrinsics.
    /// </summary>
    static partial class Intrinsics
    {
        #region Static Handler

        /// <summary>
        /// Represents a basic handler for compiler-specific device functions.
        /// </summary>
        private delegate ValueReference DeviceFunctionHandler(
            in InvocationContext context);

        /// <summary>
        /// Stores function handlers.
        /// </summary>
        private static readonly Dictionary<Type, DeviceFunctionHandler> FunctionHandlers =
            new Dictionary<Type, DeviceFunctionHandler>();

        [SuppressMessage(
            "Microsoft.Performance",
            "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Caching of compiler-known functions")]
        static Intrinsics()
        {
            FunctionHandlers.Add(typeof(Activator), HandleActivator);
            FunctionHandlers.Add(typeof(Debug), HandleDebug);
            FunctionHandlers.Add(typeof(RuntimeHelpers), HandleRuntimeHelper);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to handle a specific invocation context. This method
        /// can generate custom code instead of the default method-invocation
        /// functionality.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="result">The resulting value of the intrinsic call.</param>
        /// <returns>True, if this class could handle the call.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HandleIntrinsic(
            in InvocationContext context,
            out ValueReference result)
        {
            result = default;

            var method = context.Method;
            var intrinsic = method.GetCustomAttribute<IntrinsicAttribute>();

            if (intrinsic != null)
            {
                switch (intrinsic.Type)
                {
                    case IntrinsicType.Accelerator:
                        result = HandleAcceleratorOperation(context, intrinsic as AcceleratorIntrinsicAttribute);
                        break;
                    case IntrinsicType.Atomic:
                        result = HandleAtomicOperation(context, intrinsic as AtomicIntrinsicAttribute);
                        break;
                    case IntrinsicType.Grid:
                        result = HandleGridOperation(context, intrinsic as GridIntrinsicAttribute);
                        break;
                    case IntrinsicType.Group:
                        result = HandleGroupOperation(context, intrinsic as GroupIntrinsicAttribute);
                        break;
                    case IntrinsicType.Interop:
                        result = HandleInterop(context, intrinsic as InteropIntrinsicAttribute);
                        break;
                    case IntrinsicType.Math:
                        result = HandleMathOperation(context, intrinsic as MathIntrinsicAttribute);
                        break;
                    case IntrinsicType.MemoryFence:
                        result = HandleMemoryBarrierOperation(context, intrinsic as MemoryBarrierIntrinsicAttribute);
                        break;
                    case IntrinsicType.SharedMemory:
                        result = HandleSharedMemoryOperation(context, intrinsic as SharedMemoryIntrinsicAttribute);
                        break;
                    case IntrinsicType.View:
                        result = HandleViewOperation(context, intrinsic as ViewIntrinsicAttribute);
                        break;
                    case IntrinsicType.Warp:
                        result = HandleWarpOperation(context, intrinsic as WarpIntrinsicAttribute);
                        break;
                    case IntrinsicType.Utility:
                        result = HandleUtilityOperation(context, intrinsic as UtilityIntrinsicAttribute);
                        break;
                }
            }

            if (IsIntrinsicArrayType(method.DeclaringType))
                result = HandleArrays(context);
            else if (FunctionHandlers.TryGetValue(method.DeclaringType, out DeviceFunctionHandler handler))
                result = handler(context);

            return result.IsValid;
        }

        /// <summary>
        /// Determines whether the given type is an intrinsic array type.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True, if the given type is an intrinsic array type.</returns>
        internal static bool IsIntrinsicArrayType(Type type) =>
            type == typeof(Array) || type.IsArray;

        #endregion

        #region External

        /// <summary>
        /// Handles activator operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleActivator(in InvocationContext context)
        {
            var location = context.Location;

            var genericArgs = context.GetMethodGenericArguments();
            if (context.Method.Name != nameof(Activator.CreateInstance) ||
                context.NumArguments != 0 ||
                genericArgs.Length != 1 ||
                !genericArgs[0].IsValueType)
            {
                throw context.GetNotSupportedException(
                    ErrorMessages.NotSupportedActivatorOperation, context.Method.Name);
            }

            return context.Builder.CreateNull(
                location,
                context.Builder.CreateType(genericArgs[0]));
        }

        /// <summary>
        /// Handles debugging operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleDebug(in InvocationContext context)
        {
            var builder = context.Builder;
            var location = context.Location;

            switch (context.Method.Name)
            {
                case nameof(Debug.Write):
                    switch (context.NumArguments)
                    {
                        case 1:
                            return builder.CreateDebug(
                                location,
                                DebugKind.Trace,
                                context[0]);
                    }
                    break;
                case nameof(Debug.Fail):
                    switch (context.NumArguments)
                    {
                        case 1:
                            return builder.CreateDebug(
                                location,
                                DebugKind.AssertFailed,
                                context[0]);
                    }
                    break;
            }
            throw context.GetNotSupportedException(
                ErrorMessages.NotSupportedIntrinsic,
                context.Method.Name);
        }

        /// <summary>
        /// Handles runtime operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleRuntimeHelper(in InvocationContext context)
        {
            switch (context.Method.Name)
            {
                case nameof(RuntimeHelpers.InitializeArray):
                    InitializeArray(context);
                    return context.Builder.CreateUndefined();
                default:
                    throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
            }
        }

        /// <summary>
        /// Initializes arrays.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static unsafe void InitializeArray(in InvocationContext context)
        {
            // Resolve the array data
            var handle = context[1].ResolveAs<HandleValue>();
            var value = handle.GetHandle<FieldInfo>().GetValue(null);
            int valueSize = Marshal.SizeOf(value);

            // Load the associated array data
            byte* data = stackalloc byte[valueSize];
            Marshal.StructureToPtr(value, new IntPtr(data), true);

            // Convert unsafe data into target chunks and emit
            // appropriate store instructions
            Value target = context[0];
            var arrayType = target.Type as ArrayType;
            var elementType = arrayType.ElementType.ManagedType;

            // Convert values to IR values
            var builder = context.Builder;
            var location = context.Location;
            int elementSize = Marshal.SizeOf(Activator.CreateInstance(elementType));
            for (int i = 0, e = valueSize / elementSize; i < e; ++i)
            {
                byte* address = data + elementSize * i;
                var instance = Marshal.PtrToStructure(new IntPtr(address), elementType);

                // Convert element to IR value
                var irValue = builder.CreateValue(
                    location,
                    instance,
                    elementType);
                var targetIndex = builder.CreatePrimitiveValue(location, i);

                builder.CreateSetArrayElement(
                    location,
                    target,
                    targetIndex,
                    irValue);
            }
        }

        /// <summary>
        /// Handles array operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleArrays(in InvocationContext context)
        {
            var builder = context.Builder;
            var location = context.Location;
            if (context.Method is ConstructorInfo)
            {
                var newExtent = builder.CreateDynamicStructure(
                    location,
                    context.Arguments.RemoveAt(0));
                var newElementType = builder.CreateType(
                    context.Method.DeclaringType.GetElementType());
                return builder.CreateArray(
                    location,
                    newElementType,
                    context.NumArguments - 1,
                    newExtent);
            }
            else
            {
                return context.Method.Name switch
                {
                    "Get" => builder.CreateGetArrayElement(
                           location,
                       context[0],
                       builder.CreateDynamicStructure(
                           location,
                           context.Arguments.RemoveAt(0))),
                    "Set" => builder.CreateSetArrayElement(
                           location,
                        context[0],
                        builder.CreateDynamicStructure(
                           location,
                            context.Arguments.RemoveAt(0).RemoveAt(
                                context.NumArguments - 2)),
                        context[context.NumArguments - 1]),
                    "get_Length" => builder.CreateGetArrayLength(
                        location,
                        context[0]),
                    "get_LongLength" => builder.CreateConvert(
                        location,
                        builder.CreateGetArrayLength(
                            location,
                            context[0]),
                        builder.GetPrimitiveType(BasicValueType.Int64)),
                    nameof(Array.GetLowerBound) => builder.CreatePrimitiveValue(
                        location,
                        0),
                    nameof(Array.GetUpperBound) => builder.CreateArithmetic(
                        location,
                        builder.CreateGetField(
                            location,
                            builder.CreateGetArrayExtent(
                                location,
                                context[0]),
                            new FieldSpan(
                                context[1].ResolveAs<PrimitiveValue>().Int32Value)),
                        builder.CreatePrimitiveValue(
                            location,
                            1),
                        BinaryArithmeticKind.Sub),
                    nameof(Array.GetLength) => builder.CreateGetField(
                        location,
                        builder.CreateGetArrayExtent(
                            location,
                            context[0]),
                        new FieldSpan(
                            context[1].ResolveAs<PrimitiveValue>().Int32Value)),
                    nameof(Array.GetLongLength) => builder.CreateConvert(
                        location,
                        builder.CreateGetField(
                            location,
                            builder.CreateGetArrayExtent(
                                location,
                                context[0]),
                                new FieldSpan(
                                context[1].ResolveAs<PrimitiveValue>().Int32Value)),
                        builder.GetPrimitiveType(BasicValueType.Int64)),
                    nameof(Array.Empty) => builder.CreateArray(
                        location,
                        builder.CreateType(context.Method.GetGenericArguments()[0]),
                        1,
                        builder.CreatePrimitiveValue(
                            location,
                            0)),
                    _ => throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic,
                        context.Method.Name),
                };
            }
        }

        #endregion
    }
}
