using ILGPU.IR.Values;
using System;

namespace ILGPU.IR
{
    internal record struct IRValue(NodeId Id, ValueKind ValueKind, NodeId[] Nodes, long[] Data, string? Tag)
    {
        public class ValueVisitedEventArgs : EventArgs
        {
            public IRValue Value { get; set; }

            public ValueVisitedEventArgs(IRValue value)
            {
                Value = value;
            }
        }

        public struct ValueVisitor : IValueVisitor
        {
            public event EventHandler<ValueVisitedEventArgs> ValueVisited;

            private static NodeId[] GetNodeIds(Value value)
            {
                var nodeIds = new NodeId[value.Count];
                for (int i = 0; i < value.Count; i++)
                {
                    nodeIds[i] = value[i].Id;
                }

                return nodeIds;
            }

            private void OnValueVisited(ValueVisitedEventArgs e) => ValueVisited?.Invoke(this, e);

            public void Visit(MethodCall methodCall) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = methodCall.Id,
                        ValueKind = methodCall.ValueKind,
                        Nodes = GetNodeIds(methodCall),
                        Data = [methodCall.Target.Id],
                        Tag = methodCall.Target.Name,
                    }));

            public void Visit(Parameter parameter) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = parameter.Id,
                        ValueKind = parameter.ValueKind,
                        Nodes = [],
                        Data = [parameter.ParameterType.Id, parameter.Index],
                        Tag = parameter.Name,
                    }));

            public void Visit(PhiValue phiValue) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = phiValue.Id,
                        ValueKind = phiValue.ValueKind,
                        Nodes = [],
                        Data = [phiValue.PhiType.Id],
                        Tag = null,
                    }));

            public void Visit(UnaryArithmeticValue value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    }));

            public void Visit(BinaryArithmeticValue value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    }));

            public void Visit(TernaryArithmeticValue value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.ArithmeticBasicValueType],
                        Tag = null,
                    }));

            public void Visit(CompareValue value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.Kind, (long)value.CompareType],
                        Tag = null,
                    }));

            public void Visit(ConvertValue value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType, (long)value.TargetType, (long)value.Flags],
                        Tag = null,
                    }));

            public void Visit(IntAsPointerCast value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    }));

            public void Visit(PointerAsIntCast value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    }));

            public void Visit(PointerCast value) =>
                OnValueVisited(new ValueVisitedEventArgs(
                    new IRValue
                    {
                        Id = value.Id,
                        ValueKind = value.ValueKind,
                        Nodes = GetNodeIds(value),
                        Data = [(long)value.SourceType.Id, (long)value.TargetType.Id],
                        Tag = null,
                    }));

            // TODO
            public void Visit(AddressSpaceCast value) => throw new NotImplementedException();
            public void Visit(ViewCast value) => throw new NotImplementedException();
            public void Visit(ArrayToViewCast value) => throw new NotImplementedException();
            public void Visit(FloatAsIntCast value) => throw new NotImplementedException();
            public void Visit(IntAsFloatCast value) => throw new NotImplementedException();
            public void Visit(Predicate predicate) => throw new NotImplementedException();
            public void Visit(GenericAtomic atomic) => throw new NotImplementedException();
            public void Visit(AtomicCAS atomicCAS) => throw new NotImplementedException();
            public void Visit(Alloca alloca) => throw new NotImplementedException();
            public void Visit(MemoryBarrier barrier) => throw new NotImplementedException();
            public void Visit(Load load) => throw new NotImplementedException();
            public void Visit(Store store) => throw new NotImplementedException();
            public void Visit(SubViewValue value) => throw new NotImplementedException();
            public void Visit(LoadElementAddress value) => throw new NotImplementedException();
            public void Visit(LoadArrayElementAddress value) => throw new NotImplementedException();
            public void Visit(LoadFieldAddress value) => throw new NotImplementedException();
            public void Visit(NewView value) => throw new NotImplementedException();
            public void Visit(GetViewLength value) => throw new NotImplementedException();
            public void Visit(AlignTo value) => throw new NotImplementedException();
            public void Visit(AsAligned value) => throw new NotImplementedException();
            public void Visit(NewArray value) => throw new NotImplementedException();
            public void Visit(GetArrayLength value) => throw new NotImplementedException();
            public void Visit(PrimitiveValue value) => throw new NotImplementedException();
            public void Visit(StringValue value) => throw new NotImplementedException();
            public void Visit(NullValue value) => throw new NotImplementedException();
            public void Visit(StructureValue value) => throw new NotImplementedException();
            public void Visit(GetField value) => throw new NotImplementedException();
            public void Visit(SetField value) => throw new NotImplementedException();
            public void Visit(AcceleratorTypeValue value) => throw new NotImplementedException();
            public void Visit(GridIndexValue value) => throw new NotImplementedException();
            public void Visit(GroupIndexValue value) => throw new NotImplementedException();
            public void Visit(GridDimensionValue value) => throw new NotImplementedException();
            public void Visit(GroupDimensionValue value) => throw new NotImplementedException();
            public void Visit(WarpSizeValue value) => throw new NotImplementedException();
            public void Visit(LaneIdxValue value) => throw new NotImplementedException();
            public void Visit(DynamicMemoryLengthValue value) => throw new NotImplementedException();
            public void Visit(PredicateBarrier barrier) => throw new NotImplementedException();
            public void Visit(Barrier barrier) => throw new NotImplementedException();
            public void Visit(Broadcast broadcast) => throw new NotImplementedException();
            public void Visit(WarpShuffle shuffle) => throw new NotImplementedException();
            public void Visit(SubWarpShuffle shuffle) => throw new NotImplementedException();
            public void Visit(UndefinedValue undefined) => throw new NotImplementedException();
            public void Visit(HandleValue handle) => throw new NotImplementedException();
            public void Visit(DebugAssertOperation debug) => throw new NotImplementedException();
            public void Visit(WriteToOutput writeToOutput) => throw new NotImplementedException();
            public void Visit(ReturnTerminator returnTerminator) => throw new NotImplementedException();
            public void Visit(UnconditionalBranch branch) => throw new NotImplementedException();
            public void Visit(IfBranch branch) => throw new NotImplementedException();
            public void Visit(SwitchBranch branch) => throw new NotImplementedException();
            public void Visit(LanguageEmitValue value) => throw new NotImplementedException();
        }
    }
}
