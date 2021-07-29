// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IRTypeContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a context that manages IR types.
    /// </summary>
    public sealed class IRTypeContext : TypeInformationManager, IIRTypeContext
    {
        #region Static

        /// <summary>
        /// Contains all basic value types.
        /// </summary>
        public static readonly ImmutableArray<BasicValueType> BasicValueTypes =
            ImmutableArray.Create(
                BasicValueType.Int1,
                BasicValueType.Int8,
                BasicValueType.Int16,
                BasicValueType.Int32,
                BasicValueType.Int64,
                BasicValueType.Float16,
                BasicValueType.Float32,
                BasicValueType.Float64);

        /// <summary>
        /// Returns true if the given basic value type can be used in combination with
        /// a view type.
        /// </summary>
        /// <param name="basicValueType"></param>
        /// <returns>
        /// True if the given value type is a compatible view index type.
        /// </returns>
        internal static bool IsViewIndexType(BasicValueType basicValueType) =>
            basicValueType == BasicValueType.Int32 |
            basicValueType == BasicValueType.Int64;

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim typeLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);

        private readonly Dictionary<TypeNode, TypeNode> unifiedTypes =
            new Dictionary<TypeNode, TypeNode>();

        private readonly Dictionary<(Type, MemoryAddressSpace), TypeNode> typeMapping =
            new Dictionary<(Type, MemoryAddressSpace), TypeNode>();

        private readonly PrimitiveType[] basicValueTypes;

        /// <summary>
        /// Constructs a new IR type context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        public IRTypeContext(Context context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            TargetPlatform = context.TargetPlatform;
            RuntimeSystem = context.RuntimeSystem;
            MathMode = context.Properties.MathMode;

            VoidType = new VoidType(this);
            StringType = new StringType(this);
            HandleType = new HandleType(this);

            var rootTypeBuilder = CreateStructureType(0);
            RootType = new StructureType(this, rootTypeBuilder);

            basicValueTypes = new PrimitiveType[BasicValueTypes.Length + 1];

            foreach (var type in BasicValueTypes)
                basicValueTypes[(int)type] = new PrimitiveType(this, type);
            if (MathMode == MathMode.Fast32BitOnly)
            {
                basicValueTypes[
                    (int)BasicValueType.Float64] = basicValueTypes[
                    (int)BasicValueType.Float32];
            }

            Padding8Type = new PaddingType(this, GetPrimitiveType(BasicValueType.Int8));
            Padding16Type = new PaddingType(this, GetPrimitiveType(BasicValueType.Int16));
            Padding32Type = new PaddingType(this, GetPrimitiveType(BasicValueType.Int32));
            Padding64Type = new PaddingType(this, GetPrimitiveType(BasicValueType.Int64));

            PopulateTypeMapping();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated target platform for all pointer-based types.
        /// </summary>
        public TargetPlatform TargetPlatform { get; }

        /// <summary>
        /// Returns the parent runtime system.
        /// </summary>
        public RuntimeSystem RuntimeSystem { get; }

        /// <summary>
        /// Returns the current math mode.
        /// </summary>
        public MathMode MathMode { get; }

        /// <summary>
        /// Returns the void type.
        /// </summary>
        public VoidType VoidType { get; }

        /// <summary>
        /// Returns the memory type.
        /// </summary>
        public StringType StringType { get; }

        /// <summary>
        /// Returns the managed handle type.
        /// </summary>
        public HandleType HandleType { get; }

        /// <summary>
        /// Returns the root structure type.
        /// </summary>
        public StructureType RootType { get; }

        /// <summary>
        /// Returns a custom padding type that is used to pad structure values (8-bits).
        /// </summary>
        public PaddingType Padding8Type { get; }

        /// <summary>
        /// Returns a custom padding type that is used to pad structure values (16-bits).
        /// </summary>
        public PaddingType Padding16Type { get; }

        /// <summary>
        /// Returns a custom padding type that is used to pad structure values (32-bits).
        /// </summary>
        public PaddingType Padding32Type { get; }

        /// <summary>
        /// Returns a custom padding type that is used to pad structure values (64-bits).
        /// </summary>
        public PaddingType Padding64Type { get; }

        /// <summary>
        /// Returns a dense 1D stride type (<see cref="Stride1D.Dense"/>).
        /// </summary>
        public TypeNode Dense1DStrideType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the primitive type that corresponds to the given
        /// <see cref="BasicValueType"/>.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
            basicValueTypes[(int)basicValueType];


        /// <summary>
        /// Resolves the padding type that corresponds to the given
        /// <see cref="BasicValueType"/>.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The padding type.</returns>
        public PaddingType GetPaddingType(BasicValueType basicValueType)
        {
            switch (basicValueType)
            {
                case BasicValueType.Int8:
                    return Padding8Type;

                case BasicValueType.Int16:
                case BasicValueType.Float16:
                    return Padding16Type;

                case BasicValueType.Int32:
                case BasicValueType.Float32:
                    return Padding32Type;

                case BasicValueType.Int64:
                case BasicValueType.Float64:
                    return Padding64Type;

                default:
                    throw new ArgumentOutOfRangeException(nameof(basicValueType));
            }
        }

        /// <summary>
        /// Creates a pointer type.
        /// </summary>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created pointer type.</returns>
        public PointerType CreatePointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            UnifyType(new PointerType(
                this,
                elementType,
                addressSpace));

        /// <summary>
        /// Creates a view type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        public ViewType CreateViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            UnifyType(new ViewType(
                this,
                elementType,
                addressSpace));

        /// <summary>
        /// Creates a new structure type builder with the given capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <returns>The created structure builder.</returns>
        public StructureType.Builder CreateStructureType(int capacity) =>
            new StructureType.Builder(this, capacity, 0);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <returns>The created type.</returns>
        [SuppressMessage(
            "Style",
            "IDE0046:Convert to conditional expression",
            Justification = "Avoid nested if conditionals")]
        internal TypeNode FinishStructureType(in StructureType.Builder builder)
        {
            if (builder.Count < 1)
                return this.CreateEmptyStructureType();

            return builder.Count < 2
                ? builder[0]
                : UnifyType(new StructureType(
                    this,
                    builder));
        }

        /// <summary>
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimensions">The number of array dimensions.</param>
        /// <returns>The created array type.</returns>
        public ArrayType CreateArrayType(TypeNode elementType, int dimensions) =>
            dimensions < 1
                ? throw new NotSupportedException(
                    string.Format(
                        ErrorMessages.NotSupportedArrayDimension,
                        dimensions.ToString()))
                : UnifyType(new ArrayType(
                    this,
                    elementType,
                    dimensions));

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type) =>
            CreateType(type, MemoryAddressSpace.Generic);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="addressSpace">The address space for pointer types.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type, MemoryAddressSpace addressSpace)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // Avoid querying the cache for primitive types
            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
            {
                return GetPrimitiveType(basicValueType);
            }
            else
            {
                // Synchronize all accesses below using a read/write scope
                using var readWriteScope = typeLock.EnterUpgradeableReadScope();

                // Explicitly check the local cache for a potential type
                if (typeMapping.TryGetValue((type, addressSpace), out var result))
                    return result;

                // Synchronize all accesses below using a write scope
                using var writeScope = typeLock.EnterWriteScope();

                return CreateType_Sync(type, addressSpace);
            }
        }

        /// <summary>
        /// Maps the given type and address space to the type node provided.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="type">The managed type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <param name="typeNode">The type node to map to.</param>
        /// <returns>The given type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T Map<T>(Type type, MemoryAddressSpace addressSpace, T typeNode)
            where T : TypeNode
        {
            typeMapping[(type, addressSpace)] = typeNode;
            return typeNode;
        }

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="addressSpace">The address space for pointer types.</param>
        /// <returns>The IR type.</returns>
        private TypeNode CreateType_Sync(Type type, MemoryAddressSpace addressSpace)
        {
            Debug.Assert(type != null, "Invalid type");

            // Check for a basic value type
            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
                return GetPrimitiveType(basicValueType);

            // Check the current cache
            if (typeMapping.TryGetValue((type, addressSpace), out var result))
                return result;

            if (type.IsEnum)
            {
                // Do not store enum types
                return CreateType_Sync(type.GetEnumUnderlyingType(), addressSpace);
            }
            else if (type.IsArray)
            {
                var arrayElementType = CreateType_Sync(
                    type.GetElementType(),
                    addressSpace);
                var dimension = type.GetArrayRank();
                return Map(
                    type,
                    addressSpace,
                    CreateArrayType(arrayElementType, dimension));
            }
            else if (type.IsArrayViewType(out Type elementType))
            {
                return Map(
                    type,
                    addressSpace,
                    CreateViewType(
                        CreateType_Sync(elementType, addressSpace),
                        addressSpace));
            }
            else if (type.IsVoidPtr())
            {
                return Map(
                    type,
                    addressSpace,
                    CreatePointerType(VoidType, addressSpace));
            }
            else if (type.IsByRef || type.IsPointer)
            {
                return Map(
                    type,
                    addressSpace,
                    CreatePointerType(
                        CreateType_Sync(type.GetElementType(), addressSpace),
                        addressSpace));
            }
            else if (type.IsClass)
            {
                throw new NotSupportedException(
                    string.Format(
                        ErrorMessages.NotSupportedClassType,
                        type));
            }
            else
            {
                // Must be a structure type
                if (!type.IsValueType)
                {
                    throw new NotSupportedException(
                        string.Format(
                            ErrorMessages.NotSupportedType,
                            type));
                }

                var typeInfo = GetTypeInfo(type);
                var builder = new StructureType.Builder(
                    this,
                    typeInfo.NumFlattendedFields,
                    typeInfo.Size);
                foreach (var field in typeInfo.Fields)
                    builder.Add(CreateType_Sync(field.FieldType, addressSpace));

                return Map(
                    type,
                    addressSpace,
                    UnifyType(builder.Seal()));
            }
        }

        /// <summary>
        /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
        /// </summary>
        /// <param name="addressSpaceType">The source type.</param>
        /// <param name="addressSpace">The new address space.</param>
        /// <returns>The created specialized <see cref="AddressSpaceType"/>.</returns>
        public AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(addressSpaceType != null, "Invalid address space type");

            if (addressSpaceType is PointerType pointerType)
            {
                return CreatePointerType(
                    pointerType.ElementType,
                    addressSpace);
            }
            else
            {
                var viewType = addressSpaceType as ViewType;
                return CreateViewType(
                    viewType.ElementType,
                    addressSpace);
            }
        }

        /// <summary>
        /// Tries to specialize a view or a pointer address space.
        /// </summary>
        /// <param name="type">The pointer or view type.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <param name="specializedType">The specialized type.</param>
        /// <returns>True, if the type could be specialized.</returns>
        public bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            out TypeNode specializedType)
        {
            Debug.Assert(type != null, "Invalid type");

            specializedType = type is AddressSpaceType addressSpaceType
                ? SpecializeAddressSpaceType(addressSpaceType, addressSpace)
                : null;
            return specializedType != null;
        }

        /// <summary>
        /// Creates a type.
        /// </summary>
        /// <typeparam name="T">The type of the  type.</typeparam>
        /// <param name="type">The type to create.</param>
        /// <returns>The created type.</returns>
        private T UnifyType<T>(T type)
            where T : TypeNode
        {
            // Synchronize all accesses below using a read/write scope
            using var readWriteScope = typeLock.EnterUpgradeableReadScope();

            if (unifiedTypes.TryGetValue(type, out TypeNode result))
                return result as T;

            // Synchronize all accesses below using a write scope
            using var writeScope = typeLock.EnterWriteScope();

            unifiedTypes.Add(type, type);
            return type;
        }

        /// <summary>
        /// Populates the internal type mapping.
        /// </summary>
        private void PopulateTypeMapping()
        {
            // Populate type mapping
            foreach (var space in AddressSpaces.Spaces)
            {
                typeMapping.Add((typeof(void), space), VoidType);
                typeMapping.Add((typeof(string), space), StringType);

                typeMapping.Add((typeof(Array), space), RootType);

                typeMapping.Add((typeof(RuntimeFieldHandle), space), HandleType);
                typeMapping.Add((typeof(RuntimeMethodHandle), space), HandleType);
                typeMapping.Add((typeof(RuntimeTypeHandle), space), HandleType);
            }

            // Populate unified types
            unifiedTypes.Add(VoidType, VoidType);
            unifiedTypes.Add(StringType, StringType);
            for (int i = 1, e = basicValueTypes.Length; i < e; ++i)
            {
                var basicType = basicValueTypes[i];
                unifiedTypes[basicType] = basicType;
            }
        }

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public override void ClearCache(ClearCacheMode mode)
        {
            base.ClearCache(mode);

            // Synchronize all accesses below using a write scope
            using var writeScope = typeLock.EnterWriteScope();

            typeMapping.Clear();
            unifiedTypes.Clear();
            PopulateTypeMapping();
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                typeLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
