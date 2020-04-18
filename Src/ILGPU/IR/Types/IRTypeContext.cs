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

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim typeLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, TypeNode> unifiedTypes =
            new Dictionary<TypeNode, TypeNode>();
        private readonly Dictionary<Type, TypeNode> typeMapping =
            new Dictionary<Type, TypeNode>();
        private readonly TypeNode[] indexTypes;
        private readonly PrimitiveType[] basicValueTypes;

        /// <summary>
        /// Constructs a new IR type context.
        /// </summary>
        /// <param name="context">The associated main context.</param>
        public IRTypeContext(Context context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));

            VoidType = CreateType(new VoidType(this));
            StringType = CreateType(new StringType(this));
            HandleType = CreateType(new HandleType(this));

            basicValueTypes = new PrimitiveType[BasicValueTypes.Length + 1];
            foreach (var type in BasicValueTypes)
            {
                basicValueTypes[(int)type] = CreateType(
                    new PrimitiveType(this, type));
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

            {
                var rootTypeBuilder = CreateStructureType(0);
                typeMapping.Add(
                    typeof(Array),
                    new StructureType(
                        this,
                        rootTypeBuilder));
            }

            typeMapping.Add(typeof(RuntimeFieldHandle), HandleType);
            typeMapping.Add(typeof(RuntimeMethodHandle), HandleType);
            typeMapping.Add(typeof(RuntimeTypeHandle), HandleType);

            // Setup index types
            indexTypes = new TypeNode[]
            {
                CreateType(typeof(Index1)),
                CreateType(typeof(Index2)),
                CreateType(typeof(Index3)),
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
        public TypeNode IndexType => indexTypes[0];

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
        /// Creates an intrinsic index type.
        /// </summary>
        /// <param name="dimension">The dimension of the index type.</param>
        /// <returns>The created index type.</returns>
        public TypeNode GetIndexType(int dimension)
        {
            Debug.Assert(
                dimension >= 1 && dimension <= indexTypes.Length,
                "Invalid index dimension");
            return indexTypes[dimension - 1];
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
                this,
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
                this,
                elementType,
                addressSpace));
        }

        /// <summary>
        /// Creates a new structure type builder with the given capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <returns>The created structure builder.</returns>
        public StructureType.Builder CreateStructureType(int capacity) =>
            new StructureType.Builder(this, capacity);

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
                : CreateType(new StructureType(
                    this,
                    builder));
        }

        /// <summary>
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimension">The array dimension.</param>
        /// <returns>The created array type.</returns>
        public ArrayType CreateArrayType(TypeNode elementType, int dimension)
        {
            Debug.Assert(dimension > 0, "Invalid array length");

            // Create the actual type
            return CreateType(new ArrayType(
                this,
                elementType,
                dimension));
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
            {
                return GetPrimitiveType(basicValueType);
            }
            else if (type.IsEnum)
            {
                return CreateType(type.GetEnumUnderlyingType(), addressSpace);
            }
            else if (type.IsArray)
            {
                var arrayElementType = CreateType(
                    type.GetElementType(),
                    addressSpace);
                var dimension = type.GetArrayRank();
                return CreateArrayType(arrayElementType, dimension);
            }
            else if (type.IsArrayViewType(out Type elementType))
            {
                return CreateViewType(
                    CreateType(elementType, addressSpace),
                    addressSpace);
            }
            else if (type.IsVoidPtr())
            {
                return CreatePointerType(VoidType, addressSpace);
            }
            else if (typeMapping.TryGetValue(type, out TypeNode typeNode))
            {
                return typeNode;
            }
            else if (type.IsByRef || type.IsPointer)
            {
                return CreatePointerType(
                    CreateType(type.GetElementType(), addressSpace),
                    addressSpace);
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
                // TODO: integrated type mapping

                // Must be a structure type
                Debug.Assert(type.IsValueType, "Invalid structure type");
                var typeInfo = GetTypeInfo(type);

                var builder = CreateStructureType(typeInfo.NumFlattendedFields);
                foreach (var field in typeInfo.Fields)
                    builder.Add(CreateType(field.FieldType, addressSpace));
                return CreateType(builder.Seal());
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
