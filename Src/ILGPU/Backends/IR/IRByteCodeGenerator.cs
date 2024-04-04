using ILGPU.IR;
using ILGPU.IR.Values;
using System;

namespace ILGPU.Backends.IR
{
    partial class IRByteCodeGenerator : IBackendCodeGenerator<IRByteCodeBuilder>
    {
        public IRByteCodeBuilder Builder { get; }

        public Method Method { get; }

        public IRByteCodeGenerator(Method method)
        {
            Builder = new IRByteCodeBuilder();

            Method = method;
        }

        public void GenerateCode()
        {
            foreach (BasicBlock block in Method.Blocks)
            {
                foreach (Value value in block)
                {
                    this.GenerateCodeFor(value);
                }
            }
        }

        public void GenerateCode(MethodCall methodCall) => Builder.EmitValue(methodCall, w => w.Write(methodCall.Method.Name));
        public void GenerateCode(PhiValue phiValue) { }
        public void GenerateCode(Parameter parameter) => Builder.EmitValue(parameter, w =>
        {
            w.Write(parameter.Index);
            w.Write(parameter.Type.Size);
            w.Write(parameter.Type.Alignment);
        });
        public void GenerateCode(UnaryArithmeticValue value) => Builder.EmitValue(value, w => w.Write((int)value.Kind));
        public void GenerateCode(BinaryArithmeticValue value) => Builder.EmitValue(value, w => w.Write((int)value.Kind));
        public void GenerateCode(TernaryArithmeticValue value) => Builder.EmitValue(value, w => w.Write((int)value.Kind));
        public void GenerateCode(CompareValue value) => Builder.EmitValue(value, w => w.Write((int)value.Kind));
        public void GenerateCode(ConvertValue value) => Builder.EmitValue(value, w =>
        {
            w.Write((int)value.SourceType);
            w.Write((int)value.TargetType);
        });
        public void GenerateCode(IntAsPointerCast cast) => throw new NotImplementedException();
        public void GenerateCode(PointerAsIntCast cast) => throw new NotImplementedException();
        public void GenerateCode(PointerCast cast) => throw new NotImplementedException();
        public void GenerateCode(AddressSpaceCast value) => throw new NotImplementedException();
        public void GenerateCode(FloatAsIntCast value) => throw new NotImplementedException();
        public void GenerateCode(IntAsFloatCast value) => throw new NotImplementedException();
        public void GenerateCode(Predicate predicate) => throw new NotImplementedException();
        public void GenerateCode(GenericAtomic atomic) => throw new NotImplementedException();
        public void GenerateCode(AtomicCAS atomicCAS) => throw new NotImplementedException();
        public void GenerateCode(Alloca alloca) => throw new NotImplementedException();
        public void GenerateCode(MemoryBarrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Load load) => throw new NotImplementedException();
        public void GenerateCode(Store store) => throw new NotImplementedException();
        public void GenerateCode(LoadElementAddress value) => throw new NotImplementedException();
        public void GenerateCode(LoadFieldAddress value) => throw new NotImplementedException();
        public void GenerateCode(AlignTo value) => throw new NotImplementedException();
        public void GenerateCode(AsAligned value) => throw new NotImplementedException();
        public void GenerateCode(PrimitiveValue value) => Builder.EmitValue(value, w =>
        {
            w.Write((int)value.BasicValueType);
            w.Write(value.RawValue);
        });
        public void GenerateCode(StringValue value) => Builder.EmitValue(value, w => w.Write(value.String));
        public void GenerateCode(NullValue value) => throw new NotImplementedException();
        public void GenerateCode(StructureValue value) => throw new NotImplementedException();
        public void GenerateCode(GetField value) => throw new NotImplementedException();
        public void GenerateCode(SetField value) => throw new NotImplementedException();
        public void GenerateCode(GridIndexValue value) => throw new NotImplementedException();
        public void GenerateCode(GroupIndexValue value) => throw new NotImplementedException();
        public void GenerateCode(GridDimensionValue value) => throw new NotImplementedException();
        public void GenerateCode(GroupDimensionValue value) => throw new NotImplementedException();
        public void GenerateCode(WarpSizeValue value) => throw new NotImplementedException();
        public void GenerateCode(LaneIdxValue value) => throw new NotImplementedException();
        public void GenerateCode(DynamicMemoryLengthValue value) => throw new NotImplementedException();
        public void GenerateCode(PredicateBarrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Barrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Broadcast broadcast) => throw new NotImplementedException();
        public void GenerateCode(WarpShuffle shuffle) => throw new NotImplementedException();
        public void GenerateCode(SubWarpShuffle shuffle) => throw new NotImplementedException();
        public void GenerateCode(DebugAssertOperation debug) => throw new NotImplementedException();
        public void GenerateCode(WriteToOutput writeToOutput) => throw new NotImplementedException();
        public void GenerateCode(ReturnTerminator returnTerminator) => throw new NotImplementedException();
        public void GenerateCode(UnconditionalBranch branch) => throw new NotImplementedException();
        public void GenerateCode(IfBranch branch) => throw new NotImplementedException();
        public void GenerateCode(SwitchBranch branch) => throw new NotImplementedException();
        public void GenerateCode(LanguageEmitValue emit) => throw new NotImplementedException();
        public void GenerateConstants(IRByteCodeBuilder builder) => throw new NotImplementedException();
        public void GenerateHeader(IRByteCodeBuilder builder) => throw new NotImplementedException();
        public void Merge(IRByteCodeBuilder builder) => throw new NotImplementedException();
    }
}
