using ILGPU.IR.Types;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPU.IR
{
    /// <summary>
    /// A container wrapper for exported IR data.
    /// </summary>
    public class IRContainer
    {
        private readonly ConcurrentDictionary<NodeId, IRValue> values;
        private readonly ConcurrentDictionary<NodeId, IRType> types;

        internal IRContainer()
        {
            values = new ConcurrentDictionary<NodeId, IRValue>();
            types = new ConcurrentDictionary<NodeId, IRType>();
        }

        internal void Add(IRValue value) => values.TryAdd(value.Id, value);

        internal void Add(TypeNode type)
        {
            if (type.IsVoidType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Void, [], type.BasicValueType, 0));
            }
            else if (type.IsStringType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.String, [], type.BasicValueType, 0));
            }
            else if (type.IsPrimitiveType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Primitive, [], type.BasicValueType, 0));
            }
            else if (type.IsPointerType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Pointer, [((PointerType)type).ElementType.Id], type.BasicValueType, (long)((PointerType)type).AddressSpace));
                Add(((PointerType)type).ElementType);
            }
            else if (type.IsViewType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.View, [((ViewType)type).ElementType.Id], type.BasicValueType, (long)((ViewType)type).AddressSpace));
                Add(((ViewType)type).ElementType);
            }
            else if (type.IsArrayType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Array, [((ArrayType)type).ElementType.Id], type.BasicValueType, ((ArrayType)type).NumDimensions));
                Add(((ArrayType)type).ElementType);
            }
            else if (type.IsStructureType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Structure, ((StructureType)type).Fields.Select(t => t.Id).ToImmutableArray(), type.BasicValueType, 0));
                foreach (var fieldType in ((StructureType)type).Fields)
                {
                    Add(fieldType);
                }
            }
        }

        /// <summary>
        /// Exports the wrapped data for external API consumption.
        /// </summary>
        /// <returns>Tuple containing flattened array representations of the IR value and type graphs, see <see cref="IRValue"/> and <see cref="IRType"/> respectively.</returns>
        public (IRValue[] values, IRType[] types) Export() => (values.Values.ToArray(), types.Values.ToArray());
    }
}
