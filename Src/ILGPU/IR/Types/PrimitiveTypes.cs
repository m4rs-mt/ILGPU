// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PrimitiveTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Immutable;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a primitive type.
    /// </summary>
    public sealed class PrimitiveType : TypeNode
    {
        #region Static

        /// <summary>
        /// Contains default size information about built-in types.
        /// </summary>
        private static readonly ImmutableArray<int> BasicTypeInformation =
            ImmutableArray.Create(
                -1, // None
                4,
                1,
                2,
                4,
                8,
                4,
                8);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new primitive type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="basicValueType">The basic value type.</param>
        internal PrimitiveType(IRTypeContext typeContext, BasicValueType basicValueType)
            : base(typeContext)
        {
            BasicValueType = basicValueType;

            Size = Alignment = BasicTypeInformation[(int)basicValueType];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public new BasicValueType BasicValueType { get; }

        /// <summary>
        /// Returns true if this type represents a bool type.
        /// </summary>
        public bool IsBool => BasicValueType == BasicValueType.Int1;

        /// <summary>
        /// Returns true if this type represents a 32 bit type.
        /// </summary>
        public bool Is32Bit =>
            BasicValueType == BasicValueType.Int32 ||
            BasicValueType == BasicValueType.Float32;

        /// <summary>
        /// Returns true if this type represents a 64 bit type.
        /// </summary>
        public bool Is64Bit =>
            BasicValueType == BasicValueType.Int64 ||
            BasicValueType == BasicValueType.Float64;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the corresponding managed basic value type.
        /// </summary>
        protected override Type GetManagedType() => BasicValueType.GetManagedType();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            BasicValueType.ToString();

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x2AB11613 ^ (int)BasicValueType;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is PrimitiveType primitiveType &&
            primitiveType.BasicValueType == BasicValueType;

        #endregion
    }

    /// <summary>
    /// Represents a string type.
    /// </summary>
    public sealed class StringType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new string type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        internal StringType(IRTypeContext typeContext)
            : base(typeContext)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the corresponding managed basic value type.
        /// </summary>
        protected override Type GetManagedType() => typeof(string);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "string";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x3D12C251;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is StringType && base.Equals(obj);

        #endregion
    }
}
