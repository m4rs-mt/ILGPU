using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;

namespace ILGPU.IR
{
    internal struct IRExporter : IValueVisitor
    {
        public IRContainer? Container { get; }

        public IRExporter(IRContainer? container)
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

        private static NodeId[] GetTargetIds(TerminatorValue value)
        {
            var nodeIds = new NodeId[value.NumTargets];
            for (int i = 0; i < value.NumTargets; i++)
            {
                nodeIds[i] = value.Targets[i].Id;
            }

            return nodeIds;
        }

        private void OnValueVisited(Value value, NodeId[]? nodes = default, long data = default, string? tag = default)
        {
            Container?.Add(new IRValue
            {
                Method = value.Method.Id,
                BasicBlock = value.BasicBlock.Id,
                Id = value.Id,
                ValueKind = value.ValueKind,
                Type = value.Type.Id,
                Nodes = nodes ?? GetNodeIds(value),
                Data = data,
                Tag = tag,
            });

            Container?.Add(value.Type);
        }

        public void Visit(MethodCall methodCall) =>
            OnValueVisited(methodCall,
                data: methodCall.Target.Id,
                tag: methodCall.Target.Name);

        public void Visit(Parameter parameter) =>
            OnValueVisited(parameter,
                data: parameter.Index,
                tag: parameter.Name);

        public void Visit(PhiValue phiValue) =>
            OnValueVisited(phiValue);

        public void Visit(UnaryArithmeticValue value) =>
            OnValueVisited(value,
                data: (long)value.Kind);

        public void Visit(BinaryArithmeticValue value) =>
            OnValueVisited(value,
                data: (long)value.Kind);

        public void Visit(TernaryArithmeticValue value) =>
            OnValueVisited(value,
                data: (long)value.Kind);

        public void Visit(CompareValue value) =>
            OnValueVisited(value,
                data: (long)value.Kind);

        public void Visit(ConvertValue value) =>
            OnValueVisited(value,
                data: (long)value.Flags);

        public void Visit(IntAsPointerCast value) =>
            OnValueVisited(value);

        public void Visit(PointerAsIntCast value) =>
            OnValueVisited(value);

        public void Visit(PointerCast value) =>
            OnValueVisited(value);

        public void Visit(AddressSpaceCast value) =>
            OnValueVisited(value);

        public void Visit(ViewCast value) =>
            OnValueVisited(value);

        public void Visit(ArrayToViewCast value) =>
            OnValueVisited(value);

        public void Visit(FloatAsIntCast value) =>
            OnValueVisited(value);

        public void Visit(IntAsFloatCast value) =>
            OnValueVisited(value);

        public void Visit(Predicate predicate) =>
            OnValueVisited(predicate);

        public void Visit(GenericAtomic atomic) =>
            OnValueVisited(atomic,
                data: ((long)atomic.Kind << 32) | (long)atomic.Flags);

        public void Visit(AtomicCAS atomicCAS) =>
            OnValueVisited(atomicCAS,
                data: (long)atomicCAS.Flags);

        public void Visit(Alloca alloca) =>
            OnValueVisited(alloca,
                data: (long)alloca.AddressSpace);

        public void Visit(MemoryBarrier barrier) =>
            OnValueVisited(barrier,
                data: (long)barrier.Kind);

        public void Visit(Load load) =>
            OnValueVisited(load);

        public void Visit(Store store) =>
            OnValueVisited(store);

        public void Visit(SubViewValue value) =>
            OnValueVisited(value);

        public void Visit(LoadElementAddress value) =>
            OnValueVisited(value);

        public void Visit(LoadArrayElementAddress value) =>
            OnValueVisited(value);

        public void Visit(LoadFieldAddress value) =>
            OnValueVisited(value,
                data: (long)value.FieldSpan.Index | ((long)value.FieldSpan.Span << 32));

        public void Visit(NewView value) =>
            OnValueVisited(value,
                data: (long)value.ViewAddressSpace);

        public void Visit(GetViewLength value) =>
            OnValueVisited(value);

        public void Visit(AlignTo value) =>
            OnValueVisited(value,
                data: value.Type is AddressSpaceType ? (long)value.AddressSpace : default);

        public void Visit(AsAligned value) =>
            OnValueVisited(value,
                data: value.Type is AddressSpaceType ? (long)value.AddressSpace : default);

        public void Visit(NewArray value) =>
            OnValueVisited(value);

        public void Visit(GetArrayLength value) =>
            OnValueVisited(value);

        public void Visit(PrimitiveValue value) =>
            OnValueVisited(value,
                data: value.RawValue);

        public void Visit(StringValue value) =>
            OnValueVisited(value,
                data: value.Encoding.CodePage,
                tag: value.String);

        public void Visit(NullValue value) =>
            OnValueVisited(value);

        public void Visit(StructureValue value) =>
            OnValueVisited(value);

        public void Visit(GetField value) =>
            OnValueVisited(value,
                data: (long)value.FieldSpan.Index | ((long)value.FieldSpan.Span << 32));

        public void Visit(SetField value) =>
            OnValueVisited(value,
                data: (long)value.FieldSpan.Index | ((long)value.FieldSpan.Span << 32));

        public void Visit(AcceleratorTypeValue value) =>
            OnValueVisited(value);

        public void Visit(GridIndexValue value) =>
            OnValueVisited(value,
                data: (long)value.Dimension);

        public void Visit(GroupIndexValue value) =>
            OnValueVisited(value,
                data: (long)value.Dimension);

        public void Visit(GridDimensionValue value) =>
            OnValueVisited(value,
                data: (long)value.Dimension);

        public void Visit(GroupDimensionValue value) =>
            OnValueVisited(value,
                data: (long)value.Dimension);

        public void Visit(WarpSizeValue value) =>
            OnValueVisited(value);

        public void Visit(LaneIdxValue value) =>
            OnValueVisited(value);

        public void Visit(DynamicMemoryLengthValue value) =>
            OnValueVisited(value,
                data: (long)value.AddressSpace);

        public void Visit(PredicateBarrier barrier) =>
            OnValueVisited(barrier,
                data: (long)barrier.Kind);

        public void Visit(Barrier barrier) =>
            OnValueVisited(barrier,
                data: (long)barrier.Kind);

        public void Visit(Broadcast broadcast) =>
            OnValueVisited(broadcast,
                data: (long)broadcast.Kind);

        public void Visit(WarpShuffle shuffle) =>
            OnValueVisited(shuffle,
                data: (long)shuffle.Kind);

        public void Visit(SubWarpShuffle shuffle) =>
            OnValueVisited(shuffle,
                data: (long)shuffle.Kind);

        public void Visit(UndefinedValue undefined) { }

        public void Visit(HandleValue handle) { }

        public void Visit(DebugAssertOperation debug) =>
            OnValueVisited(debug);

        public void Visit(WriteToOutput writeToOutput) { }

        public void Visit(ReturnTerminator returnTerminator) =>
            OnValueVisited(returnTerminator);

        public void Visit(UnconditionalBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch));

        public void Visit(IfBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch),
                data: (long)branch.Flags);

        public void Visit(SwitchBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch));

        public void Visit(LanguageEmitValue value) { }
    }
}
