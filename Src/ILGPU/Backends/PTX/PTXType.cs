// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Generic;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a type in PTX.
    /// </summary>
    readonly struct PTXType
    {
        #region Static

        /// <summary>
        /// Maps basic types to PTX types.
        /// </summary>
        private static readonly Dictionary<BasicValueType, PTXType> BasicTypes =
            new Dictionary<BasicValueType, PTXType>
            {
                { BasicValueType.Int1, new PTXType("pred", PTXRegisterKind.Predicate) },

                { BasicValueType.Int8, new PTXType("b8", PTXRegisterKind.Int8) },
                { BasicValueType.Int16, new PTXType("b16", PTXRegisterKind.Int16) },
                { BasicValueType.Int32, new PTXType("b32", PTXRegisterKind.Int32) },
                { BasicValueType.Int64, new PTXType("b64", PTXRegisterKind.Int64) },

                { BasicValueType.Float32, new PTXType("f32", PTXRegisterKind.Float32) },
                { BasicValueType.Float64, new PTXType("f64", PTXRegisterKind.Float64) }
            };

        /// <summary>
        /// Maps arithemtic basic types to PTX types.
        /// </summary>
        private static readonly Dictionary<ArithmeticBasicValueType, PTXType> ArithmeticBasicTypes =
            new Dictionary<ArithmeticBasicValueType, PTXType>()
            {
                { ArithmeticBasicValueType.Int8, new PTXType("s8", PTXRegisterKind.Int16) },
                { ArithmeticBasicValueType.Int16, new PTXType("s16", PTXRegisterKind.Int16) },
                { ArithmeticBasicValueType.Int32, new PTXType("s32", PTXRegisterKind.Int32) },
                { ArithmeticBasicValueType.Int64, new PTXType("s64", PTXRegisterKind.Int64) },

                { ArithmeticBasicValueType.UInt8, new PTXType("u8", PTXRegisterKind.Int16) },
                { ArithmeticBasicValueType.UInt16, new PTXType("u16", PTXRegisterKind.Int16) },
                { ArithmeticBasicValueType.UInt32, new PTXType("u32", PTXRegisterKind.Int32) },
                { ArithmeticBasicValueType.UInt64, new PTXType("u64", PTXRegisterKind.Int64) },

                { ArithmeticBasicValueType.Float32, new PTXType("f32", PTXRegisterKind.Float32) },
                { ArithmeticBasicValueType.Float64, new PTXType("f64", PTXRegisterKind.Float64) }
            };

        /// <summary>
        /// Resolves the corresponding PTX type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <param name="abi">The current ABI.</param>
        /// <returns>The resolved PTX type.</returns>
        public static PTXType GetPTXType(TypeNode type, ABI abi)
        {
            if (type.IsPointerType || type.IsReferenceType)
                return GetPTXType(abi.PointerArithmeticType);
            return GetPTXType(type.BasicValueType);
        }

        /// <summary>
        /// Resolves the corresponding PTX type.
        /// </summary>
        /// <param name="basicValueType">The basic value type to resolve.</param>
        /// <returns>The resolved PTX type.</returns>
        public static PTXType GetPTXType(BasicValueType basicValueType) =>
            BasicTypes[basicValueType];

        /// <summary>
        /// Resolves the corresponding PTX type for a return argument.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <param name="abi">The current ABI.</param>
        /// <returns>The resolved PTX type.</returns>
        public static PTXType GetPTXParameterType(TypeNode type, ABI abi)
        {
            if (type.IsPointerType || type.IsReferenceType)
                return GetPTXType(abi.PointerArithmeticType);
            // A return call cannot handle bool types -> we have to
            // move them into a temporary 32bit register
            var basicValueType = type.BasicValueType;
            switch (basicValueType)
            {
                case BasicValueType.Int1:
                    basicValueType = BasicValueType.Int32;
                    break;
            }
            return GetPTXType(basicValueType);
        }

        /// <summary>
        /// Resolves the corresponding PTX type.
        /// </summary>
        /// <param name="basicValueType">The basic value type to resolve.</param>
        /// <returns>The resolved PTX type.</returns>
        public static PTXType GetPTXType(ArithmeticBasicValueType basicValueType) =>
            ArithmeticBasicTypes[basicValueType];

        /// <summary>
        /// Resolves the corresponding PTX type.
        /// </summary>
        /// <param name="registerType">The register type.</param>
        /// <returns>The resolved PTX type.</returns>
        public static PTXType GetPTXType(PTXRegisterKind registerType)
        {
            switch (registerType)
            {
                case PTXRegisterKind.Predicate:
                    return GetPTXType(BasicValueType.Int1);
                case PTXRegisterKind.Int8:
                    return GetPTXType(BasicValueType.Int8);
                case PTXRegisterKind.Int16:
                    return GetPTXType(BasicValueType.Int16);
                case PTXRegisterKind.Int64:
                    return GetPTXType(BasicValueType.Int64);
                case PTXRegisterKind.Float32:
                    return GetPTXType(BasicValueType.Float32);
                case PTXRegisterKind.Float64:
                    return GetPTXType(BasicValueType.Float64);
                default:
                    return GetPTXType(BasicValueType.Int32);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX type.
        /// </summary>
        /// <param name="name">The PTX name.</param>
        /// <param name="registerType">The register type.</param>
        private PTXType(string name, PTXRegisterKind registerType)
        {
            Name = name;
            RegisterKind = registerType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the type name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the register type that corresponds to this type.
        /// </summary>
        public PTXRegisterKind RegisterKind { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns the <see cref="Name"/> of this type.
        /// </summary>
        /// <returns>The name of this type.</returns>
        public override string ToString() => Name;

        #endregion

        #region Operators

        /// <summary>
        /// Converts a PTX type into its string representation.
        /// </summary>
        /// <param name="type">The PTX type.</param>
        public static implicit operator string(PTXType type) => type.ToString();

        #endregion
    }
}
