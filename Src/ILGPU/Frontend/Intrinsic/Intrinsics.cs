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

using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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
                }
            }

            if (FunctionHandlers.TryGetValue(method.DeclaringType, out DeviceFunctionHandler handler))
                result = handler(context);

            return result.IsValid;
        }

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

        #endregion
    }
}
