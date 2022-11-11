// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeConverter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// An abstract type converter to convert specific types.
    /// </summary>
    /// <typeparam name="TType">The type to convert.</typeparam>
    public interface ITypeConverter<TType>
        where TType : TypeNode
    {
        /// <summary>
        /// Converts the given type node.
        /// </summary>
        /// <typeparam name="TTypeContext">The type converter to use.</typeparam>
        /// <param name="typeContext">The type converter instance to use.</param>
        /// <param name="type">The type to convert.</param>
        /// <returns>The converted type.</returns>
        TypeNode ConvertType<TTypeContext>(TTypeContext typeContext, TType type)
            where TTypeContext : IIRTypeContext;

        /// <summary>
        /// Resolves the number of element fields per type instance.
        /// </summary>
        /// <param name="type">The parent type.</param>
        int GetNumFields(TType type);
    }

    /// <summary>
    /// A converter adapter to convert nested types within structures.
    /// </summary>
    /// <typeparam name="TType">The node type.</typeparam>
    public abstract class TypeConverter<TType> : ITypeConverter<TypeNode>
        where TType : TypeNode
    {
        /// <summary>
        /// Constructs a new type converter.
        /// </summary>
        protected TypeConverter() { }

        /// <summary>
        /// Converts the given type node.
        /// </summary>
        /// <typeparam name="TTypeContext">The type context to use.</typeparam>
        /// <param name="typeContext">The type context instance to use.</param>
        /// <param name="type">The type to convert.</param>
        /// <returns>The converted type.</returns>
        protected abstract TypeNode ConvertType<TTypeContext>(
            TTypeContext typeContext,
            TType type)
            where TTypeContext : IIRTypeContext;

        /// <summary>
        /// Resolves the number of element fields per type instance.
        /// </summary>
        /// <param name="type">The parent type.</param>
        protected abstract int GetNumFields(TType type);

        /// <summary>
        /// Converts the given type node.
        /// </summary>
        /// <typeparam name="TTypeContext">The type context to use.</typeparam>
        /// <param name="typeContext">The type context instance to use.</param>
        /// <param name="type">The type to convert.</param>
        /// <returns>The converted type.</returns>
        public TypeNode ConvertType<TTypeContext>(
            TTypeContext typeContext,
            TypeNode type)
            where TTypeContext : IIRTypeContext =>
            type switch
            {
                TType ttype => ConvertType(typeContext, ttype),
                StructureType structureType => structureType.ConvertFieldTypes(
                    typeContext,
                    this),
                PointerType pointerType => typeContext.CreatePointerType(
                    ConvertType(typeContext, pointerType.ElementType),
                    pointerType.AddressSpace),
                ViewType viewType => typeContext.CreateViewType(
                    ConvertType(typeContext, viewType.ElementType),
                    viewType.AddressSpace),
                _ => type,
            };

        /// <summary>
        /// Resolves the number of element fields per type instance.
        /// </summary>
        /// <param name="type">The parent type.</param>
        public int GetNumFields(TypeNode type) =>
            type is TType ttype
            ? GetNumFields(ttype)
            : 1;
    }

    /// <summary>
    /// The type converter used during lowering phases.
    /// </summary>
    /// <typeparam name="TType">The source type to lower.</typeparam>
    public abstract class TypeLowering<TType> : TypeConverter<TType>
        where TType : TypeNode
    {
        private readonly Dictionary<Value, TypeNode> typeMapping =
            new Dictionary<Value, TypeNode>();

        /// <summary>
        /// Constructs a new type lowering without a parent type context.
        /// </summary>
        protected TypeLowering() { }

        /// <summary>
        /// Constructs a new type lowering.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        protected TypeLowering(Method.Builder builder)
            : this(builder.TypeContext)
        { }

        /// <summary>
        /// Constructs a new type lowering.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        protected TypeLowering(IRBuilder builder)
            : this(builder.TypeContext)
        { }

        /// <summary>
        /// Constructs a new type lowering.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        protected TypeLowering(IRTypeContext typeContext)
        {
            Debug.Assert(typeContext != null, "Invalid type context");
            TypeContext = typeContext;
        }

        /// <summary>
        /// Returns the associated type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Maps the given new value to its original type.
        /// </summary>
        public TypeNode this[Value value] => typeMapping[value];

        /// <summary>
        /// Converts the given value type.
        /// </summary>
        /// <param name="value">The value to convert the type.</param>
        /// <returns>The converted type.</returns>
        public TypeNode ConvertType(Value value) => ConvertType(this[value]);

        /// <summary>
        /// Converts the given type node.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The converted type.</returns>
        public TypeNode ConvertType(TypeNode type)
        {
            Debug.Assert(
                TypeContext != null,
                "Lowering must be bound to a valid type context");
            return ConvertType(TypeContext, type);
        }

        /// <summary>
        /// Computes a new field span while taking all structure field changes into
        /// account.
        /// </summary>
        /// <param name="value">The source value.</param>
        /// <param name="fieldSpan">The source span.</param>
        /// <returns>The target field span.</returns>
        public FieldSpan ComputeSpan(Value value, FieldSpan fieldSpan)
        {
            var sourceType = this[value] as StructureType;
            int sourceIndex = fieldSpan.Index;
            int index = 0;
            for (int i = 0; i < sourceIndex; ++i)
            {
                // Check whether we need new offset information
                index += GetNumFields(sourceType[i]);
            }
            // Check whether we have to adapt the field span
            int span = 0;
            for (int i = 0, e = fieldSpan.Span; i < e; ++i)
            {
                // Check whether we need new offset information
                span += GetNumFields(sourceType[i + sourceIndex]);
            }

            return new FieldSpan(index, span);
        }

        /// <summary>
        /// Returns true if the given type has a type dependency.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True, if the given type has a type dependency.</returns>
        public abstract bool IsTypeDependent(TypeNode type);

        /// <summary>
        /// Registers the given value-type mapping.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="type">The mapped type.</param>
        /// <returns>True.</returns>
        public bool Register(Value value, TypeNode type)
        {
            typeMapping.Add(value, type);
            return true;
        }

        /// <summary>
        /// Tries to register the given value-type mapping.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="type">The mapped type.</param>
        /// <returns>True, if the given type is type dependent.</returns>
        public bool TryRegister(Value value, TypeNode type) =>
            IsTypeDependent(type) && Register(value, type);
    }
}
