// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: RemappedIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
        /// Represents a basic remapper for compiler-specific device functions.
        /// </summary>
        public delegate InvocationContext? DeviceFunctionRemapper(in InvocationContext context);

        /// <summary>
        /// Stores function remappers.
        /// </summary>
        private static readonly Dictionary<MethodBase, DeviceFunctionRemapper> FunctionRemappers =
            new Dictionary<MethodBase, DeviceFunctionRemapper>();

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Caching of compiler-known functions")]
        static RemappedIntrinsics()
        {
            var remappedType = typeof(RemappedIntrinsics);

            var mathType = typeof(Math);

            AddMathRemapping(mathType, "Abs", new Type[] { typeof(sbyte) });
            AddMathRemapping(mathType, "Abs", new Type[] { typeof(short) });
            AddMathRemapping(mathType, "Abs", new Type[] { typeof(int) });
            AddMathRemapping(mathType, "Abs", new Type[] { typeof(long) });
            AddMathRemapping(mathType, "Abs", new Type[] { typeof(float) });
            AddMathRemapping(mathType, "Abs", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Sqrt", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Sin", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Sinh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Cos", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Cosh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Tan", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Tanh", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Pow", new Type[] { typeof(double), typeof(double) });

            AddMathRemapping(mathType, "Exp", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Floor", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Ceiling", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Log", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Log", new Type[] { typeof(double), typeof(double) });
            AddMathRemapping(mathType, "Log10", new Type[] { typeof(double) });

            AddMathRemapping(mathType, "Min", new Type[] { typeof(sbyte), typeof(sbyte) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(short), typeof(short) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(int), typeof(int) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(long), typeof(long) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(byte), typeof(byte) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(ushort), typeof(ushort) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(uint), typeof(uint) });
            AddMathRemapping(mathType, "Min", new Type[] { typeof(ulong), typeof(ulong) });

            AddMathRemapping(mathType, "Max", new Type[] { typeof(sbyte), typeof(sbyte) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(short), typeof(short) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(int), typeof(int) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(long), typeof(long) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(byte), typeof(byte) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(ushort), typeof(ushort) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(uint), typeof(uint) });
            AddMathRemapping(mathType, "Max", new Type[] { typeof(ulong), typeof(ulong) });

            AddMathRemapping(mathType, "Sign", new Type[] { typeof(double) });
            AddMathRemapping(mathType, "Sign", new Type[] { typeof(float) });

            // Note: BigMul and DivRem can be mapped automatically (since they are implemented in il).

            var floatType = typeof(float);
            AddMathRemapping(floatType, "IsNaN", new Type[] { floatType });
            AddMathRemapping(floatType, "IsInfinity", new Type[] { floatType });

            var doubleType = typeof(double);
            AddMathRemapping(doubleType, "IsNaN", new Type[] { doubleType });
            AddMathRemapping(doubleType, "IsInfinity", new Type[] { doubleType });

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
        /// Registers a math mapping for a function from mathType via ILGPU.GPUMath.
        /// </summary>
        /// <param name="mathType">The scope of the function.</param>
        /// <param name="mathName">The name of the function in the scope of mathType.</param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        private static void AddMathRemapping(Type mathType, string mathName, Type[] paramTypes)
        {
            AddMathRemapping(mathType, mathName, mathName, paramTypes);
        }

        /// <summary>
        /// Registers a math mapping for a function from mathType via ILGPU.GPUMath.
        /// </summary>
        /// <param name="mathType">The scope of the function.</param>
        /// <param name="mathName">The name of the function in the scope of mathType.</param>
        /// <param name="gpuMathName">The name of the function in the scope of ILGPU.GPUMath.</param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        private static void AddMathRemapping(Type mathType, string mathName, string gpuMathName, Type[] paramTypes)
        {
            var mathFunc = mathType.GetMethod(
                mathName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                paramTypes,
                null);
            Debug.Assert(mathFunc != null, "Invalid source function");
            var gpuMathFunc = typeof(XMath).GetMethod(
                gpuMathName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                paramTypes,
                null);
            Debug.Assert(gpuMathFunc != null, "Invalid target function");
            RegisterRemapping(
                mathFunc,
                (in InvocationContext context) => context.Remap(gpuMathFunc, context.Arguments));
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
            RegisterRemapping(debugMethod,
                (in InvocationContext context) => context.Remap(targetMethod, context.Arguments));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Registers a global remapping for the given method object.
        /// </summary>
        /// <param name="methodInfo">The method to remap.</param>
        /// <param name="remapper">The remapping method.</param>
        /// <remarks>
        /// This method is not thread safe.
        /// </remarks>
        public static void RegisterRemapping(MethodInfo methodInfo, DeviceFunctionRemapper remapper)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));
            if (remapper == null)
                throw new ArgumentNullException(nameof(remapper));

            FunctionRemappers[methodInfo] = remapper;
        }

        /// <summary>
        /// Tries to remap the given invocation context.
        /// </summary>
        /// <param name="context">The invocation context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemapIntrinsic(ref InvocationContext context)
        {
            if (FunctionRemappers.TryGetValue(context.Method, out DeviceFunctionRemapper remapper))
            {
                var newContext = remapper(context);
                if (newContext.HasValue)
                    context = newContext.Value;
            }
        }

        /// <summary>
        /// Implements a simple debug assertion.
        /// </summary>
        /// <param name="condition">The assertion condition.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters",
            Justification = "ILGPU cannot load constants from global resource tables at the moment")]
        private static void DebugAssertCondition(bool condition)
        {
            DebugAssertConditionMessage(condition, "Assertion failed");
        }

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
