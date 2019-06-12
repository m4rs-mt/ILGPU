// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PrimitiveTypes.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a primitive type.
    /// </summary>
    public sealed class PrimitiveType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new primitive type.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        internal PrimitiveType(BasicValueType basicValueType)
        {
            BasicValueType = basicValueType;
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

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.TryResolveManagedType(out Type)"/>
        public override bool TryResolveManagedType(out Type type)
        {
            type = BasicValueType.GetManagedType();
            return true;
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            BasicValueType.ToString();

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x2AB11613 ^ (int)BasicValueType;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is PrimitiveType primitiveType)
                return primitiveType.BasicValueType == BasicValueType;
            return false;
        }

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
        internal StringType() { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.TryResolveManagedType(out Type)"/>
        public override bool TryResolveManagedType(out Type type)
        {
            type = typeof(string);
            return true;
        }

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
