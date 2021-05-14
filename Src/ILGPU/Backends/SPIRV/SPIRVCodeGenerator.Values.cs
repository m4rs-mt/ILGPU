using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;

namespace ILGPU.Backends.SPIRV
{
    partial class SPIRVCodeGenerator
    {
        public void GenerateCode(MethodCall methodCall) =>
            throw new NotImplementedException();

        public void GenerateCode(PhiValue phiValue) =>
            throw new NotImplementedException();

        public void GenerateCode(Parameter parameter) =>
            throw new NotImplementedException();

        public void GenerateCode(UnaryArithmeticValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(BinaryArithmeticValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(TernaryArithmeticValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(CompareValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(ConvertValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(IntAsPointerCast cast) =>
            throw new NotImplementedException();

        public void GenerateCode(PointerAsIntCast cast) =>
            throw new NotImplementedException();

        public void GenerateCode(PointerCast cast) =>
            throw new NotImplementedException();

        public void GenerateCode(AddressSpaceCast value) =>
            throw new NotImplementedException();

        public void GenerateCode(FloatAsIntCast value) =>
            throw new NotImplementedException();

        public void GenerateCode(IntAsFloatCast value) =>
            throw new NotImplementedException();

        public void GenerateCode(Predicate predicate) =>
            throw new NotImplementedException();

        public void GenerateCode(GenericAtomic atomic) =>
            throw new NotImplementedException();

        public void GenerateCode(AtomicCAS atomicCAS) =>
            throw new NotImplementedException();

        public void GenerateCode(Alloca alloca) =>
            throw new NotImplementedException();

        public void GenerateCode(MemoryBarrier barrier) =>
            throw new NotImplementedException();

        public void GenerateCode(Load load) =>
            throw new NotImplementedException();

        public void GenerateCode(Store store) =>
            throw new NotImplementedException();

        public void GenerateCode(LoadElementAddress value) =>
            throw new NotImplementedException();

        public void GenerateCode(LoadFieldAddress value) =>
            throw new NotImplementedException();

        public void GenerateCode(PrimitiveValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(StringValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(NullValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(StructureValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(GetField value) =>
            throw new NotImplementedException();

        public void GenerateCode(SetField value) =>
            throw new NotImplementedException();

        public void GenerateCode(GridIndexValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(GroupIndexValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(GridDimensionValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(GroupDimensionValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(WarpSizeValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(LaneIdxValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(DynamicMemoryLengthValue value) =>
            throw new NotImplementedException();

        public void GenerateCode(PredicateBarrier barrier) =>
            throw new NotImplementedException();

        public void GenerateCode(Barrier barrier) =>
            throw new NotImplementedException();

        public void GenerateCode(Broadcast broadcast) =>
            throw new NotImplementedException();

        public void GenerateCode(WarpShuffle shuffle) =>
            throw new NotImplementedException();

        public void GenerateCode(SubWarpShuffle shuffle) =>
            throw new NotImplementedException();

        public void GenerateCode(DebugAssertOperation debug) =>
            throw new NotImplementedException();
    }
}
