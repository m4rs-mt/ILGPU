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
        private readonly Dictionary<TypeNode, string> nodeToIdMapping =
            new Dictionary<TypeNode, string>();

        /// <summary>
        /// Generates or gets a SPIR-V type definition for a node
        /// </summary>
        /// <param name="typeNode">The type node to generate the definition for</param>
        /// <param name="lastId">The last word id used</param>
        /// <param name="currentId">The next word id that should be used</param>
        /// <returns>The type definition</returns>
        /// <remarks>
        /// This will generate THE ENTIRE DEFINITION, including assignment to and id etc.
        /// </remarks>
        public string GetOrGenerateTypeDefinition(TypeNode typeNode, int lastId, out int currentId)
        {
            Debug.Assert(!(typeNode is ViewType), "Invalid view type");

            currentId = lastId;

            if (nodeToIdMapping.TryGetValue(typeNode, out string id))
            {
                return id;
            }


            switch (typeNode)
            {
                case VoidType v:
                    return GenerateVoidType(v, lastId, out currentId);
                case PrimitiveType p:
                    return GeneratePrimitiveType(p, lastId, out currentId);
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        private string GenerateVoidType(VoidType typeNode, int lastId, out int currentId)
        {
            currentId = lastId + 1;
            string idString = $"%{lastId}";
            string def = $"{idString} OpTypeVoid ";
            nodeToIdMapping[typeNode] = idString;
            return def;
        }

        private string GeneratePrimitiveType(PrimitiveType typeNode, int lastId, out int currentId)
        {
            currentId = lastId + 1;
            string idString = $"%{lastId}";
            string def = $"{idString} ";

            if (typeNode.BasicValueType.IsInt())
            {
                def += "OpTypeInt ";
                switch (typeNode.BasicValueType)
                {
                    case BasicValueType.Int1:
                        def += "1";
                        break;
                    case BasicValueType.Int8:
                        def += "8";
                        break;
                    case BasicValueType.Int16:
                        def += "16";
                        break;
                    case BasicValueType.Int32:
                        def += "32";
                        break;
                    case BasicValueType.Int64:
                        def += "64";
                        break;
                }
            }
            else if (typeNode.BasicValueType.IsFloat())
            {
                def += "OpTypeFloat ";
                switch (typeNode.BasicValueType)
                {
                    case BasicValueType.Float16:
                        def += "16";
                        break;
                    case BasicValueType.Float32:
                        def += "32";
                        break;
                    case BasicValueType.Float64:
                        def += "64";
                        break;
                }
            }
            else
            {
                //TODO: How to handle None?
                throw new NotImplementedException();
            }

            return def;
        }
    }
}
