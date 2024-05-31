// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeKind.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Reflection;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Enumeration of tHhe various special kinds of <see cref="TypeNode"/>.
    /// </summary>
    public enum TypeKind
    {
        /// <summary>
        /// Fallback value for when the classification is unknown or doesn't apply
        /// </summary>
        Unknown,

        /// <summary>
        /// See <see cref="VoidType"/>
        /// </summary>
        Void,

        /// <summary>
        /// See <see cref="StringType"/>
        /// </summary>
        String,

        /// <summary>
        /// See <see cref="PrimitiveType"/>
        /// </summary>
        Primitive,

        /// <summary>
        /// See <see cref="PaddingType"/>
        /// </summary>
        Padding,

        /// <summary>
        /// See <see cref="PointerType"/>
        /// </summary>
        Pointer,

        /// <summary>
        /// See <see cref="ViewType"/>
        /// </summary>
        View,

        /// <summary>
        /// See <see cref="ArrayType"/>
        /// </summary>
        Array,

        /// <summary>
        /// See <see cref="StructureType"/>
        /// </summary>
        Structure,

        /// <summary>
        /// See <see cref="HandleType"/>
        /// </summary>
        Handle,

        /// <summary>
        /// Represents the number of distinct type kinds described by this enumeration
        /// </summary>
        MaxValue,
    }

    /// <summary>
    /// Marks value classes with specific type kinds.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class TypeKindAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new type kind attribute.
        /// </summary>
        /// <param name="kind">The type kind.</param>
        public TypeKindAttribute(TypeKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// Returns the type kind.
        /// </summary>
        public TypeKind Kind { get; }
    }

    /// <summary>
    /// Utility methods for <see cref="TypeKind"/> enumeration values.
    /// </summary>
    public static class TypeKinds
    {
        /// <summary>
        /// The number of different value kinds.
        /// </summary>
        public const int NumTypeKinds = (int)TypeKind.MaxValue;

        /// <summary>
        /// Gets the value kind of the value type specified.
        /// </summary>
        /// <typeparam name="TType">The compile-time type.</typeparam>
        /// <returns>The determined type kind.</returns>
        public static TypeKind GetTypeKind<TType>()
            where TType : TypeNode =>
            typeof(TType).GetCustomAttribute<TypeKindAttribute>().AsNotNull().Kind;

        /// <summary>
        /// Gets the type kind of the type specified.
        /// </summary>
        /// <returns>The determined type kind.</returns>
        public static TypeKind? GetTypeKind(Type type) =>
            type.GetCustomAttribute<TypeKindAttribute>()?.Kind;
    }
}
