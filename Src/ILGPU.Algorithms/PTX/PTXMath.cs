// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: PTXMath.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Backends.PTX;
using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.PTX
{
    /// <summary>
    /// Custom PTX-specific implementations.
    /// </summary>
    static class PTXMath
    {
        #region Code Generator

        /// <summary>
        /// Generates intrinsic math instructions for the following kinds:
        /// Rcp, Sqrt, Sin, Cos, Exp2, Log2, IsInf, IsNaN
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateMathIntrinsic(
            PTXBackend backend,
            PTXCodeGenerator codeGenerator,
            Value value)
        {
            var arithmeticValue = value as UnaryArithmeticValue;
            var instruction = PTXInstructions.GetArithmeticOperation(
                arithmeticValue.Kind,
                arithmeticValue.ArithmeticBasicValueType,
                codeGenerator.FastMath);

            var argument = codeGenerator.LoadPrimitive(arithmeticValue.Value);
            var targetRegister = codeGenerator.AllocateHardware(arithmeticValue);
            using (var command = codeGenerator.BeginCommand(instruction))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(argument);
            }
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
        public static double Rcp(double value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Rcp(float)" />
        public static float Rcp(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Rem

        /// <summary cref="XMath.Rem(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rem(double x, double y)
        {
            var xDivY = XMath.Abs(x * XMath.Rcp(y));
            var result = (xDivY - Floor(xDivY)) * XMath.Abs(y);
            return Utilities.Select(x < 0.0, -result, result);
        }

        /// <summary cref="XMath.Rem(float, float)"/>
        public static float Rem(float x, float y)
        {
            var xDivY = XMath.Abs(x * XMath.Rcp(y));
            var result = (xDivY - Floor(xDivY)) * XMath.Abs(y);
            return Utilities.Select(x < 0.0f, -result, result);
        }

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
            XMath.Rcp(XMath.Sqrt(value));

        /// <summary cref="XMath.Rsqrt(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rsqrt(float value) =>
            XMath.Rcp(XMath.Sqrt(value));

        #endregion

        #region Floor & Ceiling

        /// <summary cref="XMath.Floor(double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Floor(double value)
        {
            var intValue = (int)value;
            return Utilities.Select(value < intValue, intValue - 1.0, intValue);
        }

        /// <summary cref="XMath.Floor(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float value)
        {
            var intValue = (int)value;
            return Utilities.Select(value < intValue, intValue - 1.0f, intValue);
        }

        /// <summary cref="XMath.Ceiling(double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Ceiling(double value) =>
            -Floor(-value);

        /// <summary cref="XMath.Ceiling(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float value) =>
            -Floor(-value);

        #endregion

        #region Trig

        /// <summary cref="XMath.Sin(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sin(double value) =>
            XMath.Sin((float)value);

        /// <summary cref="XMath.Sin(float)" />
        public static float Sin(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Sinh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sinh(double value) =>
            0.5 * (Exp(value) - Exp(-value));

        /// <summary cref="XMath.Sinh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sinh(float value) =>
            0.5f * (Exp(value) - Exp(-value));

        /// <summary cref="XMath.Asin(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Asin(double value)
        {
            double arg = value * Rsqrt(1.0 - value * value);
            return Atan(arg);
        }

        /// <summary cref="XMath.Asin(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Asin(float value)
        {
            float arg = value * Rsqrt(1.0f - value * value);
            return Atan(arg);
        }

        /// <summary cref="XMath.Cos(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cos(double value) =>
            XMath.Cos((float)value);

        /// <summary cref="XMath.Cos(float)" />
        public static float Cos(float value) =>
            throw new NotImplementedException();

        /// <summary cref="XMath.Cosh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cosh(double value) =>
            0.5 * (Exp(value) + Exp(-value));

        /// <summary cref="XMath.Cosh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cosh(float value) =>
            0.5f * (Exp(value) + Exp(-value));

        /// <summary cref="XMath.Acos(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Acos(double value) =>
            XMath.PIHalfD - Asin(value);

        /// <summary cref="XMath.Acos(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Acos(float value) =>
            XMath.PIHalf - Asin(value);

        /// <summary cref="XMath.Tan(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tan(double value)
        {
            var sin = Sin(value);
            var cos = Cos(value);
            return sin * XMath.Rcp(cos);
        }

        /// <summary cref="XMath.Tan(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tan(float value)
        {
            var sin = XMath.Sin(value);
            var cos = XMath.Cos(value);
            return sin * XMath.Rcp(cos);
        }

        /// <summary cref="XMath.Tanh(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tanh(double value)
        {
            var exp = Exp(2.0 * value);
            var denominator = XMath.Rcp(exp + 1.0);
            return (exp - 1.0) * denominator;
        }

        /// <summary cref="XMath.Tanh(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tanh(float value)
        {
            var exp = Exp(2.0f * value);
            var denominator = XMath.Rcp(exp + 1.0f);
            return (exp - 1.0f) * denominator;
        }

        /// <summary cref="XMath.Atan(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan(double value)
        {
            // https://de.wikipedia.org/wiki/Arkustangens_und_Arkuskotangens
            var valueSq = value * value;
            if (XMath.Abs(value) <= 1.0)
                return value * XMath.Rcp(1.0 + 0.28 * valueSq);
            else
            {
                var result = value * XMath.Rcp(0.28 + valueSq);
                return Utilities.Select(
                    value > 1.0,
                    XMath.PIHalfD - result,
                    -XMath.PIHalfD - result);
            }
        }

        /// <summary cref="XMath.Atan(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan(float value)
        {
            // https://de.wikipedia.org/wiki/Arkustangens_und_Arkuskotangens
            var valueSq = value * value;
            if (XMath.Abs(value) <= 1.0f)
                return value * XMath.Rcp(1.0f + 0.28f * valueSq);
            else
            {
                var result = value * XMath.Rcp(0.28f + valueSq);
                return Utilities.Select(
                    value > 1.0f,
                    XMath.PIHalf - result,
                    -XMath.PIHalf - result);
            }
        }

        /// <summary cref="XMath.Atan2(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan2(double y, double x)
        {
            // https://de.wikipedia.org/wiki/Arctan2
            if (x > 0.0)
                return Atan(y * XMath.Rcp(x));
            else if (x < 0.0)
            {
                if (XMath.Abs(y) < 0.00001)
                    return XMath.PID;
                var result = Atan(y * XMath.Rcp(x));
                return Utilities.Select(y > 0.0, result + XMath.PID, result - XMath.PID);
            }
            else // x == 0.0
                return Utilities.Select(y > 0.0, XMath.PIHalfD, -XMath.PIHalfD);
        }

        /// <summary cref="XMath.Atan2(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x)
        {
            // https://de.wikipedia.org/wiki/Arctan2
            if (x > 0.0f)
                return Atan(y * XMath.Rcp(x));
            else if (x < 0.0f)
            {
                if (XMath.Abs(y) < 0.00001f)
                    return XMath.PI;
                var result = Atan(y * XMath.Rcp(x));
                return Utilities.Select(y > 0.0f, result + XMath.PI, result - XMath.PI);
            }
            else // x == 0.0f
                return Utilities.Select(y > 0.0f, XMath.PIHalf, -XMath.PIHalf);
        }

        #endregion

        #region Pow

        /// <summary cref="XMath.Pow(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Pow(double @base, double exp) =>
            Exp(exp * Log(@base));

        /// <summary cref="XMath.Pow(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float @base, float exp) =>
            Exp(exp * Log(@base));

        /// <summary cref="XMath.Exp(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp(double value) =>
            Exp2(value * XMath.OneOverLn2D);

        /// <summary cref="XMath.Exp(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float value) =>
            XMath.Exp2(value * XMath.OneOverLn2);

        /// <summary cref="XMath.Exp2(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp2(double value) =>
            XMath.Exp2((float)value);

        /// <summary cref="XMath.Exp2(float)" />
        public static float Exp2(float value) =>
            throw new NotImplementedException();

        #endregion

        #region Log

        /// <summary cref="XMath.Log(double, double)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double value, double newBase) =>
            Log(value) * XMath.Rcp(Log(newBase));

        /// <summary cref="XMath.Log(float, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value, float newBase) =>
            Log(value) * XMath.Rcp(Log(newBase));

        /// <summary cref="XMath.Log(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log(double value) =>
            Log2(value) * XMath.OneOverLog2ED;

        /// <summary cref="XMath.Log(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value) =>
            XMath.Log2(value) * XMath.OneOverLog2E;

        /// <summary cref="XMath.Log10(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log10(double value) =>
            Log(value) * XMath.OneOverLn10;

        /// <summary cref="XMath.Log10(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log10(float value) =>
            XMath.Log(value) * XMath.OneOverLn10;

        /// <summary cref="XMath.Log2(double)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log2(double value) =>
            XMath.Log2((float)value);

        /// <summary cref="XMath.Log2(float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log2(float value) =>
            throw new NotImplementedException();

        #endregion
    }
}
