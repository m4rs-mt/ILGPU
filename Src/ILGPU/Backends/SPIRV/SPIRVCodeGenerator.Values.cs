using ILGPU.IR.Values;
using System;
using System.Drawing.Imaging;

namespace ILGPU.Backends.SPIRV
{
    partial class SPIRVCodeGenerator
    {
        /// <inheritdoc />
        public void GenerateCode(MethodCall methodCall)
        {
            uint method = MethodAllocator.Load(methodCall.Target);
            uint returnType = GeneralTypeGenerator[methodCall.Method.ReturnType];
            uint returnValue = ValueAllocator.Allocate(methodCall);

            uint[] arguments = new uint[methodCall.Count];
            for(int i = 0; i < methodCall.Count; i++)
            {
                var argument = methodCall[i];
                uint id = ValueAllocator.Load(argument);
                arguments[i] = id;
            }

            Builder.GenerateOpFunctionCall(returnValue, returnType, method, arguments);
        }

        /// <inheritdoc />
        public void GenerateCode(PhiValue phiValue)
        {

        }

        /// <inheritdoc />
        public void GenerateCode(Parameter parameter) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(UnaryArithmeticValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(BinaryArithmeticValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(TernaryArithmeticValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(CompareValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(ConvertValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(IntAsPointerCast cast) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(PointerAsIntCast cast) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(PointerCast cast) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(AddressSpaceCast value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(FloatAsIntCast value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(IntAsFloatCast value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Predicate predicate) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GenericAtomic atomic) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(AtomicCAS atomicCAS) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Alloca alloca) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(MemoryBarrier barrier) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Load load) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Store store) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(LoadElementAddress value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(LoadFieldAddress value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(PrimitiveValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(StringValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(NullValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(StructureValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GetField value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(SetField value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GridIndexValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GroupIndexValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GridDimensionValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(GroupDimensionValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(WarpSizeValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(LaneIdxValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(DynamicMemoryLengthValue value) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(PredicateBarrier barrier) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Barrier barrier) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(Broadcast broadcast) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(WarpShuffle shuffle) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(SubWarpShuffle shuffle) =>
            throw new NotImplementedException();

        /// <inheritdoc />
        public void GenerateCode(DebugAssertOperation debug) =>
            throw new NotImplementedException();
    }
}
