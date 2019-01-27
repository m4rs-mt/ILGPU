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
        private static readonly ImmutableArray<BasicValueType> BasicValueTypes =
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
        internal static readonly BasicValueType ViewIndexType = BasicValueType.Int32;

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim typeLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, TypeNode> unifiedTypes =
            new Dictionary<TypeNode, TypeNode>();
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

            IndexType = CreateType(new StructureType(
                ImmutableArray.Create<TypeNode>(
                    GetPrimitiveType(BasicValueType.Int32)),
                ImmutableArray<string>.Empty,
                typeof(Index)));
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
        public VoidType VoidType { get; private set; }

        /// <summary>
        /// Returns the memory type.
        /// </summary>
        public StringType StringType { get; private set; }

        /// <summary>
        /// Returns the main index type.
        /// </summary>
        public StructureType IndexType { get; private set; }

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
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(ImmutableArray<TypeNode> fieldTypes) =>
            CreateStructureType(fieldTypes, ImmutableArray<string>.Empty, null);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="fieldNames">The structure field names.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type sourceType)
        {
            return CreateType(new StructureType(
                fieldTypes,
                fieldNames,
                sourceType));
        }

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        public StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            StructureType sourceType)
        {
            Debug.Assert(sourceType != null, "Invalid source type");
            Debug.Assert(sourceType.NumChildren == fieldTypes.Length, "Incompatible field types");
            return CreateStructureType(fieldTypes, sourceType.Names, sourceType.Source);
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

            Debug.Assert(!type.IsArray, "Invalid array type");

            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
                return GetPrimitiveType(basicValueType);
            else if (type.IsEnum)
                return CreateType(type.GetEnumUnderlyingType(), addressSpace);
            else if (type == typeof(void))
                return VoidType;
            else if (type == typeof(string))
                return StringType;
            else if (type.IsArrayViewType(out Type elementType))
                return CreateViewType(CreateType(elementType, addressSpace), addressSpace);
            else if (type.IsVoidPtr())
                return CreatePointerType(VoidType, addressSpace);
            else if (type.IsByRef || type.IsPointer)
            {
                return CreatePointerType(
                    CreateType(type.GetElementType(), addressSpace),
                    addressSpace);
            }
            else
            {
                // Struct type
                Debug.Assert(type.IsValueType, "Invalid struct type");
                var typeInfo = GetTypeInfo(type);
                var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(typeInfo.NumFields);
                var fieldNames = ImmutableArray.CreateBuilder<string>(typeInfo.NumFields);
                foreach (var fieldInfo in typeInfo.Fields)
                {
                    fieldTypes.Add(CreateType(fieldInfo.FieldType, addressSpace));
                    fieldNames.Add(fieldInfo.Name);
                }
                return CreateStructureType(
                    fieldTypes.MoveToImmutable(),
                    fieldNames.MoveToImmutable(),
                    type);
            }
        }

        /// <summary>
        /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
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

        #endregion
    }
}
