// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Generates OpenCL type structures.
    /// </summary>
    public sealed class CLTypeGenerator
    {
        #region Constants

        /// <summary>
        /// The string format of a single structure-like type.
        /// </summary>
        public const string TypeNameFormat = "_type_{0}";

        /// <summary>
        /// The string format of a single structure field.
        /// </summary>
        public const string FieldNameFormat = "field_{0}";

        /// <summary>
        /// The field index of the pointer field inside a view.
        /// </summary>
        public const int ViewPointerFieldIndex = 0;

        /// <summary>
        /// The field index of the length field inside a view.
        /// </summary>
        public const int ViewLengthFieldIndex = 1;

        /// <summary>
        /// The name of the pointer field inside a view.
        /// </summary>
        public static readonly string ViewPointerName =
            string.Format(TypeNameFormat, ViewPointerFieldIndex.ToString());

        /// <summary>
        /// The name of the length field inside a view.
        /// </summary>
        public static readonly string ViewLengthName =
            string.Format(TypeNameFormat, ViewLengthFieldIndex.ToString());

        #endregion

        #region Nested Types

        /// <summary>
        /// An internal type visitor to define structure types.
        /// </summary>
        private readonly struct TypeVisitor : ITypeNodeVisitor
        {
            /// <summary>
            /// Constructs a new type visitor.
            /// </summary>
            /// <param name="typeGenerator">The parent type generator.</param>
            public TypeVisitor(CLTypeGenerator typeGenerator)
            {
                TypeGenerator = typeGenerator;
                Builder = typeGenerator.Builder;
            }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLTypeGenerator TypeGenerator { get; }

            /// <summary>
            /// Returns the associated builder.
            /// </summary>
            public StringBuilder Builder { get; }

            /// <summary>
            /// Begins the declaration of a structure-like type.
            /// </summary>
            /// <param name="structureLikeType">The structure-like type.</param>
            private void BeginStruct(TypeNode structureLikeType)
            {
                Builder.Append("struct ");
                Builder.AppendLine(TypeGenerator[structureLikeType]);
                Builder.AppendLine("{");
            }

            /// <summary>
            /// Finishes the creation of a structure-like type.
            /// </summary>
            private void EndStruct()
            {
                Builder.AppendLine("};");
            }

            /// <summary cref="ITypeNodeVisitor.Visit(VoidType)"/>
            public void Visit(VoidType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(StringType)"/>
            public void Visit(StringType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(PrimitiveType)"/>
            public void Visit(PrimitiveType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(PointerType)"/>
            public void Visit(PointerType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(ViewType)"/>
            public void Visit(ViewType type)
            {
                BeginStruct(type);
                Builder.Append('\t');
                Builder.Append(CLInstructions.GetAddressSpacePrefix(type.AddressSpace));
                Builder.Append(' ');
                Builder.Append(TypeGenerator[type.ElementType]);
                Builder.Append(" *");
                Builder.Append(ViewPointerName);
                Builder.AppendLine(";");
                Builder.Append("\tint ");
                Builder.AppendLine(ViewLengthName);
                Builder.AppendLine(";");
                EndStruct();
            }

            /// <summary cref="ITypeNodeVisitor.Visit(ArrayType)"/>
            public void Visit(ArrayType type)
            {
                // Array types are currently not supported in OpenCL kernels
                throw new NotSupportedException(ErrorMessages.NotSupportedArrayElementType);
            }

            /// <summary cref="ITypeNodeVisitor.Visit(StructureType)"/>
            public void Visit(StructureType type)
            {
                BeginStruct(type);
                for (int i = 0, e = type.NumFields; i < e; ++i)
                {
                    Builder.Append('\t');
                    Builder.Append(TypeGenerator[type.Fields[i]]);
                    Builder.Append(' ');
                    Builder.AppendFormat(FieldNameFormat, i.ToString());
                    Builder.AppendLine(";");
                }
                EndStruct();
            }

            /// <summary cref="ITypeNodeVisitor.Visit(HandleType)"/>
            public void Visit(HandleType type) => throw new InvalidCodeGenerationException();
        }

        #endregion

        #region Static

        /// <summary>
        /// Maps basic value types to OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> BasicTypeMapping = ImmutableArray.Create(
            null,
            "bool",
            "char",
            "short",
            "int",
            "long",
            "float",
            "double");

        /// <summary>
        /// Maps arithmetic-basic value types to OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> ArtihmeticTypeMapping = ImmutableArray.Create(
            "bool",
            "char",
            "uchar",
            "short",
            "ushort",
            "int",
            "uint",
            "long",
            "ulong",
            "float",
            "double");

        /// <summary>
        /// Maps arithmetic-basic value types to atomic OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> AtomicTypeMapping = ImmutableArray.Create(
            null,
            null,
            null,
            null,
            null,
            "atomic_int",
            "atomic_uint",
            "atomic_long",
            "atomic_ulong",
            null,
            null);

        /// <summary>
        /// Resolves the given basic-value type to an OpenCL type name.
        /// </summary>
        /// <param name="basicValueType">The basic-value type to resolve.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public static string GetBasicValueType(BasicValueType basicValueType) =>
            BasicTypeMapping[(int)basicValueType];

        /// <summary>
        /// Resolves the given basic-value type to an OpenCL type name.
        /// </summary>
        /// <param name="basicValueType">The basic-value type to resolve.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public static string GetBasicValueType(ArithmeticBasicValueType basicValueType) =>
            ArtihmeticTypeMapping[(int)basicValueType];

        /// <summary>
        /// Resolves the given basic-value type to an atomic OpenCL type name.
        /// </summary>
        /// <param name="basicValueType">The basic-value type to resolve.</param>
        /// <returns>The resolved atomic OpenCL type name.</returns>
        public static string GetAtomicType(ArithmeticBasicValueType basicValueType) =>
            AtomicTypeMapping[(int)basicValueType];

        #endregion

        #region Instance

        private readonly Dictionary<TypeNode, string> mapping = new Dictionary<TypeNode, string>();

        /// <summary>
        /// Constructs a new type generator and defines all required types
        /// in OpenCL during construction.
        /// </summary>
        /// <param name="scopeProvider">All relevant method scopes.</param>
        /// <param name="typeContext">The associated type context.</param>
        /// <param name="target">The target builder to write to.</param>
        internal CLTypeGenerator(
            CachedScopeProvider scopeProvider,
            IRTypeContext typeContext,
            StringBuilder target)
        {
            Builder = target;
            TypeContext = typeContext;

            // Declare primitive types
            mapping[typeContext.VoidType] = "void";
            mapping[typeContext.StringType] = "char*";

            foreach (var basicValueType in IRTypeContext.BasicValueTypes)
            {
                var primitiveType = typeContext.GetPrimitiveType(basicValueType);
                mapping[primitiveType] = GetBasicValueType(basicValueType);
            }

            // Generate all type declarations
            foreach (var (method, scope) in scopeProvider)
            {
                // Check all parameter types
                foreach (var param in method.Parameters)
                {
                    if (!mapping.ContainsKey(param.Type))
                        DeclareType(param.Type);
                }

                // Check all node types
                foreach (Value value in scope.Values)
                {
                    if (!mapping.ContainsKey(value.Type))
                        DeclareType(value.Type);
                }
            }

            // Generate all pointer types
            foreach (var typeNode in mapping.Keys)
                DefinePointerType(typeNode);

            // Generate all structure types
            var typeVisitor = new TypeVisitor(this);
            foreach (var typeNode in mapping.Keys)
                typeNode.Accept(typeVisitor);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        private StringBuilder Builder { get; }

        /// <summary>
        /// Returns the associated OpenCL type name.
        /// </summary>
        /// <param name="typeNode">The internal IR type node.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public string this[TypeNode typeNode] => mapping[typeNode];

        /// <summary>
        /// Returns the associated OpenCL type name.
        /// </summary>
        /// <param name="type">The managed type to use.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public string this[Type type] => this[TypeContext.CreateType(type)];

        #endregion

        #region Methods

        /// <summary>
        /// Declares the given type in OpenCL.
        /// </summary>
        /// <param name="typeNode">The type to declare.</param>
        private void DeclareType(TypeNode typeNode)
        {
            if (mapping.ContainsKey(typeNode))
                return;

            var clName = string.Format(TypeNameFormat, typeNode.Id);
            if (typeNode is PointerType)
            {
                // We do not declare pointer types in this phase
            }
            else
            {
                // Adjust the structure name to include the 'struct' prefix
                // AMD drivers sometimes complain about missing struct prefixes
                clName = "struct " + clName;

                Builder.Append(clName);
                Builder.AppendLine(";");
            }

            // Register the type node and its associated name
            mapping.Add(typeNode, clName);
        }

        /// <summary>
        /// Defines a pointer type in OpenCL (if applicable).
        /// </summary>
        /// <param name="typeNode">The type to define.</param>
        private void DefinePointerType(TypeNode typeNode)
        {
            if (!(typeNode is PointerType pointerType))
                return;

            Builder.Append("typedef ");
            Builder.Append(CLInstructions.GetAddressSpacePrefix(pointerType.AddressSpace));
            Builder.Append(' ');
            Builder.Append(this[pointerType.ElementType]);
            Builder.Append(' ');
            Builder.Append(CLInstructions.DereferenceOperation);
            Builder.Append(this[pointerType]);
            Builder.AppendLine(";");
        }

        #endregion
    }
}
