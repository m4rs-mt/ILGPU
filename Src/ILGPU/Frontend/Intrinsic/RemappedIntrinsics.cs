// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RemappedIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

// Enforce DEBUG mode in all cases to preserve Debug calls
#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Frontend.Intrinsic
{
    /// <summary>
    /// Contains default remapped ILGPU intrinsics.
    /// </summary>
    public static partial class RemappedIntrinsics
    {
        #region Static Handler

        /// <summary>
        /// The global <see cref="IntrinsicMath"/> type.
        /// </summary>
        public static readonly Type MathType = typeof(IntrinsicMath);

        /// <summary>
        /// The global <see cref="IntrinsicMath.CPUOnly"/> type.
        /// </summary>
        public static readonly Type CPUMathType = typeof(IntrinsicMath.CPUOnly);

        /// <summary>
        /// Represents a basic remapper for compiler-specific device functions.
        /// </summary>
        public delegate void DeviceFunctionRemapper(ref InvocationContext context);

        /// <summary>
        /// Stores function remappers.
        /// </summary>
        private static readonly Dictionary<MethodBase, DeviceFunctionRemapper>
            FunctionRemappers =
            new Dictionary<MethodBase, DeviceFunctionRemapper>();

        [SuppressMessage(
            "Microsoft.Performance",
            "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Caching of compiler-known functions")]
        static RemappedIntrinsics()
        {
            var remappedType = typeof(RemappedIntrinsics);

            AddRemapping(
                typeof(float),
                CPUMathType,
                nameof(float.IsNaN),
                typeof(float));
            AddRemapping(
                typeof(float),
                CPUMathType,
                nameof(float.IsInfinity),
                typeof(float));

            AddRemapping(
                typeof(double),
                CPUMathType,
                nameof(double.IsNaN),
                typeof(double));
            AddRemapping(
                typeof(double),
                CPUMathType,
                nameof(double.IsInfinity),
                typeof(double));

            RegisterMathRemappings();

            // Remap debug assert
            var debugType = typeof(Debug);
            AddDebugRemapping(
                remappedType,
                nameof(DebugAssertCondition),
                debugType,
                nameof(Debug.Assert),
                new Type[] { typeof(bool) });
            AddDebugRemapping(
                remappedType,
                nameof(DebugAssertConditionMessage),
                debugType,
                nameof(Debug.Assert),
                new Type[] { typeof(bool), typeof(string) });
        }

        /// <summary>
        /// Registers a new debug mapping.
        /// </summary>
        /// <param name="remappedType">The remapped intrinsics type.</param>
        /// <param name="internalMethod">The internal method name.</param>
        /// <param name="debugType">The debug type.</param>
        /// <param name="method">The original method name.</param>
        /// <param name="parameters">The parameters types of all functions.</param>
        private static void AddDebugRemapping(
            Type remappedType,
            string internalMethod,
            Type debugType,
            string method,
            Type[] parameters)
        {
            var targetMethod = remappedType.GetMethod(
                internalMethod,
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameters,
                null);

            var debugMethod = debugType.GetMethod(
                method,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameters,
                null);
            AddRemapping(debugMethod,
                (ref InvocationContext context) => context.Method = targetMethod);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Registers a math mapping for a function from a source type to a target type.
        /// </summary>
        /// <param name="sourceType">The source math type.</param>
        /// <param name="targetType">The target math type.</param>
        /// <param name="functionName">
        /// The name of the function in the scope of mathType.
        /// </param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        public static void AddRemapping(
            Type sourceType,
            Type targetType,
            string functionName,
            params Type[] paramTypes)
        {
            var mathFunc = sourceType.GetMethod(
                functionName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                paramTypes,
                null);
            var gpuMathFunc = targetType.GetMethod(
                functionName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                paramTypes,
                null);

            AddRemapping(
                mathFunc,
                (ref InvocationContext context) => context.Method = gpuMathFunc);
        }

        /// <summary>
        /// Registers a global remapping for the given method object.
        /// </summary>
        /// <param name="methodInfo">The method to remap.</param>
        /// <param name="remapper">The remapping method.</param>
        /// <remarks>
        /// This method is not thread safe.
        /// </remarks>
        public static void AddRemapping(
            MethodInfo methodInfo,
            DeviceFunctionRemapper remapper)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));
            FunctionRemappers[methodInfo] = remapper
                ?? throw new ArgumentNullException(nameof(remapper));
        }

        /// <summary>
        /// Tries to remap the given invocation context.
        /// </summary>
        /// <param name="context">The invocation context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemapIntrinsic(ref InvocationContext context)
        {
            if (FunctionRemappers.TryGetValue(context.Method, out var remapper))
                remapper(ref context);
        }

        /// <summary>
        /// Implements a simple debug assertion.
        /// </summary>
        /// <param name="condition">The assertion condition.</param>
        [SuppressMessage(
            "Microsoft.Globalization",
            "CA1303:DoNotPassLiteralsAsLocalizedParameters",
            Justification = "ILGPU cannot load constants from global resource " +
            "tables at the moment")]
        private static void DebugAssertCondition(bool condition) =>
            DebugAssertConditionMessage(condition, "Assertion failed");

        /// <summary>
        /// Implements a simple debug assertion.
        /// </summary>
        /// <param name="condition">The assertion condition.</param>
        /// <param name="message">The error message.</param>
        private static void DebugAssertConditionMessage(bool condition, string message)
        {
            if (!condition)
                Debug.Fail(message);
        }

        #endregion
    }
}
