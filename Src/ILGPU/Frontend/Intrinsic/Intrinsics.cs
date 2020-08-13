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
        Compare,
        Convert,
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
        private delegate Value DeviceFunctionHandler(ref InvocationContext context);

        /// <summary>
        /// Represents a basic handler for compiler-specific device functions.
        /// </summary>
        private delegate Value DeviceFunctionHandler<TIntrinsicAttribute>(
            ref InvocationContext context,
            TIntrinsicAttribute attribute)
            where TIntrinsicAttribute : IntrinsicAttribute;

        /// <summary>
        /// Stores function handlers.
        /// </summary>
        private static readonly Dictionary<Type, DeviceFunctionHandler> FunctionHandlers =
            new Dictionary<Type, DeviceFunctionHandler>()
            {
                { typeof(Activator), HandleActivator },
                { typeof(Debug), HandleDebugAndTrace },
                { typeof(Trace), HandleDebugAndTrace },
                { typeof(RuntimeHelpers), HandleRuntimeHelper },
            };

        private static readonly DeviceFunctionHandler<IntrinsicAttribute>[]
            IntrinsicHandlers =
        {
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleAcceleratorOperation(
                    ref context, attribute as AcceleratorIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleAtomicOperation(
                    ref context, attribute as AtomicIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleCompareOperation(
                    ref context, attribute as CompareIntriniscAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleConvertOperation(
                    ref context, attribute as ConvertIntriniscAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleGridOperation(
                    ref context, attribute as GridIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleGroupOperation(
                    ref context, attribute as GroupIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleInterop(
                    ref context, attribute as InteropIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleMathOperation(
                    ref context, attribute as MathIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleMemoryBarrierOperation(
                    ref context, attribute as MemoryBarrierIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleSharedMemoryOperation(
                    ref context, attribute as SharedMemoryIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleViewOperation(
                    ref context, attribute as ViewIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleWarpOperation(
                    ref context, attribute as WarpIntrinsicAttribute),
            (ref InvocationContext context, IntrinsicAttribute attribute) =>
                HandleUtilityOperation(
                    ref context, attribute as UtilityIntrinsicAttribute),
        };

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
            ref InvocationContext context,
            out ValueReference result)
        {
            result = default;
            var method = context.Method;

            var intrinsic = method.GetCustomAttribute<IntrinsicAttribute>();
            if (intrinsic != null)
                result = IntrinsicHandlers[(int)intrinsic.Type](ref context, intrinsic);

            if (IsIntrinsicArrayType(method.DeclaringType))
            {
                result = HandleArrays(ref context);
            }
            else if (FunctionHandlers.TryGetValue(
                method.DeclaringType,
                out DeviceFunctionHandler handler))
            {
                result = handler(ref context);
            }

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
        private static Value HandleActivator(ref InvocationContext context)
        {
            var location = context.Location;

            var genericArgs = context.GetMethodGenericArguments();
            if (context.Method.Name != nameof(Activator.CreateInstance) ||
                context.NumArguments != 0 ||
                genericArgs.Length != 1 ||
                !genericArgs[0].IsValueType)
            {
                throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedActivatorOperation,
                    context.Method.Name);
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
        private static Value HandleDebugAndTrace(ref InvocationContext context)
        {
            var builder = context.Builder;
            var location = context.Location;

            return context.Method.Name switch
            {
                nameof(Debug.Assert) when context.NumArguments == 2 =>
                    builder.CreateDebug(location, DebugKind.Trace, context[0]),
                nameof(Debug.Fail) when context.NumArguments == 1 =>
                    builder.CreateDebug(location, DebugKind.AssertFailed, context[0]),
                _ => throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedIntrinsic,
                    context.Method.Name),
            };
        }

        /// <summary>
        /// Handles runtime operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value HandleRuntimeHelper(ref InvocationContext context)
        {
            switch (context.Method.Name)
            {
                case nameof(RuntimeHelpers.InitializeArray):
                    InitializeArray(ref context);
                    return context.Builder.CreateUndefined();
            }
            throw context.Location.GetNotSupportedException(
                ErrorMessages.NotSupportedIntrinsic, context.Method.Name);
        }

        /// <summary>
        /// Initializes arrays.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static unsafe void InitializeArray(ref InvocationContext context)
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
        private static Value HandleArrays(ref InvocationContext context)
        {
            var builder = context.Builder;
            var location = context.Location;
            return context.Method is ConstructorInfo
                ? CreateNewArray(ref context)
                : context.Method.Name switch
                {
                    "Get" => CreateGetArrayElement(ref context),
                    "Set" => CreateSetArrayElement(ref context),
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
                    _ => throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntrinsic,
                        context.Method.Name),
                };
        }

        /// <summary>
        /// Creates a new array instance.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateNewArray(ref InvocationContext context)
        {
            var builder = context.Builder;
            var structureArgs = context.Arguments.Slice(1, context.NumArguments - 1);
            var newExtent = builder.CreateDynamicStructure(
                context.Location,
                ref structureArgs);
            var newElementType = builder.CreateType(
                context.Method.DeclaringType.GetElementType());
            return builder.CreateArray(
                context.Location,
                newElementType,
                context.NumArguments - 1,
                newExtent);
        }

        /// <summary>
        /// Gets an array element.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayElement(ref InvocationContext context)
        {
            var builder = context.Builder;
            var indices = context.Arguments.Slice(1, context.NumArguments - 1);
            return builder.CreateGetArrayElement(
                context.Location,
               context[0],
               builder.CreateDynamicStructure(
                   context.Location,
                   ref indices));
        }

        /// <summary>
        /// Sets an array element.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateSetArrayElement(ref InvocationContext context)
        {
            var builder = context.Builder;
            var indices = context.Arguments.Slice(1, context.NumArguments - 2);
            return builder.CreateSetArrayElement(
                context.Location,
                context[0],
                builder.CreateDynamicStructure(
                    context.Location,
                    ref indices),
                context[context.NumArguments - 1]);
        }

        #endregion
    }
}
