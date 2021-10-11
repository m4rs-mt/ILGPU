﻿// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.CL
{
    static class CLMath
    {
        #region Code Generator

        /// <summary>
        /// Generates intrinsic math instructions for the following kinds:
        /// Rcp
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateMathIntrinsic(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
        {
            // Manually generate code for "1.0 / argument"
            var arithmeticValue = value as UnaryArithmeticValue;
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
                statement.OpenParen();

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

        #region IsNaN & IsInfinity

        /// <summary cref="XMath.IsNaN(double)"/>
        public static bool IsNaN(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.IsNaN(float)"/>
        public static bool IsNaN(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.IsInfinity(double)"/>
        public static bool IsInfinity(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.IsInfinity(float)"/>
        public static bool IsInfinity(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Rcp

        /// <summary cref="XMath.Rcp(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rcp(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Rcp(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rcp(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Rem

        /// <summary cref="XMath.Rem(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rem(double x, double y) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Rem(float, float)"/>
        public static float Rem(float x, float y) =>
            throw new NotImplementedException();

        #endregion

        #region Sqrt

        /// <summary cref="XMath.Sqrt(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sqrt(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Sqrt(float)" />
        public static float Sqrt(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Rsqrt(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rsqrt(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Rsqrt(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rsqrt(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Floor & Ceiling

        /// <summary cref="XMath.Floor(double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Floor(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Floor(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Ceiling(double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Ceiling(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Ceiling(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Trig

        /// <summary cref="XMath.Sin(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sin(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Sin(float)" />
        public static float Sin(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Sinh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sinh(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Sinh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sinh(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Asin(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Asin(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Asin(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Asin(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Cos(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cos(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Cos(float)" />
        public static float Cos(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Cosh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cosh(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Cosh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cosh(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Acos(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Acos(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Acos(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Acos(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Tan(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tan(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Tan(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tan(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Tanh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tanh(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Tanh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tanh(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Atan(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Atan(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Atan2(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan2(double y, double x) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Atan2(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x) =>
            throw new NotImplementedException();

        #endregion

        #region Pow

        /// <summary cref="XMath.Pow(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Pow(double @base, double exp) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Pow(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float @base, float exp) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Exp(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Exp(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Exp2(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp2(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Exp2(float)" />
        public static float Exp2(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Log

        /// <summary cref="XMath.Log(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double value, double newBase)
        {
            if (value < 0.0 ||
                newBase < 0.0 ||
                (value != 1.0 && newBase == 0.0) ||
                (value != 1.0 && newBase == double.PositiveInfinity) ||
                XMath.IsNaN(value) ||
                XMath.IsNaN(newBase) ||
                newBase == 1.0)
            {
                return double.NaN;
            }

            if (value == 0.0)
            {
                if (0.0 < newBase && newBase < 1.0)
                    return double.PositiveInfinity;
                else if (newBase > 1.0)
                    return double.NegativeInfinity;
            }

            if (value == double.PositiveInfinity)
            {
                if (0.0 < newBase && newBase < 1.0)
                    return double.NegativeInfinity;
                else if (newBase > 1.0)
                    return double.PositiveInfinity;
            }

            if (value == 1.0 && (newBase == 0.0 || newBase == double.PositiveInfinity))
                return 0.0;

            return XMath.Log(value) * XMath.Rcp(XMath.Log(newBase));
        }

        /// <summary cref="XMath.Log(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value, float newBase)
        {
            if (value < 0.0f ||
                newBase < 0.0f ||
                (value != 1.0f && newBase == 0.0f) ||
                (value != 1.0f && newBase == float.PositiveInfinity) ||
                XMath.IsNaN(value) ||
                XMath.IsNaN(newBase) ||
                newBase == 1.0f)
            {
                return float.NaN;
            }

            if (value == 0.0f)
            {
                if (0.0f < newBase && newBase < 1.0f)
                    return float.PositiveInfinity;
                else if (newBase > 1.0f)
                    return float.NegativeInfinity;
            }

            if (value == float.PositiveInfinity)
            {
                if (0.0f < newBase && newBase < 1.0f)
                    return float.NegativeInfinity;
                else if (newBase > 1.0f)
                    return float.PositiveInfinity;
            }

            if (value == 1.0f && (newBase == 0.0f || newBase == float.PositiveInfinity))
                return 0.0f;

            return XMath.Log(value) * XMath.Rcp(XMath.Log(newBase));
        }

        /// <summary cref="XMath.Log(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Log(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Log10(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log10(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Log10(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log10(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Log2(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log2(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Log2(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log2(float value) =>
            throw new NotImplementedException();

        #endregion
    }
}
