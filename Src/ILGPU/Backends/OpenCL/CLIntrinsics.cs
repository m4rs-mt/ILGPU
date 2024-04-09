// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.AtomicOperations;
using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Implements and initializes OpenCL intrinsics.
    /// </summary>
    static partial class CLIntrinsics
    {
        #region Specializers

        /// <summary>
        /// The CLIntrinsics type.
        /// </summary>
        private static readonly Type CLIntrinsicsType = typeof(CLIntrinsics);

        /// <summary>
        /// Creates a new CL intrinsic.
        /// </summary>
        /// <param name="name">The name of the intrinsic.</param>
        /// <param name="mode">The implementation mode.</param>
        /// <returns>The created intrinsic.</returns>
        private static CLIntrinsic CreateIntrinsic(
            string name,
            IntrinsicImplementationMode mode) =>
            new CLIntrinsic(
                CLIntrinsicsType,
                name,
                mode);

        /// <summary>
        /// Registers all CL intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        public static void Register(IntrinsicImplementationManager manager)
        {
            // Atomics
            manager.RegisterGenericAtomic(
                AtomicKind.Add,
                BasicValueType.Float32,
                CreateIntrinsic(
                    nameof(AtomicAddF32),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Add,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicAddF64),
                    IntrinsicImplementationMode.Redirect));

            // Group
            manager.RegisterPredicateBarrier(
                PredicateBarrierKind.PopCount,
                CreateIntrinsic(
                    nameof(BarrierPopCount),
                    IntrinsicImplementationMode.Redirect));

            // IntrinsicMath
            RegisterBinaryLogMathIntrinsic(
                manager,
                BasicValueType.Float32,
                typeof(float),
                typeof(float));
            RegisterBinaryLogMathIntrinsic(
                manager,
                BasicValueType.Float64,
                typeof(double),
                typeof(double));

            RegisterRcpMathIntrinsic(manager, BasicValueType.Float32);
            RegisterRcpMathIntrinsic(manager, BasicValueType.Float64);
        }

        #endregion

        #region Atomics

        /// <summary>
        /// Represents an atomic add operation of type float.
        /// </summary>
        private readonly struct AddFloat : IAtomicOperation<float>
        {
            public float Operation(float current, float value) => current + value;
        }

        /// <summary>
        /// A software implementation for atomic adds on 32-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float AtomicAddF32(ref float target, float value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddFloat(),
                new CompareExchangeFloat());

        /// <summary>
        /// Represents an atomic add operation of type double.
        /// </summary>
        private readonly struct AddDouble : IAtomicOperation<double>
        {
            public double Operation(double current, double value) => current + value;
        }

        /// <summary>
        /// A software implementation for atomic adds on 64-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AtomicAddF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddDouble(),
                new CompareExchangeDouble());

        #endregion

        #region Groups

        /// <summary>
        /// A software implementation to simulate barriers with pop count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BarrierPopCount(bool predicate)
        {
            ref var counter = ref SharedMemory.Allocate<int>();
            if (Group.IsFirstThread)
                counter = 0;
            Group.Barrier();
            if (predicate)
                Atomic.Add(ref counter, 1);
            Group.Barrier();
            return counter;
        }

        #endregion

        #region IntrinsicMath

        /// <summary>
        /// Registers intrinsic for Log with two parameters.
        /// </summary>
        private static void RegisterBinaryLogMathIntrinsic(
            IntrinsicImplementationManager manager,
            BasicValueType basicValueType,
            params Type[] types)
        {
            var targetMethod = typeof(IntrinsicMath.BinaryLog).GetMethod(
                nameof(IntrinsicMath.BinaryLog.Log),
                BindingFlags.Public | BindingFlags.Static,
                null,
                types,
                null)
                .ThrowIfNull();
            manager.RegisterBinaryArithmetic(
                BinaryArithmeticKind.BinaryLogF,
                basicValueType,
                new CLIntrinsic(targetMethod, IntrinsicImplementationMode.Redirect));
        }

        /// <summary>
        /// Registers the Rcp intrinsic for the basic value type.
        /// </summary>
        private static void RegisterRcpMathIntrinsic(
            IntrinsicImplementationManager manager,
            BasicValueType basicValueType) =>
            manager.RegisterUnaryArithmetic(
                UnaryArithmeticKind.RcpF,
                basicValueType,
                new CLIntrinsic(
                    typeof(CLIntrinsics).GetMethod(
                        nameof(GenerateRcpMathIntrinsic),
                        BindingFlags.NonPublic | BindingFlags.Static).AsNotNull(),
                        IntrinsicImplementationMode.GenerateCode));

        /// <summary>
        /// Generates intrinsic math instructions for the following kinds:
        /// Rcp
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        private static void GenerateRcpMathIntrinsic(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
        {
            // Manually generate code for "1.0 / argument"
            var arithmeticValue = value.AsNotNullCast<UnaryArithmeticValue>();
            var argument = codeGenerator.Load(arithmeticValue.Value);
            var target = codeGenerator.Allocate(arithmeticValue);
            var operation = CLInstructions.GetArithmeticOperation(
                BinaryArithmeticKind.Div,
                arithmeticValue.BasicValueType.IsFloat(),
                out var isFunction);
            using var statement = codeGenerator.BeginStatement(target);
            statement.AppendCast(arithmeticValue.ArithmeticBasicValueType);
            if (isFunction)
            {
                statement.AppendCommand(operation);
                statement.BeginArguments();
            }
            else
            {
                statement.OpenParen();
            }

            statement.AppendCast(arithmeticValue.ArithmeticBasicValueType);
            if (arithmeticValue.BasicValueType == BasicValueType.Float32)
                statement.AppendConstant(1.0f);
            else
                statement.AppendConstant(1.0);

            if (!isFunction)
                statement.AppendCommand(operation);

            statement.AppendArgument();
            statement.AppendCast(arithmeticValue.ArithmeticBasicValueType);
            statement.Append(argument);

            if (isFunction)
                statement.EndArguments();
            else
                statement.CloseParen();
        }

        #endregion
    }
}
