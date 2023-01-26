// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// A type generator for managed IL types.
    /// </summary>
    sealed class VelocityTypeGenerator : DisposeBase
    {
        #region Static

        /// <summary>
        /// Maps basic types to vectorized basic types.
        /// </summary>
        private static readonly Type[] VectorizedBasicTypeMapping = new Type[]
        {
            null,                   // None

            typeof(VelocityWarp32), // Int1
            typeof(VelocityWarp32), // Int8
            typeof(VelocityWarp32), // Int16
            typeof(VelocityWarp32), // Int32
            typeof(VelocityWarp64), // Int64

            typeof(VelocityWarp32), // Float16
            typeof(VelocityWarp32), // Float32
            typeof(VelocityWarp64), // Float64
        };

        /// <summary>
        /// Gets a vectorized type corresponding to the given basic value type.
        /// </summary>
        public static Type GetVectorizedBasicType(BasicValueType basicValueType) =>
            VectorizedBasicTypeMapping[(int)basicValueType];

        /// <summary>
        /// Returns the default structure type implementation reflecting the basic
        /// type hierarchy.
        /// </summary>
        private static Type LoadStructureType<TTypeProvider>(
            StructureType structureType,
            VelocityTypeGenerator parent,
            in TTypeProvider typeProvider)
            where TTypeProvider : IExtendedTypeProvider
        {
            using var scopedLock = parent.RuntimeSystem.DefineRuntimeStruct(
                explicitLayout: typeProvider.UsesExplicitOffsets,
                out var typeBuilder);
            int index = 0;
            foreach (var (type, fieldAccess) in structureType)
            {
                var field = typeBuilder.DefineField(
                    StructureType.GetFieldName(index++),
                    type.LoadManagedType(typeProvider),
                    FieldAttributes.Public);

                int offset = structureType.GetOffset(fieldAccess.Index);
                typeProvider.SetOffset(field, offset);
            }

            return typeBuilder.CreateType();
        }

        #endregion

        #region Nested Types

        private interface IExtendedTypeProvider : IManagedTypeProvider
        {
            /// <summary>
            /// Returns true if this provider requires explicit offsets.
            /// </summary>
            bool UsesExplicitOffsets { get; }

            /// <summary>
            /// Sets an explicit field offset.
            /// </summary>
            void SetOffset(FieldBuilder fieldBuilder, int offset);
        }

        /// <summary>
        /// Provides linearized scalar versions of given scalar managed types.
        /// </summary>
        private readonly struct LinearScalarTypeProvider : IExtendedTypeProvider
        {
            private readonly VelocityTypeGenerator parent;
            private readonly TypeNode.ScalarManagedTypeProvider scalarProvider;

            /// <summary>
            /// Creates a new instance of the scalar type provider.
            /// </summary>
            /// <param name="typeGenerator">The parent IL type generator.</param>
            public LinearScalarTypeProvider(VelocityTypeGenerator typeGenerator)
            {
                parent = typeGenerator;
                scalarProvider = new TypeNode.ScalarManagedTypeProvider();
            }

            /// <summary>
            /// Returns the default managed type for the given primitive one.
            /// </summary>
            public Type GetPrimitiveType(PrimitiveType primitiveType) =>
                scalarProvider.GetPrimitiveType(primitiveType);

            /// <summary>
            /// Returns the default managed array type for the given array type.
            /// </summary>
            public Type GetArrayType(ArrayType arrayType) =>
                scalarProvider.GetArrayType(arrayType);

            /// <summary>
            /// Returns a specialized pointer implementation.
            /// </summary>
            public Type GetPointerType(PointerType pointerType) =>
                scalarProvider.GetPointerType(pointerType);

            /// <summary>
            /// Returns a specialized pointer-view implementation.
            /// </summary>
            public Type GetViewType(ViewType viewType) =>
                scalarProvider.GetViewType(viewType);

            /// <summary>
            /// Returns the default structure type implementation reflecting the basic
            /// type hierarchy.
            /// </summary>
            public Type GetStructureType(StructureType structureType) =>
                LoadStructureType(structureType, parent, this);

            /// <summary>
            /// Returns true.
            /// </summary>
            public bool UsesExplicitOffsets => true;

            /// <summary>
            /// Sets the current field offset.
            /// </summary>
            public void SetOffset(FieldBuilder fieldBuilder, int offset) =>
                fieldBuilder.SetOffset(offset);
        }

        /// <summary>
        /// Provides vectorized versions of given scalar managed types.
        /// </summary>
        private readonly struct VectorizedTypeProvider : IExtendedTypeProvider
        {
            private readonly VelocityTypeGenerator parent;

            /// <summary>
            /// Creates a new instance of the vectorized type provider.
            /// </summary>
            /// <param name="typeGenerator">The parent IL type generator.</param>
            public VectorizedTypeProvider(VelocityTypeGenerator typeGenerator)
            {
                parent = typeGenerator;
            }

            /// <summary>
            /// Returns the default managed type for the given primitive one.
            /// </summary>
            public Type GetPrimitiveType(PrimitiveType primitiveType) =>
                GetVectorizedBasicType(primitiveType.BasicValueType);

            /// <summary>
            /// Returns the default managed array type for the given array type.
            /// </summary>
            public Type GetArrayType(ArrayType arrayType) => arrayType.LoadManagedType();

            /// <summary>
            /// Returns a specialized pointer implementation.
            /// </summary>
            public Type GetPointerType(PointerType pointerType) =>
                GetVectorizedBasicType(BasicValueType.Int64);

            /// <summary>
            /// Returns a specialized pointer-view implementation.
            /// </summary>
            public Type GetViewType(ViewType viewType) =>
                PointerViews.ViewImplementation.GetImplementationType(
                    viewType.ElementType.LoadManagedType());

            /// <summary>
            /// Returns the default structure type implementation reflecting the basic
            /// type hierarchy.
            /// </summary>
            public Type GetStructureType(StructureType structureType) =>
                LoadStructureType(structureType, parent, this);

            /// <summary>
            /// Returns false.
            /// </summary>
            public bool UsesExplicitOffsets => false;

            /// <summary>
            /// Does not do anything.
            /// </summary>
            public void SetOffset(FieldBuilder fieldBuilder, int offset) { }
        }

        #endregion

        #region Static

        /// <summary>
        /// Gets or creates a new managed type using the given type provider instance.
        /// </summary>
        private static Type GetOrCreateType<TTypeProvider>(
            ReaderWriterLockSlim readerWriterLock,
            Dictionary<TypeNode, (Type Linear, Type Vectorized)> typeMapping,
            TypeNode typeNode,
            TTypeProvider typeProvider,
            Func<Type, Type, Type> typeSelector,
            Func<Type, Type, Type, (Type, Type)> typeBinder)
            where TTypeProvider : IManagedTypeProvider
        {
            // Synchronize all accesses below using a read/write scope
            using var readWriteScope = readerWriterLock.EnterUpgradeableReadScope();

            if (typeMapping.TryGetValue(typeNode, out var mappedType))
            {
                var selected = typeSelector(mappedType.Linear, mappedType.Vectorized);
                if (selected != null)
                    return selected;
            }

            // Get a new type instance
            using var writeScope = readWriteScope.EnterWriteScope();
            var newMappedType = typeNode.LoadManagedType(typeProvider);
            mappedType = typeBinder(
                mappedType.Linear,
                mappedType.Vectorized,
                newMappedType);
            typeMapping[typeNode] = mappedType;

            return typeSelector(mappedType.Linear, mappedType.Vectorized);
        }

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim readerWriterLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<TypeNode, (Type Linear, Type Vectorized)>
            typeMapping = new Dictionary<TypeNode, (Type, Type)>();

        /// <summary>
        /// Constructs a new IL type generator.
        /// </summary>
        /// <param name="runtimeSystem">The parent runtime system.</param>
        /// <param name="warpSize">The current warp size.</param>
        public VelocityTypeGenerator(RuntimeSystem runtimeSystem, int warpSize)
        {
            RuntimeSystem = runtimeSystem;
            WarpSize = warpSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent runtime system.
        /// </summary>
        public RuntimeSystem RuntimeSystem { get; }

        /// <summary>
        /// Returns the current warp size.
        /// </summary>
        public int WarpSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets or creates a linearized managed type for the given IR type.
        /// </summary>
        /// <param name="typeNode">The type to build a vectorized type for.</param>
        /// <returns>
        /// The linearized scalar managed type that corresponds to the given IR type.
        /// </returns>
        public Type GetLinearizedScalarType(TypeNode typeNode)
        {
            // Check for primitive types without locking
            if (typeNode is PrimitiveType || typeNode is PaddingType)
                return typeNode.LoadManagedType();

            // Get or create a new type
            return GetOrCreateType(
                readerWriterLock,
                typeMapping,
                typeNode,
                new LinearScalarTypeProvider(this),
                (linear, _) => linear,
                (_, vectorized, newLinear) => (newLinear, vectorized));
        }

        /// <summary>
        /// Gets or creates a vectorized managed type for the given IR type.
        /// </summary>
        /// <param name="typeNode">The type to build a vectorized type for.</param>
        /// <returns>
        /// The vectorized managed type that corresponds to the given IR type.
        /// </returns>
        public Type GetVectorizedType(TypeNode typeNode)
        {
            // Check for primitive types without locking
            if (typeNode is PrimitiveType || typeNode is PaddingType)
                return GetVectorizedBasicType(typeNode.BasicValueType);

            // Get or create a new type
            return GetOrCreateType(
                readerWriterLock,
                typeMapping,
                typeNode,
                new VectorizedTypeProvider(this),
                (_, vectorized) => vectorized,
                (linear, _, newVectorized) => (linear, newVectorized));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc cref="Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                readerWriterLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}

