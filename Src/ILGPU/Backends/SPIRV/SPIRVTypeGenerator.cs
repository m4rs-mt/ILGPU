using ILGPU.IR.Types;
using ILGPU.Util;
using System.Diagnostics;
using System.Linq;
using IdVariable = ILGPU.Backends.SPIRV.SPIRVIdAllocator.IdVariable;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// A SPIR-V type generator
    /// </summary>
    public class SPIRVTypeGenerator
    {
        #region Instance

        /// <summary>
        /// Creates a new type generator that generates type definitions in SPIR-V.
        /// </summary>
        /// <param name="allocator">The ID allocator to store generated nodes.</param>
        /// <param name="spirvBuilder">
        /// The SPIR-V builder to append binary SPIR-V type declaration instructions to.
        /// </param>
        public SPIRVTypeGenerator(SPIRVIdAllocator allocator)
        {
            idAllocator = allocator;
        }

        private readonly SPIRVIdAllocator idAllocator;

        // Since the IdAllocator only keeps track of NodeIds, we need to keep track of
        // all the type nodes we encounter so we can generate their definitions later
        #endregion

        #region Methods

        public void GenerateTypes(ISPIRVBuilder builder)
        {
            foreach (var node in idAllocator.GetTypeNodes())
            {
                GenerateTypeDefinition(node, builder);
            }
        }

        /// <summary>
        /// Generates the type definition for a given node. Uses already stored
        /// <see cref="IdVariable"/>s from the allocator for each <see cref="TypeNode"/>.
        /// </summary>
        /// <param name="typeNode">The type node to generate a definition for.</param>
        /// <returns>
        /// The <see cref="IdVariable"/> representing the final allocated type.
        /// </returns>
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

        private void GenerateVoidType(VoidType typeNode, ISPIRVBuilder builder)
        {
            var typeVar = idAllocator.Load(typeNode);
            builder.GenerateOpTypeVoid(typeVar);
        }

        private void GeneratePrimitiveType(PrimitiveType typeNode, ISPIRVBuilder builder)
        {
            var typeVar = idAllocator.Load(typeNode);
            if (typeNode.BasicValueType.IsInt())
            {
                // Only BasicValueType.None has a size of -1
                builder.GenerateOpTypeInt(typeVar, (uint) typeNode.Size, 1);
            }
            else if (typeNode.BasicValueType.IsFloat())
            {
                // Only BasicValueType.None has a size of -1
                builder.GenerateOpTypeFloat(typeVar, (uint) typeNode.Size);
            }
            throw new InvalidCodeGenerationException(
                    "Cannot generate primitive type with basic value type None");
        }

        private void GeneratePointerType(PointerType typeNode, ISPIRVBuilder builder)
        {
            var typeVar = idAllocator.Load(typeNode);
            GenerateTypeDefinition(typeNode.ElementType, builder);
            var element = idAllocator.Load(typeNode);
            builder.GenerateOpTypePointer(typeVar, StorageClass.Generic, element);
        }

        private void GenerateStructureType(StructureType typeNode, ISPIRVBuilder builder)
        {
            var fields = typeNode.Fields
                .Select(x =>
                {
                    GenerateTypeDefinition(x, builder);
                    return (uint)idAllocator.Load(x);
                })
                .ToArray();

            var typeVar = idAllocator.Load(typeNode);
            builder.GenerateOpTypeStruct(typeVar, fields);
        }

        #endregion
    }
}
