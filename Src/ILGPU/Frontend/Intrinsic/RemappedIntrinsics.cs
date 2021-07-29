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

using System;
using System.Collections.Generic;
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
            RegisterBitConverterRemappings();
            RegisterCopySignRemappings();
            RegisterInterlockedRemappings();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Registers a mapping for a function from a source type to a target type.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="functionName">
        /// The name of the function in the scope of sourceType.
        /// </param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        public static void AddRemapping(
            Type sourceType,
            Type targetType,
            string functionName,
            params Type[] paramTypes) =>
            AddRemapping(
                sourceType,
                targetType,
                functionName,
                required: true,
                paramTypes);

        /// <summary>
        /// Registers a mapping for a function from a source type to a target type.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="functionName">
        /// The name of the function in the scope of sourceType.
        /// </param>
        /// <param name="required">Indicates if the mapping is optional.</param>
        /// <param name="paramTypes">The parameter types of both functions.</param>
        private static void AddRemapping(
            Type sourceType,
            Type targetType,
            string functionName,
            bool required,
            params Type[] paramTypes)
        {
            var srcFunc = sourceType.GetMethod(
                functionName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                paramTypes,
                null);
            if (srcFunc != null)
            {
                var dstFunc = targetType.GetMethod(
                    functionName,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    paramTypes,
                    null);
                if (dstFunc != null)
                {
                    AddRemapping(
                        srcFunc,
                        (ref InvocationContext context) => context.Method = dstFunc);
                }
                else if (required)
                {
                    throw new MissingMethodException(targetType.FullName, functionName);
                }
            }
            else if (required)
            {
                throw new MissingMethodException(sourceType.FullName, functionName);
            }
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
                                            ?? throw new ArgumentNullException(
                                                nameof(remapper));
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

        #endregion

        #region BitConverter remappings

        /// <summary>
        /// Internal class to handle the signed/unsigned difference between functions
        /// of <see cref="System.BitConverter"/> and <see cref="Interop"/>.
        /// </summary>
        static class BitConverter
        {
            /// <summary cref="System.BitConverter.DoubleToInt64Bits(double)"/>
            public static long DoubleToInt64Bits(double value) =>
                (long)Interop.FloatAsInt(value);

            /// <summary cref="System.BitConverter.Int64BitsToDouble(long)"/>
            public static double Int64BitsToDouble(long value) =>
                Interop.IntAsFloat((ulong)value);

#if !NETFRAMEWORK
            /// <summary cref="System.BitConverter.SingleToInt32Bits(float)"/>
            public static int SingleToInt32Bits(float value) =>
                (int)Interop.FloatAsInt(value);

            /// <summary cref="System.BitConverter.Int32BitsToSingle(int)"/>
            public static float Int32BitsToSingle(int value) =>
                Interop.IntAsFloat((uint)value);
#endif
        }

        /// <summary>
        /// Registers instrinsic mappings for BitConverter functions.
        /// </summary>
        private static void RegisterBitConverterRemappings()
        {
            AddRemapping(
                typeof(System.BitConverter),
                typeof(BitConverter),
                nameof(System.BitConverter.DoubleToInt64Bits),
                typeof(double));
            AddRemapping(
                typeof(System.BitConverter),
                typeof(BitConverter),
                nameof(System.BitConverter.Int64BitsToDouble),
                typeof(long));

#if !NETFRAMEWORK
            AddRemapping(
                typeof(System.BitConverter),
                typeof(BitConverter),
                nameof(System.BitConverter.SingleToInt32Bits),
                typeof(float));
            AddRemapping(
                typeof(System.BitConverter),
                typeof(BitConverter),
                nameof(System.BitConverter.Int32BitsToSingle),
                typeof(int));
#endif
        }

        #endregion

        #region CopySign remappings

        private static void RegisterCopySignRemappings()
        {
#if !NETFRAMEWORK
            AddRemapping(
                typeof(Math),
                MathType,
                "CopySign",
                typeof(double),
                typeof(double));
            AddRemapping(
                typeof(MathF),
                MathType,
                "CopySign",
                typeof(float),
                typeof(float));
#endif
        }

        #endregion

        #region Interlocked remappings

        /// <summary>
        /// Internal class to handle the differences between functions of
        /// <see cref="System.Threading.Interlocked"/> and <see cref="Atomic"/>.
        /// </summary>
        static class Interlocked
        {
            public static int Increment(ref int value) => Atomic.Add(ref value, 1);
            public static long Increment(ref long value) => Atomic.Add(ref value, 1L);
            public static uint Increment(ref uint value) => Atomic.Add(ref value, 1U);
            public static ulong Increment(ref ulong value) => Atomic.Add(ref value, 1UL);

            public static int Decrement(ref int value) => Atomic.Add(ref value, -1);
            public static long Decrement(ref long value) => Atomic.Add(ref value, -1L);

            public static uint Decrement(ref uint value) =>
                (uint)Atomic.Add(ref Unsafe.As<uint, int>(ref value), -1);

            public static ulong Decrement(ref ulong value) =>
                (ulong)Atomic.Add(ref Unsafe.As<ulong, long>(ref value), -1L);
        }

        #endregion
    }
}
