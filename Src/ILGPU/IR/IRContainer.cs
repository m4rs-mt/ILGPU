// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRContainer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Void,
                    ImmutableArray<NodeId>.Empty, type.BasicValueType, 0));
            }
            else if (type.IsStringType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.String,
                    ImmutableArray<NodeId>.Empty, type.BasicValueType, 0));
            }
            else if (type.IsPrimitiveType)
            {
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Primitive,
                    ImmutableArray<NodeId>.Empty, type.BasicValueType, 0));
            }
            else if (type.IsPointerType)
            {
                Add(((PointerType)type).ElementType);
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Pointer,
                    ImmutableArray.Create(((PointerType)type).ElementType.Id),
                    type.BasicValueType, (long)((PointerType)type).AddressSpace));
            }
            else if (type.IsViewType)
            {
                Add(((ViewType)type).ElementType);
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.View,
                    ImmutableArray.Create(((ViewType)type).ElementType.Id),
                    type.BasicValueType, (long)((ViewType)type).AddressSpace));
            }
            else if (type.IsArrayType)
            {
                Add(((ArrayType)type).ElementType);
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Array,
                    ImmutableArray.Create(((ArrayType)type).ElementType.Id),
                    type.BasicValueType, ((ArrayType)type).NumDimensions));
            }
            else if (type.IsStructureType)
            {
                foreach (var fieldType in ((StructureType)type).Fields)
                {
                    Add(fieldType);
                }
                types.TryAdd(type.Id, new IRType(type.Id, IRType.Classifier.Structure,
                    ((StructureType)type).Fields.Select(t => t.Id).ToImmutableArray(),
                    type.BasicValueType, 0));
            }
        }

        /// <summary>
        /// Exports the wrapped data for external API consumption.
        /// </summary>
        /// <returns>
        /// Tuple containing flattened array representations of the
        /// IR value and type graphs, see <see cref="IRValue"/>
        /// and <see cref="IRType"/> respectively.
        /// </returns>
        public (IRValue[] values, IRType[] types) Export() =>
            (values.Values.ToArray(), types.Values.ToArray());
    }
}
