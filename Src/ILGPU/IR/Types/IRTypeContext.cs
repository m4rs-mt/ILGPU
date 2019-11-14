// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IRTypeContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
                BasicValueType.Float32,
                BasicValueType.Float64);

        /// <summary>
        /// Represents the index type of a view.
        /// </summary>
        internal const BasicValueType ViewIndexType = BasicValueType.Int32;

        /// <summary>
        /// Represents a generic array view type.
        /// </summary>
        private static readonly Type GenericArrayViewType = typeof(ArrayView<,>);

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim typeLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, TypeNode> unifiedTypes =
            new Dictionary<TypeNode, TypeNode>();
        private readonly Dictionary<Type, TypeNode> typeMapping =
            new Dictionary<Type, TypeNode>();
        private readonly StructureType[] indexTypes;
        private readonly PrimitiveType[] basicValueTypes;

        /// <summary>
        /// Constructs a new IR type context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        public IRTypeContext(Context context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));

            VoidType = CreateType(new VoidType());
            StringType = CreateType(new StringType());
            HandleType = CreateType(new HandleType());

            basicValueTypes = new PrimitiveType[BasicValueTypes.Length + 1];
            foreach (var type in BasicValueTypes)
            {
                basicValueTypes[(int)type] = CreateType(
                    new PrimitiveType(type));
            }

            if (context.HasFlags(ContextFlags.Force32BitFloats))
            {
                basicValueTypes[
                    (int)BasicValueType.Float64] = basicValueTypes[
                        (int)BasicValueType.Float32];
            }

            // Populate type mapping
            typeMapping.Add(typeof(void), VoidType);
            typeMapping.Add(typeof(string), StringType);
            typeMapping.Add(typeof(Array), StructureType.Root);

            typeMapping.Add(typeof(RuntimeFieldHandle), HandleType);
            typeMapping.Add(typeof(RuntimeMethodHandle), HandleType);
            typeMapping.Add(typeof(RuntimeTypeHandle), HandleType);

            // Setup index types
            indexTypes = new StructureType[]
            {
                CreateType(typeof(Index)) as StructureType,
                CreateType(typeof(Index2)) as StructureType,
                CreateType(typeof(Index3)) as StructureType,
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

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
        /// Returns the main index type.
        /// </summary>
        public StructureType IndexType => indexTypes[0];

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the primitive type that corresponds to the given <see cref="BasicValueType"/>.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
            basicValueTypes[(int)basicValueType];

        /// <summary>
        /// Creates an intrinsic index type.
        /// </summary>
        /// <param name="dimension">The dimension of the index type.</param>
        /// <returns>The created index type.</returns>
        public StructureType GetIndexType(int dimension)
        {
            Debug.Assert(
                dimension >= 0 && dimension < indexTypes.Length,
                "Invalid index dimension");
            return indexTypes[dimension];
        }

        /// <summary>
        /// Creates a pointer type.
        /// </summary>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created pointer type.</returns>
        public PointerType CreatePointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(elementType != null, "Invalid element type");
            return CreateType(new PointerType(
                elementType,
                addressSpace));
        }

        /// <summary>
        /// Creates a view type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        public ViewType CreateViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(elementType != null, "Invalid element type");
            return CreateType(new ViewType(
                elementType,
                addressSpace));
        }

        /// <summary>
        /// Creates a new object type.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <param name="fieldTypes">The object field types.</param>
        /// <returns>The created object type.</returns>
        public StructureType CreateStructureType(
            StructureType baseType,
            ImmutableArray<TypeNode> fieldTypes) =>
            CreateStructureType(
                baseType,
                fieldTypes,
                ImmutableArray<string>.Empty,
                null);

        /// <summary>
        /// Creates a new object type.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <param name="fieldTypes">The object field types.</param>
        /// <param name="fieldNames">The object field names.</param>
        /// <param name="sourceType">The source object type.</param>
        /// <returns>The created object type.</returns>
        public StructureType CreateStructureType(
            StructureType baseType,
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type sourceType)
        {
            return CreateType(new StructureType(
                baseType,
                fieldTypes,
                fieldNames,
                sourceType));
        }

        /// <summary>
        /// Creates a new objact type.
        /// </summary>
        /// <param name="fieldTypes">The objact field types.</param>
        /// <param name="sourceType">The source objact type.</param>
        /// <returns>The created objact type.</returns>
        public StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            StructureType sourceType)
        {
            Debug.Assert(sourceType != null, "Invalid source type");
            Debug.Assert(sourceType.NumFields == fieldTypes.Length, "Incompatible field types");
            return CreateStructureType(
                sourceType.BaseType,
                fieldTypes,
                sourceType.Names,
                sourceType.Source);
        }

        /// <summary>
        /// Creates a new generic view type that relies on an n-dimension index.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="indexType">The index type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        public StructureType CreateGenericViewType(
            TypeNode elementType,
            StructureType indexType,
            MemoryAddressSpace addressSpace)
        {
            // Try to resolve the managed type
            Type managedType = null;
            if (elementType.TryResolveManagedType(out Type managedElementType) &&
                indexType.TryResolveManagedType(out Type managedIndexType))
                managedType = GenericArrayViewType.MakeGenericType(managedElementType, managedIndexType);

            // Create the actual type
            var viewType = CreateViewType(elementType, addressSpace);
            return CreateStructureType(
                StructureType.Root,
                ImmutableArray.Create<TypeNode>(viewType, indexType),
                ImmutableArray<string>.Empty,
                managedType);
        }

        /// <summary>
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="length">The array length.</param>
        /// <returns>The created array type.</returns>
        public ArrayType CreateArrayType(TypeNode elementType, int length)
        {
            Debug.Assert(length > 0, "Invalid array length");

            // Try to resolve the managed type
            Type managedType = null;
            if (elementType.TryResolveManagedType(out Type managedElementType))
                managedType = managedElementType.MakeArrayType();

            // Create the actual type
            return CreateType(
                new ArrayType(
                    elementType,
                    length,
                    managedType));
        }

        /// <summary>
        /// Creates a new structure type that implements array functionality.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimension">The array dimension.</param>
        /// <returns>The created implementation structure type.</returns>
        public StructureType CreateArrayImplementationType(
            TypeNode elementType,
            int dimension)
        {
            Debug.Assert(dimension > 0, "Invalid array dimension");
            if (!elementType.IsStructureType && !elementType.IsPrimitiveType)
            {
                throw new NotSupportedException(
                    string.Format(
                        ErrorMessages.NotSupportedArrayElementType,
                        elementType));
            }

            // Try to resolve the managed type
            Type managedType = null;
            if (elementType.TryResolveManagedType(out Type managedElementType))
                managedType = managedElementType.MakeArrayType(dimension);

            var viewType = CreateViewType(elementType, MemoryAddressSpace.Local);
            var dimensionType = CreateArrayType(
                GetPrimitiveType(BasicValueType.Int32),
                dimension);
            return CreateStructureType(
                StructureType.Root,
                ImmutableArray.Create<TypeNode>(viewType, dimensionType),
                ImmutableArray.Create("View", "Extent"),
                managedType);
        }

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
            Debug.Assert(type != null, "Invalid type");

            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
                return GetPrimitiveType(basicValueType);
            else if (type.IsEnum)
                return CreateType(type.GetEnumUnderlyingType(), addressSpace);
            else if (type.IsArray)
            {
                var arrayElementType = CreateType(type.GetElementType(), addressSpace);
                var dimension = type.GetArrayRank();
                return CreateArrayImplementationType(arrayElementType, dimension);
            }
            else if (type.IsArrayViewType(out Type elementType))
                return CreateViewType(CreateType(elementType, addressSpace), addressSpace);
            else if (type.IsVoidPtr())
                return CreatePointerType(VoidType, addressSpace);
            else if (typeMapping.TryGetValue(type, out TypeNode typeNode))
                return typeNode;
            else if (type.IsByRef || type.IsPointer)
            {
                return CreatePointerType(
                    CreateType(type.GetElementType(), addressSpace),
                    addressSpace);
            }
            else
            {
                // Container (structure or class) type

                // FIXME: Disable classes for now
                if (type.IsClass)
                {
                    throw new NotSupportedException(
                        string.Format(
                            ErrorMessages.NotSupportedClassType,
                            type));
                }

                Debug.Assert(type.IsValueType, "Invalid structure type");

                var typeInfo = GetTypeInfo(type);
                var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(typeInfo.NumFields);
                var fieldNames = ImmutableArray.CreateBuilder<string>(typeInfo.NumFields);
                foreach (var fieldInfo in typeInfo.Fields)
                {
                    fieldTypes.Add(CreateType(fieldInfo.FieldType, addressSpace));
                    fieldNames.Add(fieldInfo.Name);
                }

                var fieldTypesArray = fieldTypes.MoveToImmutable();
                var fieldNamesArray = fieldNames.MoveToImmutable();

                var baseType = type.IsClass ?
                    CreateType(type.BaseType, addressSpace) as StructureType :
                    StructureType.Root;
                return CreateStructureType(
                    baseType,
                    fieldTypesArray,
                    fieldNamesArray,
                    type);
            }
        }

        /// <summary>
        /// Specializes the address space of the given <seeVoidType cref="AddressSpaceType"/>.
        /// </summary>
        /// <param name="addressSpaceType">The source type.</param>
        /// <param name="addressSpace">The new address space.</param>
        /// <returns>The created specialzized <see cref="AddressSpaceType"/>.</returns>
        public AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(addressSpaceType != null, "Invalid address space type");

            if (addressSpaceType is PointerType pointerType)
                return CreatePointerType(
                    pointerType.ElementType,
                    addressSpace);
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
        /// <returns>True, iff the type could be specialized.</returns>
        public bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            out TypeNode specializedType)
        {
            Debug.Assert(type != null, "Invalid type");

            if (type is AddressSpaceType addressSpaceType)
                specializedType = SpecializeAddressSpaceType(
                    addressSpaceType,
                    addressSpace);
            else
                specializedType = null;
            return specializedType != null;
        }

        /// <summary>
        /// Creates a type.
        /// </summary>
        /// <typeparam name="T">The type of the  type.</typeparam>
        /// <param name="type">The type to create.</param>
        /// <returns>The created type.</returns>
        private T CreateType<T>(T type)
            where T : TypeNode
        {
            typeLock.EnterUpgradeableReadLock();
            try
            {
                if (!unifiedTypes.TryGetValue(type, out TypeNode result))
                {
                    typeLock.EnterWriteLock();
                    result = type;
                    try
                    {
                        type.Id = Context.CreateNodeId();
                        unifiedTypes.Add(type, type);
                    }
                    finally
                    {
                        typeLock.ExitWriteLock();
                    }
                }
                return result as T;
            }
            finally
            {
                typeLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public override void ClearCache(ClearCacheMode mode)
        {
            base.ClearCache(mode);

            typeLock.EnterWriteLock();
            try
            {
                unifiedTypes.Clear();

                unifiedTypes.Add(VoidType, VoidType);
                unifiedTypes.Add(StringType, StringType);

                foreach (var basicType in BasicValueTypes)
                {
                    var type = GetPrimitiveType(basicType);
                    unifiedTypes.Add(type, type);
                }

                unifiedTypes.Add(IndexType, IndexType);
            }
            finally
            {
                typeLock.ExitWriteLock();
            }
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
