// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Intrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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

namespace ILGPU.Frontend.Intrinsic
{
    enum IntrinsicType
    {
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
    /// Marks methods that are builtin.
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
        private delegate ValueReference DeviceFunctionHandler(in InvocationContext context);

        /// <summary>
        /// Stores function handlers.
        /// </summary>
        private static readonly Dictionary<Type, DeviceFunctionHandler> FunctionHandlers =
            new Dictionary<Type, DeviceFunctionHandler>();

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Caching of compiler-known functions")]
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
        /// <returns>True, iff this class could handle the call.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HandleIntrinsic(in InvocationContext context, out ValueReference result)
        {
            result = default;

            var method = context.Method;
            var intrinsic = method.GetCustomAttribute<IntrinsicAttribute>();

            if (intrinsic != null)
            {
                switch (intrinsic.Type)
                {
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
            var genericArgs = context.GetMethodGenericArguments();
            if (context.Method.Name != nameof(Activator.CreateInstance) ||
                context.NumArguments != 0 ||
                genericArgs.Length != 1 ||
                !genericArgs[0].IsValueType)
                throw context.GetNotSupportedException(
                    ErrorMessages.NotSupportedActivatorOperation, context.Method.Name);
            return context.Builder.CreateNull(
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

            switch (context.Method.Name)
            {
                case nameof(Debug.Write):
                    switch (context.NumArguments)
                    {
                        case 1:
                            return builder.CreateDebugTrace(context[0]);
                        default:
                            throw context.GetNotSupportedException(
                                ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
                    }
                case nameof(Debug.Fail):
                    switch (context.NumArguments)
                    {
                        case 1:
                            return builder.CreateDebugAssertFailed(context[0]);
                        default:
                            throw context.GetNotSupportedException(
                                ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
                    }
                default:
                    throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
            }
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
                    return context.Builder.CreateUndefinedVoid();
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
            var targetType = target.Type as StructureType;
            var targetViewType = targetType.Fields[0] as ViewType;
            if (!targetViewType.ElementType.TryResolveManagedType(out Type elementType))
            {
                throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic,
                        context.Method.Name);
            }
            int elementSize = Marshal.SizeOf(Activator.CreateInstance(elementType));

            // Load array instance
            var builder = context.Builder;
            var view = builder.CreateGetArrayImplementationView(target);

            // Convert values to IR values
            for (int i = 0, e = valueSize / elementSize; i < e; ++i)
            {
                byte* address = data + elementSize * i;
                var instance = Marshal.PtrToStructure(new IntPtr(address), elementType);

                // Convert element to IR value
                var irValue = builder.CreateValue(instance, elementType);
                var targetAddress = builder.CreateLoadElementAddress(
                    view,
                    builder.CreatePrimitiveValue(i));
                builder.CreateStore(targetAddress, irValue);
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
            if (context.Method is ConstructorInfo)
            {
                var newExtent = builder.CreateArrayImplementationExtent(
                    context.Arguments,
                    1);
                var newElementType = builder.CreateType(
                    context.Method.DeclaringType.GetElementType());
                var newArray = context.CodeGenerator.CreateArray(
                    context.Builder,
                    newExtent,
                    newElementType);
                return builder.CreateStore(context[0], newArray);

            }
            else
            {
                switch (context.Method.Name)
                {
                    case "Get":
                        var getAddress = builder.CreateLoadArrayImplementationElementAddress(
                            context[0],
                            context.Arguments,
                            1,
                            context.NumArguments - 1);
                        return builder.CreateLoad(getAddress);
                    case "Set":
                        var setAddress = builder.CreateLoadArrayImplementationElementAddress(
                            context[0],
                            context.Arguments,
                            1,
                            context.NumArguments - 2);
                        return builder.CreateStore(setAddress, context[context.NumArguments - 1]);
                    case "get_Length":
                        return builder.CreateGetLinearArrayImplementationLength(context[0]);
                    case "get_LongLength":
                        return builder.CreateConvert(
                            builder.CreateGetLinearArrayImplementationLength(context[0]),
                            builder.GetPrimitiveType(BasicValueType.Int64));
                    case nameof(Array.GetLowerBound):
                        return builder.CreatePrimitiveValue(0);
                    case nameof(Array.GetUpperBound):
                        return builder.CreateArithmetic(
                            builder.CreateGetArrayImplementationLength(context[0], context[1]),
                            builder.CreatePrimitiveValue(1),
                            BinaryArithmeticKind.Sub);
                    case nameof(Array.GetLength):
                        return builder.CreateGetArrayImplementationLength(context[0], context[1]);
                    case nameof(Array.GetLongLength):
                        return builder.CreateConvert(
                            builder.CreateGetArrayImplementationLength(context[0], context[1]),
                            builder.GetPrimitiveType(BasicValueType.Int64));
                    default:
                        throw context.GetNotSupportedException(
                            ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
                }
            }
        }

        #endregion
    }
}
