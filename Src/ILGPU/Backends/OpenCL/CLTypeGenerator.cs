// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// The type generation mode.
    /// </summary>
    public enum CLTypeGeneratorMode
    {
        /// <summary>
        /// An internal type that can be used inside a kernel only.
        /// </summary>
        Internal = 0,

        /// <summary>
        /// An external type that is used for kernel-host interop.
        /// </summary>
        Kernel = 1
    }

    /// <summary>
    /// Generates OpenCL type structures.
    /// </summary>
    public sealed class CLTypeGenerator : DisposeBase
    {
        #region Constants

        /// <summary>
        /// The string format of a single structure-like type.
        /// </summary>
        private const string DefaultNameFormat = "internal_type_{0}";

        /// <summary>
        /// The string format of a single structure-like type.
        /// </summary>
        private const string KernelTypeNameFormat = "kernel_type_{0}";

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
            string.Format(FieldNameFormat, ViewPointerFieldIndex.ToString());

        /// <summary>
        /// The name of the length field inside a view.
        /// </summary>
        public static readonly string ViewLengthName =
            string.Format(FieldNameFormat, ViewLengthFieldIndex.ToString());

        /// <summary>
        /// The name of the index field inside a view.
        /// </summary>
        public static readonly string ViewIndexName = ViewPointerName;

        /// <summary>
        /// Gets the type name for the specified mode and index.
        /// </summary>
        /// <param name="mode">The generator mode.</param>
        /// <param name="index">The type index.</param>
        /// <returns>The created type name.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetStructTypeName(CLTypeGeneratorMode mode, int index)
        {
            string indexString = index.ToString();
            string clName;
            if (mode == CLTypeGeneratorMode.Internal)
                clName = string.Format(DefaultNameFormat, indexString);
            else
                clName = string.Format(KernelTypeNameFormat, indexString);

            // Adjust the structure name to include the 'struct' prefix
            // AMD drivers sometimes complain about missing struct prefixes
            return "struct " + clName;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// An internal type lookup.
        /// </summary>
        interface ITypeLookup
        {
            /// <summary>
            /// Tries to lookup the given type.
            /// </summary>
            /// <param name="type">The type to lookup.</param>
            /// <param name="typeName">The resolved type name (if any).</param>
            /// <returns>True, if the given type could be resolved.</returns>
            bool TryLookup(TypeNode type, out string typeName);

            /// <summary>
            /// Adds the given type node and returns the declared type name.
            /// </summary>
            /// <param name="type">The type to add.</param>
            /// <returns>The declared type name.</returns>
            string Add(TypeNode type);

            /// <summary>
            /// Lookups the given type for final code generation.
            /// </summary>
            /// <param name="type">The type to lookup.</param>
            /// <returns>The type name for final code generation.</returns>
            string Lookup(TypeNode type);

            void GenerateView<TTypeGenerator>(in TTypeGenerator typeGenerator, ViewType viewType)
                where TTypeGenerator : struct, ITypeGenerator;
        }

        /// <summary>
        /// Represents a lookup for internal types.
        /// </summary>
        private readonly struct InternalTypeLookup : ITypeLookup
        {
            /// <summary>
            /// Creates a new internal type lookup.
            /// </summary>
            /// <param name="parent">The parent type generator.</param>
            public InternalTypeLookup(CLTypeGenerator parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLTypeGenerator Parent { get; }

            /// <summary cref="ITypeLookup.TryLookup(TypeNode, out string)"/>
            public bool TryLookup(TypeNode type, out string typeName) =>
                Parent.mapping.TryGetValue(type, out typeName);

            /// <summary cref="ITypeLookup.Add(TypeNode)"/>
            public string Add(TypeNode type)
            {
                var clName = GetStructTypeName(
                    CLTypeGeneratorMode.Internal,
                    Parent.mapping.Count);
                Parent.mapping.Add(type, clName);
                return clName;
            }

            /// <summary cref="ITypeLookup.Lookup(TypeNode)"/>
            public string Lookup(TypeNode type)
            {
                if (type is PointerType pointerType)
                    return Lookup(pointerType.ElementType) +
                        CLInstructions.DereferenceOperation;
                return Parent.mapping[type];
            }

            /// <summary cref="ITypeLookup.GenerateView{TTypeGenerator}(in TTypeGenerator, ViewType)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GenerateView<TTypeGenerator>(in TTypeGenerator typeGenerator, ViewType viewType)
                where TTypeGenerator : struct, ITypeGenerator
            {
                var builder = typeGenerator.Builder;
                typeGenerator.BeginStruct(viewType);
                builder.Append('\t');
                builder.Append(Lookup(viewType.ElementType));
                builder.Append(CLInstructions.DereferenceOperation);
                builder.Append(' ');
                builder.Append(ViewPointerName);
                builder.AppendLine(";");
                builder.Append("\tint ");
                builder.Append(ViewLengthName);
                builder.AppendLine(";");
                typeGenerator.EndStruct();
            }
        }

        /// <summary>
        /// Represents a lookup for kernel types.
        /// </summary>
        private readonly struct KernelTypeLookup : ITypeLookup
        {
            private readonly RequiresMappingTypeVisitor mappingVisitor;
            private readonly InternalTypeLookup internalTypeLookup;

            /// <summary>
            /// Creates a new kernel type lookup.
            /// </summary>
            /// <param name="parent">The parent type generator.</param>
            public KernelTypeLookup(CLTypeGenerator parent)
            {
                Parent = parent;
                internalTypeLookup = new InternalTypeLookup(parent);
                mappingVisitor = new RequiresMappingTypeVisitor();
            }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLTypeGenerator Parent { get; }

            /// <summary cref="ITypeLookup.TryLookup(TypeNode, out string)"/>
            public bool TryLookup(TypeNode type, out string typeName) =>
                Parent.kernelArgumentMapping.TryGetValue(type, out typeName) ||
                internalTypeLookup.TryLookup(type, out typeName);

            /// <summary cref="ITypeLookup.Add(TypeNode)"/>
            public string Add(TypeNode type)
            {
                // Check whether we need to create a specific kernel argument type
                mappingVisitor.Reset();
                type.Accept(mappingVisitor);

                // Do we require a specific type?
                var clName = internalTypeLookup.Add(type);
                if (mappingVisitor.RequiresMapping)
                {
                    clName = GetStructTypeName(
                        CLTypeGeneratorMode.Kernel,
                        Parent.kernelArgumentMapping.Count);
                    Parent.kernelArgumentMapping.Add(type, clName);
                }
                return clName;
            }

            /// <summary cref="ITypeLookup.Lookup(TypeNode)"/>
            public string Lookup(TypeNode type)
            {
                if (Parent.kernelArgumentMapping.TryGetValue(type, out string typeName))
                    return typeName;
                return internalTypeLookup.Lookup(type);
            }

            /// <summary cref="ITypeLookup.GenerateView{TTypeGenerator}(in TTypeGenerator, ViewType)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GenerateView<TTypeGenerator>(in TTypeGenerator typeGenerator, ViewType viewType)
                where TTypeGenerator : struct, ITypeGenerator
            {
                var builder = typeGenerator.Builder;
                typeGenerator.BeginStruct(viewType);
                builder.Append("\tint ");
                builder.Append(ViewIndexName);
                builder.AppendLine(";");
                builder.Append("\tint ");
                builder.Append(ViewLengthName);
                builder.AppendLine(";");
                typeGenerator.EndStruct();
            }
        }

        /// <summary>
        /// An internal type visitor to determine whether a type requires
        /// a custom kernel argument mapper.
        /// </summary>
        private sealed class RequiresMappingTypeVisitor : ITypeNodeVisitor
        {
            /// <summary>
            /// Returns true if the analyzed type requires custom mapping.
            /// </summary>
            public bool RequiresMapping { get; private set; }

            /// <summary>
            /// Resets the internal state of this visitor.
            /// </summary>
            public void Reset() { RequiresMapping = false; }

            /// <summary cref="ITypeNodeVisitor.Visit(VoidType)"/>
            public void Visit(VoidType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(StringType)"/>
            public void Visit(StringType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(PrimitiveType)"/>
            public void Visit(PrimitiveType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(PointerType)"/>
            public void Visit(PointerType type) => RequiresMapping = true;

            /// <summary cref="ITypeNodeVisitor.Visit(ViewType)"/>
            public void Visit(ViewType type) => RequiresMapping = true;

            /// <summary cref="ITypeNodeVisitor.Visit(ArrayType)"/>
            public void Visit(ArrayType type) { }

            /// <summary cref="ITypeNodeVisitor.Visit(StructureType)"/>
            public void Visit(StructureType type)
            {
                foreach (var field in type.Fields)
                    field.Accept(this);
            }

            /// <summary cref="ITypeNodeVisitor.Visit(HandleType)"/>
            public void Visit(HandleType type) => throw new InvalidCodeGenerationException();
        }

        private interface ITypeGenerator
        {
            /// <summary>
            /// Returns the associated builder.
            /// </summary>
            StringBuilder Builder { get; }

            /// <summary>
            /// Begins the declaration of a structure-like type.
            /// </summary>
            /// <param name="structureLikeType">The structure-like type.</param>
            void BeginStruct(TypeNode structureLikeType);

            /// <summary>
            /// Finishes the creation of a structure-like type.
            /// </summary>
            void EndStruct();
        }

        /// <summary>
        /// An internal type visitor to generate type definitions.
        /// </summary>
        private readonly struct TypeGenerator<TTypeLookup> : ITypeGenerator
            where TTypeLookup : struct, ITypeLookup
        {
            /// <summary>
            /// Constructs a new type visitor.
            /// </summary>
            /// <param name="typeLookup">The lookup to use.</param>
            /// <param name="builder">The builder to use.</param>
            public TypeGenerator(TTypeLookup typeLookup, StringBuilder builder)
            {
                TypeLookup = typeLookup;
                Builder = builder;
            }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public TTypeLookup TypeLookup { get; }

            /// <summary>
            /// Returns the associated builder.
            /// </summary>
            public StringBuilder Builder { get; }

            /// <summary>
            /// Begins the declaration of a structure-like type.
            /// </summary>
            /// <param name="structureLikeType">The structure-like type.</param>
            public void BeginStruct(TypeNode structureLikeType)
            {
                var typeName = TypeLookup.Lookup(structureLikeType);
                Builder.AppendLine(typeName);
                Builder.AppendLine("{");
            }

            /// <summary>
            /// Finishes the creation of a structure-like type.
            /// </summary>
            public void EndStruct()
            {
                Builder.AppendLine("};");
            }

            /// <summary>
            /// Generates OpenCL code for the given type.
            /// </summary>
            /// <param name="typeNode">The type to generate code for.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GenerateType(TypeNode typeNode)
            {
                switch (typeNode)
                {
                    case ViewType viewType:
                        GenerateView(viewType);
                        break;
                    case StructureType structureType:
                        GenerateStructure(structureType);
                        break;
                }
            }

            /// <summary>
            /// Generates OpenCL code for the given view.
            /// </summary>
            /// <param name="viewType">The type to generate code for.</param>
            public void GenerateView(ViewType viewType) =>
                TypeLookup.GenerateView(this, viewType);

            /// <summary>
            /// Generates OpenCL code for the given structure.
            /// </summary>
            /// <param name="structureType">The type to generate code for.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GenerateStructure(StructureType structureType)
            {
                BeginStruct(structureType);
                for (int i = 0, e = structureType.NumFields; i < e; ++i)
                {
                    Builder.Append('\t');
                    Builder.Append(TypeLookup.Lookup(structureType.Fields[i]));
                    Builder.Append(' ');
                    Builder.AppendFormat(FieldNameFormat, i.ToString());
                    Builder.AppendLine(";");
                }
                EndStruct();
            }
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
            string.Empty,
            "bool",
            "char",
            "short",
            "int",
            "long",
            "float",
            "double",
            "uchar",
            "ushort",
            "uint",
            "ulong");

        /// <summary>
        /// Maps arithmetic-basic value types to atomic OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> AtomicTypeMapping = ImmutableArray.Create(
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

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim readerWriterLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, string> kernelArgumentMapping =
            new Dictionary<TypeNode, string>();
        private readonly Dictionary<TypeNode, string> mapping =
            new Dictionary<TypeNode, string>();

        /// <summary>
        /// Constructs a new type generator and defines all required types
        /// in OpenCL during construction.
        /// </summary>
        /// <param name="typeContext">The associated type context.</param>
        /// <param name="targetPlatform">The target platform to use.</param>
        internal CLTypeGenerator(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform)
        {
            TypeContext = typeContext;

            // Declare primitive types
            mapping[typeContext.VoidType] = "void";
            mapping[typeContext.StringType] = "char*";

            string intPtrType = targetPlatform == TargetPlatform.X64 ? "long" : "int";
            mapping[typeContext.CreateType(typeof(IntPtr))] = intPtrType;
            mapping[typeContext.CreateType(typeof(UIntPtr))] = intPtrType;

            foreach (var basicValueType in IRTypeContext.BasicValueTypes)
            {
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
                        return GetOrCreateType(new InternalTypeLookup(this), typeNode);
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
        /// Gets a kernel argument type name.
        /// </summary>
        /// <param name="typeNode">The type to declare.</param>
        /// <returns>The declared kernel argument type.</returns>
        public string GetKernelArgumentType(TypeNode typeNode)
        {
            // Note that we do not require synchronization in this case
            if (kernelArgumentMapping.TryGetValue(typeNode, out string typeName) ||
                mapping.TryGetValue(typeNode, out typeName))
                return typeName;

            // Check whether we need a separate type
            var lookup = new KernelTypeLookup(this);
            return GetOrCreateType(lookup, typeNode);
        }

        /// <summary>
        /// Gets or creates the given type in OpenCL.
        /// </summary>
        /// <param name="lookup">The current lookup to use.</param>
        /// <param name="typeNode">The type to declare.</param>
        /// <returns>The declared type name.</returns>
        private string GetOrCreateType<TLookup>(TLookup lookup, TypeNode typeNode)
            where TLookup : struct, ITypeLookup
        {
            if (lookup.TryLookup(typeNode, out string clName))
                return clName;

            if (typeNode is PointerType pointerType)
            {
                // We do not store pointer types internally
                return GetOrCreateType(lookup, pointerType.ElementType) +
                    CLInstructions.DereferenceOperation;
            }
            else
            {
                var result = lookup.Add(typeNode);
                if (typeNode is StructureType structType)
                {
                    foreach (var fieldType in structType.Fields)
                        GetOrCreateType(lookup, fieldType);
                }
                return result;
            }
        }

        /// <summary>
        /// Returns true if the given type requires a custom argument mapping.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <param name="structureType">The resolved structure type (if any).</param>
        /// <returns>True, if the given type requires a custom argument mapping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RequiresKernelArgumentMapping(
            TypeNode type,
            out StructureType structureType)
        {
            structureType = type as StructureType;
            return structureType != null && kernelArgumentMapping.ContainsKey(type);
        }

        /// <summary>
        /// Generate all forward type declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDeclarations(StringBuilder builder)
        {
            // Declare all kernel types
            foreach (var kernelType in kernelArgumentMapping.Values)
            {
                builder.Append(kernelType);
                builder.AppendLine(";");
            }

            // Declare internal types
            foreach (var entry in mapping)
            {
                switch (entry.Key)
                {
                    case ViewType _:
                    case StructureType _:
                        builder.Append(entry.Value);
                        builder.AppendLine(";");
                        break;
                }
            }
            builder.AppendLine();
        }

        /// <summary>
        /// Computes a serialized list of all type nodes according to their dependencies.
        /// </summary>
        /// <typeparam name="TCollection">The collection type.</typeparam>
        /// <param name="types">The collection of types.</param>
        /// <returns>The sorted list of type nodes.</returns>
        private static List<TypeNode> GetSerializedTypeList<TCollection>(TCollection types)
            where TCollection : IReadOnlyCollection<TypeNode>
        {
            var result = new List<TypeNode>(types.Count);
            result.AddRange(types);
            result.Sort((left, right) =>
            {
                bool isLeftStructure = left is StructureType;
                bool isRightStructure = right is StructureType;
                if (isLeftStructure & !isRightStructure)
                    return 1;
                if (!isLeftStructure & isRightStructure)
                    return -1;
                if (!isLeftStructure & !isRightStructure)
                    return 0;

                var leftStructure = left as StructureType;
                var rightStructure = right as StructureType;
                if (_IsStructContainsStructRecursively(leftStructure, rightStructure))
                    return 1;
                if (_IsStructContainsStructRecursively(rightStructure, leftStructure))
                    return -1;
                return 0;
            });
            return result;
        }

        private static bool _IsStructContainsStructRecursively(StructureType parentStructType, StructureType targetStructType, int level = 0)
        {
            if (level > 1000)
            {
                throw new NotSupportedException(ErrorMessages.TooManyNestedStructure);
            }

            if (parentStructType.Fields.Contains(targetStructType))
            {
                return true;
            }

            foreach(var field in parentStructType.Fields)
            {
                if (field is StructureType)
                {
                    if (_IsStructContainsStructRecursively((StructureType)field, targetStructType, level++))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Generate all type definitions.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDefinitions(StringBuilder builder)
        {
            // Declare internal types
            var internalTypeGenerator = new TypeGenerator<InternalTypeLookup>(
                new InternalTypeLookup(this),
                builder);
            foreach (var internalType in GetSerializedTypeList(mapping.Keys))
                internalTypeGenerator.GenerateType(internalType);

            // Declare all kernel types
            var kernelTypeGenerator = new TypeGenerator<KernelTypeLookup>(
                new KernelTypeLookup(this),
                builder);
            foreach (var kernelType in GetSerializedTypeList(kernelArgumentMapping.Keys))
                kernelTypeGenerator.GenerateType(kernelType);
            builder.AppendLine();
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
