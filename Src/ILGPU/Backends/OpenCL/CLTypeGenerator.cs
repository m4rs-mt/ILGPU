// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// An abstract type generator that can emit type declarations and definitions.
    /// </summary>
    public interface ICLTypeGenerator
    {
        /// <summary>
        /// Generate all forward type declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        void GenerateTypeDeclarations(StringBuilder builder);

        /// <summary>
        /// Generate all type definitions.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        void GenerateTypeDefinitions(StringBuilder builder);
    }

    /// <summary>
    /// Generates internal OpenCL type structures that are used inside kernels.
    /// </summary>
    public sealed class CLTypeGenerator : DisposeBase, ICLTypeGenerator
    {
        #region Constants

        /// <summary>
        /// The string format of a single structure-like type.
        /// </summary>
        private const string TypeNameFormat = "type_{0}";

        /// <summary>
        /// The string format of a single structure field.
        /// </summary>
        private const string FieldNameFormat = "field_{0}";

        #endregion

        #region Static

        /// <summary>
        /// Maps basic value types to OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> BasicTypeMapping =
            ImmutableArray.Create(
                null,
                "bool",
                "char",
                "short",
                "int",
                "long",
                "half",
                "float",
                "double");

        /// <summary>
        /// Maps arithmetic-basic value types to OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> ArtihmeticTypeMapping =
            ImmutableArray.Create(
                string.Empty,
                "bool",
                "char",
                "short",
                "int",
                "long",
                "half",
                "float",
                "double",
                "uchar",
                "ushort",
                "uint",
                "ulong");

        /// <summary>
        /// Maps arithmetic-basic value types to atomic OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> AtomicTypeMapping =
            ImmutableArray.Create(
                string.Empty,
                null,
                null,
                null,
                "atomic_int",
                "atomic_long",
                null,
                null,
                null,
                null,
                null,
                "atomic_uint",
                "atomic_ulong");

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

        /// <summary>
        /// Resolves a unique type name for the given node.
        /// </summary>
        /// <param name="typeNode">The type node.</param>
        /// <returns>The unique type name.</returns>
        public static string GetTypeName(TypeNode typeNode) =>
            string.Format(TypeNameFormat, typeNode.Id.ToString());

        /// <summary>
        /// Resolves a unique field name for the field index.
        /// </summary>
        /// <param name="fieldIndex">The field index.</param>
        /// <returns>The unique field name.</returns>
        public static string GetFieldName(int fieldIndex) =>
            string.Format(FieldNameFormat, fieldIndex.ToString());

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim readerWriterLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, string> mapping =
            new Dictionary<TypeNode, string>();

        /// <summary>
        /// Constructs a new type generator and defines all required types
        /// in OpenCL during construction.
        /// </summary>
        /// <param name="typeContext">The associated type context.</param>
        internal CLTypeGenerator(IRTypeContext typeContext)
        {
            TypeContext = typeContext;

            // Declare primitive types
            mapping[typeContext.VoidType] = "void";
            mapping[typeContext.StringType] = "char*";

            foreach (var basicValueType in IRTypeContext.BasicValueTypes)
            {
                if (basicValueType == BasicValueType.Float64
                    && TypeContext.Context.HasFlags(ContextFlags.Force32BitFloats))
                {
                    continue;
                }

                var primitiveType = typeContext.GetPrimitiveType(basicValueType);
                mapping[primitiveType] = GetBasicValueType(basicValueType);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the associated OpenCL type name.
        /// </summary>
        /// <param name="typeNode">The internal IR type node.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public string this[TypeNode typeNode]
        {
            get
            {
                readerWriterLock.EnterUpgradeableReadLock();
                try
                {
                    if (mapping.TryGetValue(typeNode, out string typeName))
                        return typeName;
                    readerWriterLock.EnterWriteLock();
                    try
                    {
                        return GetOrCreateType(typeNode);
                    }
                    finally
                    {
                        readerWriterLock.ExitWriteLock();
                    }
                }
                finally
                {
                    readerWriterLock.ExitUpgradeableReadLock();
                }
            }
        }

        /// <summary>
        /// Returns the associated OpenCL type name.
        /// </summary>
        /// <param name="type">The managed type to use.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public string this[Type type] => this[TypeContext.CreateType(type)];

        #endregion

        #region Methods

        /// <summary>
        /// Gets or creates the given type in OpenCL.
        /// </summary>
        /// <param name="typeNode">The type to declare.</param>
        /// <returns>The declared type name.</returns>
        private string GetOrCreateType(TypeNode typeNode)
        {
            Debug.Assert(!(typeNode is ViewType), "Invalid view type");
            if (mapping.TryGetValue(typeNode, out string clName))
                return clName;

            if (typeNode is PointerType pointerType)
            {
                // Make sure the element type is known
                GetOrCreateType(pointerType.ElementType);
                clName = GetTypeName(typeNode);
            }
            else if (typeNode is StructureType structureType)
            {
                // Make sure all field types are known
                foreach (var fieldType in structureType.Fields)
                    GetOrCreateType(fieldType);

                clName = CLInstructions.StructTypePrefix + " " + GetTypeName(typeNode);
            }
            else
            {
                // Must be a not supported view type
                throw new InvalidCodeGenerationException();
            }

            // Store generated type name
            mapping[typeNode] = clName;
            return clName;
        }

        /// <summary>
        /// Generate all forward type declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDeclarations(StringBuilder builder)
        {
            foreach (var entry in mapping)
            {
                switch (entry.Key)
                {
                    case PointerType pointerType:
                        builder.Append(
                            CLInstructions.TypeDefStatement);
                        builder.Append(' ');
                        builder.Append(
                            CLInstructions.GetAddressSpacePrefix(
                                pointerType.AddressSpace));
                        builder.Append(' ');
                        builder.Append(mapping[pointerType.ElementType]);
                        builder.Append(CLInstructions.DereferenceOperation);
                        builder.Append(' ');
                        builder.Append(entry.Value);
                        builder.AppendLine(";");
                        break;
                    case StructureType _:
                        builder.Append(entry.Value);
                        builder.AppendLine(";");
                        break;
                }
            }
            builder.AppendLine();
        }

        /// <summary>
        /// Generate all type definitions.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDefinitions(StringBuilder builder)
        {
            foreach (var entry in mapping)
            {
                if (!(entry.Key is StructureType structureType))
                    continue;

                GenerateStructureDefinition(
                    structureType,
                    entry.Value,
                    builder);
            }

            builder.AppendLine();
        }

        /// <summary>
        /// Generates a new structure definition in OpenCL format.
        /// </summary>
        /// <param name="structureType">The structure type.</param>
        /// <param name="typeName">The type name.</param>
        /// <param name="builder">The target builder to write to.</param>
        public void GenerateStructureDefinition(
            StructureType structureType,
            string typeName,
            StringBuilder builder)
        {
            int paddingCounter = 0;

            builder.AppendLine(typeName);
            builder.AppendLine("{");
            foreach (var (access, _, padding) in structureType.Offsets)
            {
                // Append padding information
                if (padding > 0)
                {
                    builder.Append('\t');
                    builder.Append(GetBasicValueType(ArithmeticBasicValueType.Int8));
                    builder.Append(' ');
                    builder.Append("__padding");
                    builder.Append(++paddingCounter);
                    builder.Append('[');
                    builder.Append(padding);
                    builder.AppendLine("];");
                }

                builder.Append('\t');
                builder.Append(mapping[structureType[access]]);
                builder.Append(' ');
                builder.Append(GetFieldName(access.Index));
                builder.AppendLine(";");

            }
            builder.AppendLine("};");
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            readerWriterLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
