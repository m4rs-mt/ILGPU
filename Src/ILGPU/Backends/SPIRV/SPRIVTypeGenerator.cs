using ILGPU.Backends.OpenCL;
using ILGPU.IR.Types;
using ILGPU.Runtime.OpenCL;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// A SPIR-V type generator
    /// </summary>
    public class SPRIVTypeGenerator
    {
        private readonly Dictionary<TypeNode, uint> nodeToIdMapping =
            new Dictionary<TypeNode, uint>();

        public uint GetOrGenerateTypeDefinition(
            TypeNode typeNode,
            SPIRVBuilder builder,
            uint nextUsableId)
        {
            Debug.Assert(!(typeNode is ViewType), "Invalid view type");

            if (nodeToIdMapping.TryGetValue(typeNode, out uint id))
            {
                return id;
            }

            switch (typeNode)
            {
                case VoidType v:
                    return GenerateVoidType(v, builder, nextUsableId);
                case PrimitiveType p:
                    return GeneratePrimitiveType(p, builder, nextUsableId);
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        private uint GenerateVoidType(
            VoidType typeNode,
            SPIRVBuilder builder,
            uint nextUsableId)
        {
            nodeToIdMapping[typeNode] = nextUsableId;
            builder.GenerateOpTypeVoid(nextUsableId);
            return nextUsableId + 1;
        }

        private uint GeneratePrimitiveType(
            PrimitiveType typeNode,
            SPIRVBuilder builder,
            uint nextUsableId)
        {
            nodeToIdMapping[typeNode] = nextUsableId;
            if (typeNode.BasicValueType.IsInt())
            {
                uint size;
                switch (typeNode.BasicValueType)
                {
                    case BasicValueType.Int1:
                        size = 1;
                        break;
                    case BasicValueType.Int8:
                        size = 8;
                        break;
                    case BasicValueType.Int16:
                        size = 16;
                        break;
                    case BasicValueType.Int32:
                        size = 32;
                        break;
                    case BasicValueType.Int64:
                        size = 64;
                        break;
                    default:
                        size = 32;
                        break;
                }
                builder.GenerateOpTypeInt(nextUsableId, size, 1);
            }
            else if (typeNode.BasicValueType.IsFloat())
            {
                uint size;
                switch (typeNode.BasicValueType)
                {
                    case BasicValueType.Float16:
                        size = 16;
                        break;
                    case BasicValueType.Float32:
                        size = 32;
                        break;
                    case BasicValueType.Float64:
                        size = 64;
                        break;
                    default:
                        size = 32;
                        break;
                }
                builder.GenerateOpTypeInt(nextUsableId, size, 1);
            }
            else
            {
                //TODO: How to handle None?
                throw new NotImplementedException();
            }

            return nextUsableId + 1;
        }
    }
}
