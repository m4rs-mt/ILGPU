using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.Util;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// A type generator that can be used to store <see cref="TypeNode"/>s
    /// </summary>
    public class SPIRVTypeGenerator
    {
        #region Instance

        /// <summary>
        /// A set of all the types we have generated declarations for so far.
        /// </summary>
        /// <remarks>
        /// Having this set avoids the following scenario:
        /// 1. Someone asks for an id for a pointer type.
        /// 2. Someone asks for an id for the element type of that pointer type.
        /// Both types are now in the lookup.
        /// 4. We generate the type definition for the pointer type, which also generates
        /// the type definition for the element type, as we have to make sure it is
        /// generated too.
        /// 5. We generate the type definition for the element type. There are now two
        /// definitions for that type with the same id.
        /// 6. The world explodes.
        /// </remarks>
        private readonly HashSet<TypeNode> _generatedTypes = new HashSet<TypeNode>();

        private readonly Dictionary<TypeNode, uint> _lookup =
            new Dictionary<TypeNode, uint>();

        private readonly ConcurrentIdProvider _provider;

        private readonly ReaderWriterLockSlim _readerWriterLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Creates a new type generator that generates type definitions in SPIR-V.
        /// </summary>
        /// <param name="provider">The ID provider to generate new IDs.</param>
        public SPIRVTypeGenerator(ConcurrentIdProvider provider)
        {
            _provider = provider;
        }

        #endregion

        #region Indexer

        /// <summary>
        /// Gets a uint for a given <see cref="TypeNode"/>
        /// </summary>
        /// <param name="node">The node to get a variable for.</param>
        /// <returns>The variable for the node.</returns>
        public uint this[TypeNode node]
        {
            get
            {
                using var readWriteScope = _readerWriterLock.EnterUpgradeableReadScope();

                // This is repeated here since we don't want to enter a write scope if
                // possible.
                if (_lookup.TryGetValue(node, out uint typeId))
                    return typeId;

                using var writeScope = readWriteScope.EnterWriteScope();

                return GetOrCreateTypeId(node);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a uint for a given <see cref="TypeNode"/>
        /// </summary>
        /// <param name="node">The node to get a variable for.</param>
        /// <returns>The variable for the node.</returns>
        private uint GetOrCreateTypeId(TypeNode node)
        {
            if (_lookup.TryGetValue(node, out uint value))
            {
                return value;
            }

            uint next = _provider.Next();
            _lookup.Add(node, next);
            return next;
        }

        /// <summary>
        /// Generates all types and appends them to a builder.
        /// </summary>
        /// <param name="builder">The builder to use.</param>
        public void GenerateTypes(ISPIRVBuilder builder)
        {
            foreach (var node in _lookup.Keys)
            {
                if (_generatedTypes.Contains(node))
                {
                   continue;
                }

                GenerateTypeDefinition(node, builder);
            }
        }

        /// <summary>
        /// Generates the type definition for a given node. Uses already stored
        /// uints from the lookup for each <see cref="TypeNode"/>.
        /// </summary>
        /// <param name="typeNode">The type node to generate a definition for.</param>
        /// <param name="builder">The builder to use.</param>
        private void GenerateTypeDefinition(TypeNode typeNode, ISPIRVBuilder builder)
        {
            Debug.Assert(!(typeNode is ViewType), "Invalid view type");

            switch (typeNode)
            {
                case VoidType v:
                    GenerateVoidType(v, builder);
                    break;
                case PrimitiveType p:
                    GeneratePrimitiveType(p, builder);
                    break;
                case PointerType p:
                    GeneratePointerType(p, builder);
                    break;
                case StructureType s:
                    GenerateStructureType(s, builder);
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        private void GenerateVoidType(VoidType typeNode, ISPIRVBuilder builder) =>
            builder.GenerateOpTypeVoid(_lookup[typeNode]);

        private void GeneratePrimitiveType(PrimitiveType typeNode, ISPIRVBuilder builder)
        {
            uint id = _lookup[typeNode];
            if (typeNode.BasicValueType.IsInt())
            {
                // Only BasicValueType.None has a size of -1
                builder.GenerateOpTypeInt(id, (uint) typeNode.Size, 1);
            }
            else if (typeNode.BasicValueType.IsFloat())
            {
                // Only BasicValueType.None has a size of -1
                builder.GenerateOpTypeFloat(id, (uint) typeNode.Size);
            }
            else
            {
                throw new InvalidCodeGenerationException(
                    "Cannot generate primitive type with basic value type None");
            }

            _generatedTypes.Add(typeNode);
        }

        private void GeneratePointerType(PointerType typeNode, ISPIRVBuilder builder)
        {
            uint elementId = GetOrCreateTypeId(typeNode.ElementType);
            _generatedTypes.Add(typeNode.ElementType);
            GenerateTypeDefinition(typeNode.ElementType, builder);

            uint pointerId = GetOrCreateTypeId(typeNode);
            _generatedTypes.Add(typeNode);
            builder.GenerateOpTypePointer(pointerId,
                StorageClass.Generic,
                elementId);
        }

        private void GenerateStructureType(StructureType typeNode, ISPIRVBuilder builder)
        {
            foreach (var type in typeNode.Fields)
            {
                _generatedTypes.Add(type);
            }

            _generatedTypes.Add(typeNode);

            uint[] fields = typeNode.Fields.Select(GetOrCreateTypeId).ToArray();

            uint structureId = GetOrCreateTypeId(typeNode);
            builder.GenerateOpTypeStruct(structureId, fields);
        }

        #endregion
    }
}
