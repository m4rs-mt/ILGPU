using ILGPU.IR.Values;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ILGPU.IR
{
    public record struct IRValue(NodeId Id, ValueKind ValueKind, NodeId[] Nodes, long[] Data, string? Tag)
    {
        public class ValueVisitedEventArgs : EventArgs
        {
            public IRValue Value { get; set; }

            public ValueVisitedEventArgs(IRValue value)
            {
                Value = value;
            }
        }

        public class Container
        {
            private readonly ConcurrentDictionary<NodeId, IRValue> values;

            public IReadOnlyDictionary<NodeId, IRValue> Values => values;

            public Container()
            {
                values = new ConcurrentDictionary<NodeId, IRValue>();
            }

            public bool TryAdd(NodeId key, IRValue value) => values.TryAdd(key, value);
        }

        public struct Visitor : IValueVisitor
        {
            public Container? Container { get; }

            public Visitor(Container? container)
            {
                Container = container;
            }

            private static NodeId[] GetNodeIds(Value value)
            {
                var nodeIds = new NodeId[value.Count];
                for (int i = 0; i < value.Count; i++)
                {
                    nodeIds[i] = value[i].Id;
                }

                return nodeIds;
            }

            private void OnValueVisited(IRValue value) => Container?.TryAdd(value.Id, value);

            public void Visit(MethodCall methodCall) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = methodCall.Id,
                        ValueKind = methodCall.ValueKind,
                        Nodes = GetNodeIds(methodCall),
                        Data = [methodCall.Target.Id],
                        Tag = methodCall.Target.Name,
                    });

            public void Visit(Parameter parameter) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = parameter.Id,
                        ValueKind = parameter.ValueKind,
                        Nodes = [],
                        Data = [parameter.ParameterType.Id, parameter.Index],
                        Tag = parameter.Name,
                    });

            public void Visit(PhiValue phiValue) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = phiValue.Id,
                        ValueKind = phiValue.ValueKind,
                        Nodes = [],
                        Data = [phiValue.PhiType.Id],
                        Tag = null,
                    });

            public void Visit(UnaryArithmeticValue value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    });

            public void Visit(BinaryArithmeticValue value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    });

            public void Visit(TernaryArithmeticValue value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    });

            public void Visit(CompareValue value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.CompareType],
                        Tag = null,
                    });

            public void Visit(ConvertValue value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType, (long)value.TargetType, (long)value.Flags],
                        Tag = null,
                    });

            public void Visit(IntAsPointerCast value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    });

            public void Visit(PointerAsIntCast value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    });

            public void Visit(PointerCast value) =>
                OnValueVisited(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    });

            // TODO
            public void Visit(AddressSpaceCast value) { }
            public void Visit(ViewCast value) { }
            public void Visit(ArrayToViewCast value) { }
            public void Visit(FloatAsIntCast value) { }
            public void Visit(IntAsFloatCast value) { }
            public void Visit(Predicate predicate) { }
            public void Visit(GenericAtomic atomic) { }
            public void Visit(AtomicCAS atomicCAS) { }
            public void Visit(Alloca alloca) { }
            public void Visit(MemoryBarrier barrier) { }
            public void Visit(Load load) { }
            public void Visit(Store store) { }
            public void Visit(SubViewValue value) { }
            public void Visit(LoadElementAddress value) { }
            public void Visit(LoadArrayElementAddress value) { }
            public void Visit(LoadFieldAddress value) { }
            public void Visit(NewView value) { }
            public void Visit(GetViewLength value) { }
            public void Visit(AlignTo value) { }
            public void Visit(AsAligned value) { }
            public void Visit(NewArray value) { }
            public void Visit(GetArrayLength value) { }
            public void Visit(PrimitiveValue value) { }
            public void Visit(StringValue value) { }
            public void Visit(NullValue value) { }
            public void Visit(StructureValue value) { }
            public void Visit(GetField value) { }
            public void Visit(SetField value) { }
            public void Visit(AcceleratorTypeValue value) { }
            public void Visit(GridIndexValue value) { }
            public void Visit(GroupIndexValue value) { }
            public void Visit(GridDimensionValue value) { }
            public void Visit(GroupDimensionValue value) { }
            public void Visit(WarpSizeValue value) { }
            public void Visit(LaneIdxValue value) { }
            public void Visit(DynamicMemoryLengthValue value) { }
            public void Visit(PredicateBarrier barrier) { }
            public void Visit(Barrier barrier) { }
            public void Visit(Broadcast broadcast) { }
            public void Visit(WarpShuffle shuffle) { }
            public void Visit(SubWarpShuffle shuffle) { }
            public void Visit(UndefinedValue undefined) { }
            public void Visit(HandleValue handle) { }
            public void Visit(DebugAssertOperation debug) { }
            public void Visit(WriteToOutput writeToOutput) { }
            public void Visit(ReturnTerminator returnTerminator) { }
            public void Visit(UnconditionalBranch branch) { }
            public void Visit(IfBranch branch) { }
            public void Visit(SwitchBranch branch) { }
            public void Visit(LanguageEmitValue value) { }
        }
    }
}
