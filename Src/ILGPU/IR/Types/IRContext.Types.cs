// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContext.Types.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.IR
{
    partial class IRContext
    {
        #region Instance

        private readonly Dictionary<TypeNode, TypeNode> unifiedTypes =
            new Dictionary<TypeNode, TypeNode>();
        private readonly PrimitiveType[] basicValueTypes;

        /// <summary>
        /// Constructs global types.
        /// </summary>
        private void CreateGlobalTypes()
        {
            VoidType = CreateType(new VoidType());
            MemoryType = CreateType(new MemoryType());
            StringType = CreateType(new StringType());

            foreach (var type in BasicValueTypes)
            {
                basicValueTypes[(int)type] = CreateType(
                    new PrimitiveType(type));
            }

            if ((Flags & IRContextFlags.Force32BitFloats) == IRContextFlags.Force32BitFloats)
            {
                basicValueTypes[
                    (int)BasicValueType.Float64] = basicValueTypes[
                        (int)BasicValueType.Float32];
            }

            IndexType = CreateType(new StructureType(
                ImmutableArray.Create<TypeNode>(
                    GetPrimitiveType(BasicValueType.Int32)),
                ImmutableArray<string>.Empty,
                null));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the void type.
        /// </summary>
        public VoidType VoidType { get; private set; }

        /// <summary>
        /// Returns the memory type.
        /// </summary>
        public MemoryType MemoryType { get; private set; }

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
        /// <param name="basicValueType">The type to resolve.</param>
        /// <returns>The resolved IR type node.</returns>
        public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
            basicValueTypes[(int)basicValueType];

        /// <summary>
        /// Creates a type.
        /// </summary>
        /// <typeparam name="T">The type of the  type.</typeparam>
        /// <param name="type">The type to create.</param>
        /// <returns>The created type.</returns>
        internal T CreateType<T>(T type)
            where T : TypeNode
        {
            irLock.EnterUpgradeableReadLock();
            try
            {
                if (!unifiedTypes.TryGetValue(type, out TypeNode result))
                {
                    irLock.EnterWriteLock();
                    result = type;
                    try
                    {
                        type.Id = CreateNodeId();
                        unifiedTypes.Add(type, type);
                    }
                    finally
                    {
                        irLock.ExitWriteLock();
                    }
                }
                return result as T;
            }
            finally
            {
                irLock.ExitUpgradeableReadLock();
            }
        }

        #endregion
    }
}
