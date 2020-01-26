// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeNode.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a type in the scope of the ILGPU IR.
    /// </summary>
    public abstract class TypeNode : Node
    {
        #region Instance

        /// <summary>
        /// Constructs a new type.
        /// </summary>
        protected TypeNode() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the current type is a <see cref="VoidType"/>.
        /// </summary>
        public bool IsVoidType => this is VoidType;

        /// <summary>
        /// Returns true if the current type is a <see cref="StringType"/>.
        /// </summary>
        public bool IsStringType => this is StringType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PrimitiveType"/>.
        /// </summary>
        public bool IsPrimitiveType => this is PrimitiveType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PointerType"/>
        /// or a <see cref="ViewType"/>.
        /// </summary>
        public bool IsViewOrPointerType => this is AddressSpaceType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PointerType"/>.
        /// </summary>
        public bool IsPointerType => this is PointerType;

        /// <summary>
        /// Returns true if the current type is a <see cref="ViewType"/>.
        /// </summary>
        public bool IsViewType => this is ViewType;

        /// <summary>
        /// Returns true if the current type is an <see cref="ObjectType"/>.
        /// </summary>
        public bool IsObjectType => this is ObjectType;

        /// <summary>
        /// Returns true if the current type is a <see cref="StructureType"/>.
        /// </summary>
        public bool IsStructureType => this is StructureType;

        /// <summary>
        /// Returns true if the current type is a <see cref="ArrayType"/>.
        /// </summary>
        public bool IsArrayType => this is ArrayType;

        /// <summary>
        /// Returns the basic value type.
        /// </summary>
        public BasicValueType BasicValueType
        {
            get
            {
                if (this is PrimitiveType primitiveType)
                    return primitiveType.BasicValueType;
                return BasicValueType.None;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Accepts a type node visitor.
        /// </summary>
        /// <typeparam name="T">The type of the visitor.</typeparam>
        /// <param name="visitor">The visitor.</param>
        public abstract void Accept<T>(T visitor)
            where T : ITypeNodeVisitor;

        /// <summary>
        /// Tries to resolve the managed type that represents this IR type.
        /// </summary>
        /// <param name="type">The resolved managed type that represents this IR type.</param>
        /// <returns>True, if the managed type could be resolved.</returns>
        public abstract bool TryResolveManagedType(out Type type);

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this type node.
        /// </summary>
        /// <returns>The hash code of this type node.</returns>
        public override int GetHashCode() => 0;

        /// <summary>
        /// Returns true if the given object is equal to the current type.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current type.</returns>
        public override bool Equals(object obj) =>
            obj is TypeNode;

        /// <summary>
        /// Returns the string represetation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public override string ToString() => ToPrefixString();

        #endregion
    }
}
