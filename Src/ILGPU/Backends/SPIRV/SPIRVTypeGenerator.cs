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
        public SPIRVTypeGenerator(SPIRVIdAllocator allocator, ISPIRVBuilder spirvBuilder)
        {
            idAllocator = allocator;
            builder = spirvBuilder;
        }

        private readonly SPIRVIdAllocator idAllocator;
        private readonly ISPIRVBuilder builder;

        #endregion

        /// <summary>
        /// Generates the type definition for a given node. Intermediate nodes and
        /// the given node are stored in this type generator's ID allocator. Binary
        /// instructions to generate this type are appended to this type generator's
        /// SPIR-V builder.
        /// </summary>
        /// <param name="typeNode">The type node to generate a definition for.</param>
        /// <returns>
        /// The <see cref="IdVariable"/> representing the final allocated type.
        /// </returns>
        public IdVariable GenerateTypeDefinition(TypeNode typeNode)
        {
            Debug.Assert(!(typeNode is ViewType), "Invalid view type");

            if (idAllocator.TryLoad(typeNode, out var variable))
            {
                return variable;
            }

            // The reason we return a value here where it's seemingly unnecessary
            // is to make generation of structure types easier. I *could* add
            // wrapper method with a void return, but it seems unnecessary.
            return typeNode switch
            {
                VoidType v => GenerateVoidType(v),
                PrimitiveType p => GeneratePrimitiveType(p),
                PointerType p => GeneratePointerType(p),
                StructureType s => GenerateStructureType(s),
                _ => throw new InvalidCodeGenerationException()
            };
        }

        private IdVariable GenerateVoidType(VoidType typeNode)
        {
            var typeVar = idAllocator.Allocate(typeNode);
            builder.GenerateOpTypeVoid(typeVar);
            return typeVar;
        }

        private IdVariable GeneratePrimitiveType(PrimitiveType typeNode)
        {
            var typeVar = idAllocator.Allocate(typeNode);
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
            else
            {
                throw new InvalidCodeGenerationException(
                    "Cannot generate primitive type with basic value type None");
            }

            return typeVar;
        }

        private IdVariable GeneratePointerType(PointerType typeNode)
        {
            var typeVar = idAllocator.Allocate(typeNode);
            var element = GenerateTypeDefinition(typeNode.ElementType);
            builder.GenerateOpTypePointer(typeVar, StorageClass.Generic, element);
            return typeVar;
        }

        private IdVariable GenerateStructureType(StructureType typeNode)
        {
            var fields = typeNode.Fields
                .Select(x => (uint) GenerateTypeDefinition(x))
                .ToArray();

            var typeVar = idAllocator.Allocate(typeNode);
            builder.GenerateOpTypeStruct(typeVar, fields);
            return typeVar;
        }

    }
}
