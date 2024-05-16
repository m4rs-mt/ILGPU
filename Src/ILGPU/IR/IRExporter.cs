// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRExporter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPU.IR
{
    internal struct IRExporter : IValueVisitor
    {
        public IRContainer? Container { get; }

        public IRExporter(IRContainer? container)
        {
            Container = container;
        }

        

        private static ImmutableArray<long> GetTargetIds(TerminatorValue value)
        {
            var nodeIds = new long[value.NumTargets];
            for (int i = 0; i < value.NumTargets; i++)
            {
                nodeIds[i] = value.Targets[i].Id;
            }

            return nodeIds.ToImmutableArray();
        }

        private void OnValueVisited(Value value, ImmutableArray<long>? nodes = default, long data = default, string? tag = default) =>
            Container?.Add(value, nodes, data, tag);

        public void Visit(MethodCall methodCall) =>
            OnValueVisited(methodCall,
                data: methodCall.Target.Id,
                tag: methodCall.Target.Name);

        public void Visit(Parameter parameter) =>
            OnValueVisited(parameter,
                data: parameter.Index,
                tag: parameter.Name);

        public void Visit(PhiValue phiValue)
        {
            if (phiValue.IsSealed)
            {
                OnValueVisited(phiValue,
                    nodes: phiValue.Sources.ToImmutableArray()
                        .Select(x => (long)x.Id)
                        .Zip(phiValue.Nodes
                            .ToImmutableArray()
                            .Select(x => (long)x.Id)
                            )
                        .SelectMany<(long, long), long>(x => [x.Item1, x.Item2])
                        .ToImmutableArray()
                    );
            }
        }

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
                data: ((long)value.Kind << 32) | (long)value.Flags);

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
                data: ((long)value.FieldSpan.Index << 32) | (long)value.FieldSpan.Span);

        public void Visit(NewView value) =>
            OnValueVisited(value,
                data: (long)value.ViewAddressSpace);

        public void Visit(GetViewLength value) =>
            OnValueVisited(value);

        public void Visit(AlignTo value) =>
            OnValueVisited(value,
                data: value.Type is AddressSpaceType ? (long)value.AddressSpace : -1);

        public void Visit(AsAligned value) =>
            OnValueVisited(value,
                data: value.Type is AddressSpaceType ? (long)value.AddressSpace : -1);

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
                data: ((long)value.FieldSpan.Index << 32) | (long)value.FieldSpan.Span);

        public void Visit(SetField value) =>
            OnValueVisited(value,
                data: ((long)value.FieldSpan.Index << 32) | (long)value.FieldSpan.Span);

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

        public void Visit(UndefinedValue undefined) =>
            OnValueVisited(undefined);

        public void Visit(HandleValue handle) =>
            OnValueVisited(handle);

        public void Visit(DebugAssertOperation debug) =>
            OnValueVisited(debug);

        public void Visit(WriteToOutput writeToOutput) =>
            OnValueVisited(writeToOutput);

        public void Visit(ReturnTerminator returnTerminator) =>
            OnValueVisited(returnTerminator);

        public void Visit(UnconditionalBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch));

        public void Visit(IfBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch)
                    .Prepend(branch.Condition.Id)
                    .ToImmutableArray(),
                data: (long)branch.Flags);

        public void Visit(SwitchBranch branch) =>
            OnValueVisited(branch,
                nodes: GetTargetIds(branch)
                    .Prepend(branch.Condition.Id)
                    .ToImmutableArray());

        public void Visit(LanguageEmitValue value) =>
            OnValueVisited(value);
    }
}
