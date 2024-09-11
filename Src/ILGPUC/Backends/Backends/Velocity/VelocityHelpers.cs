// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityHelpers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using System;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Represents a type evaluation mode.
    /// </summary>
    enum VelocityWarpOperationMode
    {
        /// <summary>
        /// Signed integers.
        /// </summary>
        I,

        /// <summary>
        /// Unsigned integers.
        /// </summary>
        U,

        /// <summary>
        /// Floats.
        /// </summary>
        F,

        /// <summary>
        /// Doubles, or floats essentially.
        /// </summary>
        D = F,
    }

    static class VelocityHelpers
    {
        /// <summary>
        /// Returns true if the given value type is actually a 32bit value type.
        /// </summary>
        public static bool Is32Bit(this BasicValueType valueType) =>
            valueType switch
            {
                BasicValueType.Int32 => true,
                BasicValueType.Float32 => true,
                _ => false,
            };

        /// <summary>
        /// Returns true if the given value type is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this BasicValueType valueType) =>
            valueType switch
            {
                BasicValueType.Float64 => false,
                BasicValueType.Int64 => false,
                _ => true,
            };

        /// <summary>
        /// Returns true if the given value is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this Value value) =>
            value.BasicValueType.IsTreatedAs32Bit();

        /// <summary>
        /// Returns true if the given value is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this ArithmeticValue value) =>
            value.ArithmeticBasicValueType switch
            {
                ArithmeticBasicValueType.Float64 => false,
                ArithmeticBasicValueType.Int64 => false,
                ArithmeticBasicValueType.UInt64 => false,
                _ => true,
            };

        /// <summary>
        /// Determines the current warp-operation mode for the given arithmetic basic
        /// value type.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(
            this ArithmeticBasicValueType valueType) =>
            valueType switch
            {
                ArithmeticBasicValueType.UInt1 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt8 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt16 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt32 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt64 => VelocityWarpOperationMode.U,

                ArithmeticBasicValueType.Int8 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int16 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int32 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int64 => VelocityWarpOperationMode.I,

                ArithmeticBasicValueType.Float16 => VelocityWarpOperationMode.F,
                ArithmeticBasicValueType.Float32 => VelocityWarpOperationMode.F,
                ArithmeticBasicValueType.Float64 => VelocityWarpOperationMode.D,
                _ => throw new NotSupportedException()
            };

        /// <summary>
        /// Determines the current warp-operation mode for the given value.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(this ArithmeticValue value) =>
            value.ArithmeticBasicValueType.GetWarpMode();

        /// <summary>
        /// Determines the current warp-operation mode for the given value.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(this CompareValue value) =>
            value.CompareType.GetWarpMode();

        /// <summary>
        /// Gets the basic value type corresponding to the given warp mode.
        /// </summary>
        public static BasicValueType GetBasicValueType(
            this VelocityWarpOperationMode mode,
            bool is64Bit) =>
            mode switch
            {
                VelocityWarpOperationMode.I => !is64Bit
                    ? BasicValueType.Int32 : BasicValueType.Int64,
                VelocityWarpOperationMode.U => !is64Bit
                    ? BasicValueType.Int32 : BasicValueType.Int64,
                VelocityWarpOperationMode.F => !is64Bit
                    ? BasicValueType.Float32 : BasicValueType.Float64,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };

        /// <summary>
        /// Gets the arithmetic basic value type corresponding to the given warp mode.
        /// </summary>
        public static ArithmeticBasicValueType GetArithmeticBasicValueType(
            this VelocityWarpOperationMode mode,
            bool is64Bit) =>
            mode switch
            {
                VelocityWarpOperationMode.I => !is64Bit
                    ? ArithmeticBasicValueType.Int32 : ArithmeticBasicValueType.Int64,
                VelocityWarpOperationMode.U => !is64Bit
                    ? ArithmeticBasicValueType.UInt32 : ArithmeticBasicValueType.UInt64,
                VelocityWarpOperationMode.F => !is64Bit
                    ? ArithmeticBasicValueType.Float32 : ArithmeticBasicValueType.Float64,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
    }
}
