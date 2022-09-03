using System;
using ILGPU.Backends.SPIRV.Types;

#nullable enable

namespace ILGPU.Backends.SPIRV
{

    internal interface ISPIRVBuilder
    {
        byte[] ToByteArray();
    
        void AddMetadata(
            SPIRVWord magic,
            SPIRVWord version,
            SPIRVWord genMagic,
            SPIRVWord bound,
            SPIRVWord schema);

        // This is the best way I could come up with to
        // handle trying to merge different builders
        // Implementing classes will kinda just have to
        // deal with it
        void Merge(ISPIRVBuilder other);
        public void GenerateOpNop();

        public void GenerateOpUndef(IdResultType resultType, IdResult resultId);

        public void GenerateOpSourceContinued(LiteralString continuedSource);

        public void GenerateOpSource(SourceLanguage param0, LiteralInteger version, IdRef? file = null, LiteralString? source = null);

        public void GenerateOpSourceExtension(LiteralString extension);

        public void GenerateOpName(IdRef target, LiteralString name);

        public void GenerateOpMemberName(IdRef type, LiteralInteger member, LiteralString name);

        public void GenerateOpString(IdResult resultId, LiteralString @string);

        public void GenerateOpLine(IdRef file, LiteralInteger line, LiteralInteger column);

        public void GenerateOpExtension(LiteralString name);

        public void GenerateOpExtInstImport(IdResult resultId, LiteralString name);

        public void GenerateOpExtInst(IdResultType resultType, IdResult resultId, IdRef set, LiteralExtInstInteger instruction, params IdRef[] operand1Operand2);

        public void GenerateOpMemoryModel(AddressingModel param0, MemoryModel param1);

        public void GenerateOpEntryPoint(ExecutionModel param0, IdRef entryPoint, LiteralString name, params IdRef[] @interface);

        public void GenerateOpExecutionMode(IdRef entryPoint, ExecutionMode mode);

        public void GenerateOpCapability(Capability capability);

        public void GenerateOpTypeVoid(IdResult resultId);

        public void GenerateOpTypeBool(IdResult resultId);

        public void GenerateOpTypeInt(IdResult resultId, LiteralInteger width, LiteralInteger signedness);

        public void GenerateOpTypeFloat(IdResult resultId, LiteralInteger width);

        public void GenerateOpTypeVector(IdResult resultId, IdRef componentType, LiteralInteger componentCount);

        public void GenerateOpTypeMatrix(IdResult resultId, IdRef columnType, LiteralInteger columnCount);

        public void GenerateOpTypeImage(IdResult resultId, IdRef sampledType, Dim param2, LiteralInteger depth, LiteralInteger arrayed, LiteralInteger mS, LiteralInteger sampled, ImageFormat param7, AccessQualifier? param8 = null);

        public void GenerateOpTypeSampler(IdResult resultId);

        public void GenerateOpTypeSampledImage(IdResult resultId, IdRef imageType);

        public void GenerateOpTypeArray(IdResult resultId, IdRef elementType, IdRef length);

        public void GenerateOpTypeRuntimeArray(IdResult resultId, IdRef elementType);

        public void GenerateOpTypeStruct(IdResult resultId, params IdRef[] member0typemember1type);

        public void GenerateOpTypeOpaque(IdResult resultId, LiteralString thenameoftheopaquetype);

        public void GenerateOpTypePointer(IdResult resultId, StorageClass param1, IdRef type);

        public void GenerateOpTypeFunction(IdResult resultId, IdRef returnType, params IdRef[] parameter0TypeParameter1Type);

        public void GenerateOpTypeEvent(IdResult resultId);

        public void GenerateOpTypeDeviceEvent(IdResult resultId);

        public void GenerateOpTypeReserveId(IdResult resultId);

        public void GenerateOpTypeQueue(IdResult resultId);

        public void GenerateOpTypePipe(IdResult resultId, AccessQualifier qualifier);

        public void GenerateOpTypeForwardPointer(IdRef pointerType, StorageClass param1);

        public void GenerateOpConstantTrue(IdResultType resultType, IdResult resultId);

        public void GenerateOpConstantFalse(IdResultType resultType, IdResult resultId);

        public void GenerateOpConstant(IdResultType resultType, IdResult resultId, LiteralContextDependentNumber value);

        public void GenerateOpConstantComposite(IdResultType resultType, IdResult resultId, params IdRef[] constituents);

        public void GenerateOpConstantSampler(IdResultType resultType, IdResult resultId, SamplerAddressingMode param2, LiteralInteger param, SamplerFilterMode param4);

        public void GenerateOpConstantNull(IdResultType resultType, IdResult resultId);

        public void GenerateOpSpecConstantTrue(IdResultType resultType, IdResult resultId);

        public void GenerateOpSpecConstantFalse(IdResultType resultType, IdResult resultId);

        public void GenerateOpSpecConstant(IdResultType resultType, IdResult resultId, LiteralContextDependentNumber value);

        public void GenerateOpSpecConstantComposite(IdResultType resultType, IdResult resultId, params IdRef[] constituents);

        public void GenerateOpSpecConstantOp(IdResultType resultType, IdResult resultId, LiteralSpecConstantOpInteger opcode);

        public void GenerateOpFunction(IdResultType resultType, IdResult resultId, FunctionControl param2, IdRef functionType);

        public void GenerateOpFunctionParameter(IdResultType resultType, IdResult resultId);

        public void GenerateOpFunctionEnd();

        public void GenerateOpFunctionCall(IdResultType resultType, IdResult resultId, IdRef function, params IdRef[] argument0Argument1);

        public void GenerateOpVariable(IdResultType resultType, IdResult resultId, StorageClass param2, IdRef? initializer = null);

        public void GenerateOpImageTexelPointer(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef sample);

        public void GenerateOpLoad(IdResultType resultType, IdResult resultId, IdRef pointer, MemoryAccess? param3 = null);

        public void GenerateOpStore(IdRef pointer, IdRef @object, MemoryAccess? param2 = null);

        public void GenerateOpCopyMemory(IdRef target, IdRef source, MemoryAccess? param2 = null, MemoryAccess? param3 = null);

        public void GenerateOpCopyMemorySized(IdRef target, IdRef source, IdRef size, MemoryAccess? param3 = null, MemoryAccess? param4 = null);

        public void GenerateOpAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, params IdRef[] indexes);

        public void GenerateOpInBoundsAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, params IdRef[] indexes);

        public void GenerateOpPtrAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, IdRef element, params IdRef[] indexes);

        public void GenerateOpArrayLength(IdResultType resultType, IdResult resultId, IdRef structure, LiteralInteger arraymember);

        public void GenerateOpGenericPtrMemSemantics(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpInBoundsPtrAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, IdRef element, params IdRef[] indexes);

        public void GenerateOpDecorate(IdRef target, Decoration param1);

        public void GenerateOpMemberDecorate(IdRef structureType, LiteralInteger member, Decoration param2);

        public void GenerateOpDecorationGroup(IdResult resultId);

        public void GenerateOpGroupDecorate(IdRef decorationGroup, params IdRef[] targets);

        public void GenerateOpGroupMemberDecorate(IdRef decorationGroup, params PairIdRefLiteralInteger[] targets);

        public void GenerateOpVectorExtractDynamic(IdResultType resultType, IdResult resultId, IdRef vector, IdRef index);

        public void GenerateOpVectorInsertDynamic(IdResultType resultType, IdResult resultId, IdRef vector, IdRef component, IdRef index);

        public void GenerateOpVectorShuffle(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, params LiteralInteger[] components);

        public void GenerateOpCompositeConstruct(IdResultType resultType, IdResult resultId, params IdRef[] constituents);

        public void GenerateOpCompositeExtract(IdResultType resultType, IdResult resultId, IdRef composite, params LiteralInteger[] indexes);

        public void GenerateOpCompositeInsert(IdResultType resultType, IdResult resultId, IdRef @object, IdRef composite, params LiteralInteger[] indexes);

        public void GenerateOpCopyObject(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpTranspose(IdResultType resultType, IdResult resultId, IdRef matrix);

        public void GenerateOpSampledImage(IdResultType resultType, IdResult resultId, IdRef image, IdRef sampler);

        public void GenerateOpImageSampleImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageSampleExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4);

        public void GenerateOpImageSampleDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageSampleDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5);

        public void GenerateOpImageSampleProjImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageSampleProjExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4);

        public void GenerateOpImageSampleProjDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageSampleProjDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5);

        public void GenerateOpImageFetch(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef component, ImageOperands? param5 = null);

        public void GenerateOpImageDrefGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageRead(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageWrite(IdRef image, IdRef coordinate, IdRef texel, ImageOperands? param3 = null);

        public void GenerateOpImage(IdResultType resultType, IdResult resultId, IdRef sampledImage);

        public void GenerateOpImageQueryFormat(IdResultType resultType, IdResult resultId, IdRef image);

        public void GenerateOpImageQueryOrder(IdResultType resultType, IdResult resultId, IdRef image);

        public void GenerateOpImageQuerySizeLod(IdResultType resultType, IdResult resultId, IdRef image, IdRef levelofDetail);

        public void GenerateOpImageQuerySize(IdResultType resultType, IdResult resultId, IdRef image);

        public void GenerateOpImageQueryLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate);

        public void GenerateOpImageQueryLevels(IdResultType resultType, IdResult resultId, IdRef image);

        public void GenerateOpImageQuerySamples(IdResultType resultType, IdResult resultId, IdRef image);

        public void GenerateOpConvertFToU(IdResultType resultType, IdResult resultId, IdRef floatValue);

        public void GenerateOpConvertFToS(IdResultType resultType, IdResult resultId, IdRef floatValue);

        public void GenerateOpConvertSToF(IdResultType resultType, IdResult resultId, IdRef signedValue);

        public void GenerateOpConvertUToF(IdResultType resultType, IdResult resultId, IdRef unsignedValue);

        public void GenerateOpUConvert(IdResultType resultType, IdResult resultId, IdRef unsignedValue);

        public void GenerateOpSConvert(IdResultType resultType, IdResult resultId, IdRef signedValue);

        public void GenerateOpFConvert(IdResultType resultType, IdResult resultId, IdRef floatValue);

        public void GenerateOpQuantizeToF16(IdResultType resultType, IdResult resultId, IdRef value);

        public void GenerateOpConvertPtrToU(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpSatConvertSToU(IdResultType resultType, IdResult resultId, IdRef signedValue);

        public void GenerateOpSatConvertUToS(IdResultType resultType, IdResult resultId, IdRef unsignedValue);

        public void GenerateOpConvertUToPtr(IdResultType resultType, IdResult resultId, IdRef integerValue);

        public void GenerateOpPtrCastToGeneric(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpGenericCastToPtr(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpGenericCastToPtrExplicit(IdResultType resultType, IdResult resultId, IdRef pointer, StorageClass storage);

        public void GenerateOpBitcast(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpSNegate(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpFNegate(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpIAdd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFAdd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpISub(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFSub(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpIMul(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFMul(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSRem(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFRem(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpVectorTimesScalar(IdResultType resultType, IdResult resultId, IdRef vector, IdRef scalar);

        public void GenerateOpMatrixTimesScalar(IdResultType resultType, IdResult resultId, IdRef matrix, IdRef scalar);

        public void GenerateOpVectorTimesMatrix(IdResultType resultType, IdResult resultId, IdRef vector, IdRef matrix);

        public void GenerateOpMatrixTimesVector(IdResultType resultType, IdResult resultId, IdRef matrix, IdRef vector);

        public void GenerateOpMatrixTimesMatrix(IdResultType resultType, IdResult resultId, IdRef leftMatrix, IdRef rightMatrix);

        public void GenerateOpOuterProduct(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2);

        public void GenerateOpDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2);

        public void GenerateOpIAddCarry(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpISubBorrow(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUMulExtended(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSMulExtended(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpAny(IdResultType resultType, IdResult resultId, IdRef vector);

        public void GenerateOpAll(IdResultType resultType, IdResult resultId, IdRef vector);

        public void GenerateOpIsNan(IdResultType resultType, IdResult resultId, IdRef x);

        public void GenerateOpIsInf(IdResultType resultType, IdResult resultId, IdRef x);

        public void GenerateOpIsFinite(IdResultType resultType, IdResult resultId, IdRef x);

        public void GenerateOpIsNormal(IdResultType resultType, IdResult resultId, IdRef x);

        public void GenerateOpSignBitSet(IdResultType resultType, IdResult resultId, IdRef x);

        public void GenerateOpLessOrGreater(IdResultType resultType, IdResult resultId, IdRef x, IdRef y);

        public void GenerateOpOrdered(IdResultType resultType, IdResult resultId, IdRef x, IdRef y);

        public void GenerateOpUnordered(IdResultType resultType, IdResult resultId, IdRef x, IdRef y);

        public void GenerateOpLogicalEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpLogicalNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpLogicalOr(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpLogicalAnd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpLogicalNot(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpSelect(IdResultType resultType, IdResult resultId, IdRef condition, IdRef object1, IdRef object2);

        public void GenerateOpIEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpINotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpULessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpULessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpSLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFOrdGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpFUnordGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpShiftRightLogical(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift);

        public void GenerateOpShiftRightArithmetic(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift);

        public void GenerateOpShiftLeftLogical(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift);

        public void GenerateOpBitwiseOr(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpBitwiseXor(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpBitwiseAnd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpNot(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpBitFieldInsert(IdResultType resultType, IdResult resultId, IdRef @base, IdRef insert, IdRef offset, IdRef count);

        public void GenerateOpBitFieldSExtract(IdResultType resultType, IdResult resultId, IdRef @base, IdRef offset, IdRef count);

        public void GenerateOpBitFieldUExtract(IdResultType resultType, IdResult resultId, IdRef @base, IdRef offset, IdRef count);

        public void GenerateOpBitReverse(IdResultType resultType, IdResult resultId, IdRef @base);

        public void GenerateOpBitCount(IdResultType resultType, IdResult resultId, IdRef @base);

        public void GenerateOpDPdx(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpDPdy(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpFwidth(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpDPdxFine(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpDPdyFine(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpFwidthFine(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpDPdxCoarse(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpDPdyCoarse(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpFwidthCoarse(IdResultType resultType, IdResult resultId, IdRef p);

        public void GenerateOpEmitVertex();

        public void GenerateOpEndPrimitive();

        public void GenerateOpEmitStreamVertex(IdRef stream);

        public void GenerateOpEndStreamPrimitive(IdRef stream);

        public void GenerateOpControlBarrier(IdScope execution, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpMemoryBarrier(IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpAtomicLoad(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpAtomicStore(IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicExchange(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicCompareExchange(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics equal, IdMemorySemantics unequal, IdRef value, IdRef comparator);

        public void GenerateOpAtomicCompareExchangeWeak(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics equal, IdMemorySemantics unequal, IdRef value, IdRef comparator);

        public void GenerateOpAtomicIIncrement(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpAtomicIDecrement(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpAtomicIAdd(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicISub(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicSMin(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicUMin(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicSMax(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicUMax(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicAnd(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicOr(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicXor(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpPhi(IdResultType resultType, IdResult resultId, params PairIdRefIdRef[] variableParent);

        public void GenerateOpLoopMerge(IdRef mergeBlock, IdRef continueTarget, LoopControl param2);

        public void GenerateOpSelectionMerge(IdRef mergeBlock, SelectionControl param1);

        public void GenerateOpLabel(IdResult resultId);

        public void GenerateOpBranch(IdRef targetLabel);

        public void GenerateOpBranchConditional(IdRef condition, IdRef trueLabel, IdRef falseLabel, params LiteralInteger[] branchweights);

        public void GenerateOpSwitch(IdRef selector, IdRef @default, params PairLiteralIntegerIdRef[] target);

        public void GenerateOpKill();

        public void GenerateOpReturn();

        public void GenerateOpReturnValue(IdRef value);

        public void GenerateOpUnreachable();

        public void GenerateOpLifetimeStart(IdRef pointer, LiteralInteger size);

        public void GenerateOpLifetimeStop(IdRef pointer, LiteralInteger size);

        public void GenerateOpGroupAsyncCopy(IdResultType resultType, IdResult resultId, IdScope execution, IdRef destination, IdRef source, IdRef numElements, IdRef stride, IdRef @event);

        public void GenerateOpGroupWaitEvents(IdScope execution, IdRef numEvents, IdRef eventsList);

        public void GenerateOpGroupAll(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate);

        public void GenerateOpGroupAny(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate);

        public void GenerateOpGroupBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef localId);

        public void GenerateOpGroupIAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupUMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupSMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupUMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupSMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpReadPipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef pointer, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpWritePipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef pointer, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpReservedReadPipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef reserveId, IdRef index, IdRef pointer, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpReservedWritePipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef reserveId, IdRef index, IdRef pointer, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpReserveReadPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpReserveWritePipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpCommitReadPipe(IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpCommitWritePipe(IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpIsValidReserveId(IdResultType resultType, IdResult resultId, IdRef reserveId);

        public void GenerateOpGetNumPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpGetMaxPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpGroupReserveReadPipePackets(IdResultType resultType, IdResult resultId, IdScope execution, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpGroupReserveWritePipePackets(IdResultType resultType, IdResult resultId, IdScope execution, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpGroupCommitReadPipe(IdScope execution, IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpGroupCommitWritePipe(IdScope execution, IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpEnqueueMarker(IdResultType resultType, IdResult resultId, IdRef queue, IdRef numEvents, IdRef waitEvents, IdRef retEvent);

        public void GenerateOpEnqueueKernel(IdResultType resultType, IdResult resultId, IdRef queue, IdRef flags, IdRef nDRange, IdRef numEvents, IdRef waitEvents, IdRef retEvent, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign, params IdRef[] localSize);

        public void GenerateOpGetKernelNDrangeSubGroupCount(IdResultType resultType, IdResult resultId, IdRef nDRange, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpGetKernelNDrangeMaxSubGroupSize(IdResultType resultType, IdResult resultId, IdRef nDRange, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpGetKernelWorkGroupSize(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpGetKernelPreferredWorkGroupSizeMultiple(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpRetainEvent(IdRef @event);

        public void GenerateOpReleaseEvent(IdRef @event);

        public void GenerateOpCreateUserEvent(IdResultType resultType, IdResult resultId);

        public void GenerateOpIsValidEvent(IdResultType resultType, IdResult resultId, IdRef @event);

        public void GenerateOpSetUserEventStatus(IdRef @event, IdRef status);

        public void GenerateOpCaptureEventProfilingInfo(IdRef @event, IdRef profilingInfo, IdRef value);

        public void GenerateOpGetDefaultQueue(IdResultType resultType, IdResult resultId);

        public void GenerateOpBuildNDRange(IdResultType resultType, IdResult resultId, IdRef globalWorkSize, IdRef localWorkSize, IdRef globalWorkOffset);

        public void GenerateOpImageSparseSampleImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageSparseSampleExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4);

        public void GenerateOpImageSparseSampleDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageSparseSampleDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5);

        public void GenerateOpImageSparseSampleProjImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageSparseSampleProjExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4);

        public void GenerateOpImageSparseSampleProjDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageSparseSampleProjDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5);

        public void GenerateOpImageSparseFetch(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpImageSparseGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef component, ImageOperands? param5 = null);

        public void GenerateOpImageSparseDrefGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null);

        public void GenerateOpImageSparseTexelsResident(IdResultType resultType, IdResult resultId, IdRef residentCode);

        public void GenerateOpNoLine();

        public void GenerateOpAtomicFlagTestAndSet(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpAtomicFlagClear(IdRef pointer, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpImageSparseRead(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null);

        public void GenerateOpSizeOf(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpTypePipeStorage(IdResult resultId);

        public void GenerateOpConstantPipeStorage(IdResultType resultType, IdResult resultId, LiteralInteger packetSize, LiteralInteger packetAlignment, LiteralInteger capacity);

        public void GenerateOpCreatePipeFromPipeStorage(IdResultType resultType, IdResult resultId, IdRef pipeStorage);

        public void GenerateOpGetKernelLocalSizeForSubgroupCount(IdResultType resultType, IdResult resultId, IdRef subgroupCount, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpGetKernelMaxNumSubgroups(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign);

        public void GenerateOpTypeNamedBarrier(IdResult resultId);

        public void GenerateOpNamedBarrierInitialize(IdResultType resultType, IdResult resultId, IdRef subgroupCount);

        public void GenerateOpMemoryNamedBarrier(IdRef namedBarrier, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpModuleProcessed(LiteralString process);

        public void GenerateOpExecutionModeId(IdRef entryPoint, ExecutionMode mode);

        public void GenerateOpDecorateId(IdRef target, Decoration param1);

        public void GenerateOpGroupNonUniformElect(IdResultType resultType, IdResult resultId, IdScope execution);

        public void GenerateOpGroupNonUniformAll(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate);

        public void GenerateOpGroupNonUniformAny(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate);

        public void GenerateOpGroupNonUniformAllEqual(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value);

        public void GenerateOpGroupNonUniformBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef id);

        public void GenerateOpGroupNonUniformBroadcastFirst(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value);

        public void GenerateOpGroupNonUniformBallot(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate);

        public void GenerateOpGroupNonUniformInverseBallot(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value);

        public void GenerateOpGroupNonUniformBallotBitExtract(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef index);

        public void GenerateOpGroupNonUniformBallotBitCount(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value);

        public void GenerateOpGroupNonUniformBallotFindLSB(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value);

        public void GenerateOpGroupNonUniformBallotFindMSB(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value);

        public void GenerateOpGroupNonUniformShuffle(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef id);

        public void GenerateOpGroupNonUniformShuffleXor(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef mask);

        public void GenerateOpGroupNonUniformShuffleUp(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta);

        public void GenerateOpGroupNonUniformShuffleDown(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta);

        public void GenerateOpGroupNonUniformIAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformFAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformIMul(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformFMul(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformSMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformUMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformFMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformSMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformUMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformFMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformBitwiseAnd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformBitwiseOr(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformBitwiseXor(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformLogicalAnd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformLogicalOr(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformLogicalXor(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null);

        public void GenerateOpGroupNonUniformQuadBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef index);

        public void GenerateOpGroupNonUniformQuadSwap(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef direction);

        public void GenerateOpCopyLogical(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpPtrEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpPtrNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpPtrDiff(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpTerminateInvocation();

        public void GenerateOpSubgroupBallotKHR(IdResultType resultType, IdResult resultId, IdRef predicate);

        public void GenerateOpSubgroupFirstInvocationKHR(IdResultType resultType, IdResult resultId, IdRef value);

        public void GenerateOpSubgroupAllKHR(IdResultType resultType, IdResult resultId, IdRef predicate);

        public void GenerateOpSubgroupAnyKHR(IdResultType resultType, IdResult resultId, IdRef predicate);

        public void GenerateOpSubgroupAllEqualKHR(IdResultType resultType, IdResult resultId, IdRef predicate);

        public void GenerateOpGroupNonUniformRotateKHR(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta, IdRef? clusterSize = null);

        public void GenerateOpSubgroupReadInvocationKHR(IdResultType resultType, IdResult resultId, IdRef value, IdRef index);

        public void GenerateOpTraceRayKHR(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef payload);

        public void GenerateOpExecuteCallableKHR(IdRef sBTIndex, IdRef callableData);

        public void GenerateOpConvertUToAccelerationStructureKHR(IdResultType resultType, IdResult resultId, IdRef accel);

        public void GenerateOpIgnoreIntersectionKHR();

        public void GenerateOpTerminateRayKHR();

        public void GenerateOpSDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpUDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpUDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSUDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSUDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpUDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpUDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSUDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpSUDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null);

        public void GenerateOpTypeRayQueryKHR(IdResult resultId);

        public void GenerateOpRayQueryInitializeKHR(IdRef rayQuery, IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef rayOrigin, IdRef rayTMin, IdRef rayDirection, IdRef rayTMax);

        public void GenerateOpRayQueryTerminateKHR(IdRef rayQuery);

        public void GenerateOpRayQueryGenerateIntersectionKHR(IdRef rayQuery, IdRef hitT);

        public void GenerateOpRayQueryConfirmIntersectionKHR(IdRef rayQuery);

        public void GenerateOpRayQueryProceedKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetIntersectionTypeKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpGroupIAddNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFAddNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupUMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupSMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupUMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupSMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpFragmentMaskFetchAMD(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate);

        public void GenerateOpFragmentFetchAMD(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef fragmentIndex);

        public void GenerateOpReadClockKHR(IdResultType resultType, IdResult resultId, IdScope scope);

        public void GenerateOpImageSampleFootprintNV(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef granularity, IdRef coarse, ImageOperands? param6 = null);

        public void GenerateOpGroupNonUniformPartitionNV(IdResultType resultType, IdResult resultId, IdRef value);

        public void GenerateOpWritePackedPrimitiveIndices4x8NV(IdRef indexOffset, IdRef packedIndices);

        public void GenerateOpReportIntersectionNV(IdResultType resultType, IdResult resultId, IdRef hit, IdRef hitKind);

        public void GenerateOpReportIntersectionKHR(IdResultType resultType, IdResult resultId, IdRef hit, IdRef hitKind);

        public void GenerateOpIgnoreIntersectionNV();

        public void GenerateOpTerminateRayNV();

        public void GenerateOpTraceNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef payloadId);

        public void GenerateOpTraceMotionNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef time, IdRef payloadId);

        public void GenerateOpTraceRayMotionNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef time, IdRef payload);

        public void GenerateOpTypeAccelerationStructureNV(IdResult resultId);

        public void GenerateOpTypeAccelerationStructureKHR(IdResult resultId);

        public void GenerateOpExecuteCallableNV(IdRef sBTIndex, IdRef callableDataId);

        public void GenerateOpTypeCooperativeMatrixNV(IdResult resultId, IdRef componentType, IdScope execution, IdRef rows, IdRef columns);

        public void GenerateOpCooperativeMatrixLoadNV(IdResultType resultType, IdResult resultId, IdRef pointer, IdRef stride, IdRef columnMajor, MemoryAccess? param5 = null);

        public void GenerateOpCooperativeMatrixStoreNV(IdRef pointer, IdRef @object, IdRef stride, IdRef columnMajor, MemoryAccess? param4 = null);

        public void GenerateOpCooperativeMatrixMulAddNV(IdResultType resultType, IdResult resultId, IdRef a, IdRef b, IdRef c);

        public void GenerateOpCooperativeMatrixLengthNV(IdResultType resultType, IdResult resultId, IdRef type);

        public void GenerateOpBeginInvocationInterlockEXT();

        public void GenerateOpEndInvocationInterlockEXT();

        public void GenerateOpDemoteToHelperInvocation();

        public void GenerateOpDemoteToHelperInvocationEXT();

        public void GenerateOpIsHelperInvocationEXT(IdResultType resultType, IdResult resultId);

        public void GenerateOpConvertUToImageNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpConvertUToSamplerNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpConvertImageToUNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpConvertSamplerToUNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpConvertUToSampledImageNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpConvertSampledImageToUNV(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpSamplerImageAddressingModeNV(LiteralInteger bitWidth);

        public void GenerateOpSubgroupShuffleINTEL(IdResultType resultType, IdResult resultId, IdRef data, IdRef invocationId);

        public void GenerateOpSubgroupShuffleDownINTEL(IdResultType resultType, IdResult resultId, IdRef current, IdRef next, IdRef delta);

        public void GenerateOpSubgroupShuffleUpINTEL(IdResultType resultType, IdResult resultId, IdRef previous, IdRef current, IdRef delta);

        public void GenerateOpSubgroupShuffleXorINTEL(IdResultType resultType, IdResult resultId, IdRef data, IdRef value);

        public void GenerateOpSubgroupBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef ptr);

        public void GenerateOpSubgroupBlockWriteINTEL(IdRef ptr, IdRef data);

        public void GenerateOpSubgroupImageBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate);

        public void GenerateOpSubgroupImageBlockWriteINTEL(IdRef image, IdRef coordinate, IdRef data);

        public void GenerateOpSubgroupImageMediaBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef width, IdRef height);

        public void GenerateOpSubgroupImageMediaBlockWriteINTEL(IdRef image, IdRef coordinate, IdRef width, IdRef height, IdRef data);

        public void GenerateOpUCountLeadingZerosINTEL(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpUCountTrailingZerosINTEL(IdResultType resultType, IdResult resultId, IdRef operand);

        public void GenerateOpAbsISubINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpAbsUSubINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpIAddSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUAddSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpIAverageINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUAverageINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpIAverageRoundedINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUAverageRoundedINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpISubSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUSubSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpIMul32x16INTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpUMul32x16INTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2);

        public void GenerateOpConstantFunctionPointerINTEL(IdResultType resultType, IdResult resultId, IdRef function);

        public void GenerateOpFunctionPointerCallINTEL(IdResultType resultType, IdResult resultId, params IdRef[] operand1);

        public void GenerateOpAsmTargetINTEL(IdResultType resultType, IdResult resultId, LiteralString asmtarget);

        public void GenerateOpAsmINTEL(IdResultType resultType, IdResult resultId, IdRef asmtype, IdRef target, LiteralString asminstructions, LiteralString constraints);

        public void GenerateOpAsmCallINTEL(IdResultType resultType, IdResult resultId, IdRef asm, params IdRef[] argument0);

        public void GenerateOpAtomicFMinEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAtomicFMaxEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpAssumeTrueKHR(IdRef condition);

        public void GenerateOpExpectKHR(IdResultType resultType, IdResult resultId, IdRef value, IdRef expectedValue);

        public void GenerateOpDecorateString(IdRef target, Decoration param1);

        public void GenerateOpDecorateStringGOOGLE(IdRef target, Decoration param1);

        public void GenerateOpMemberDecorateString(IdRef structType, LiteralInteger member, Decoration param2);

        public void GenerateOpMemberDecorateStringGOOGLE(IdRef structType, LiteralInteger member, Decoration param2);

        public void GenerateOpVmeImageINTEL(IdResultType resultType, IdResult resultId, IdRef imageType, IdRef sampler);

        public void GenerateOpTypeVmeImageINTEL(IdResult resultId, IdRef imageType);

        public void GenerateOpTypeAvcImePayloadINTEL(IdResult resultId);

        public void GenerateOpTypeAvcRefPayloadINTEL(IdResult resultId);

        public void GenerateOpTypeAvcSicPayloadINTEL(IdResult resultId);

        public void GenerateOpTypeAvcMcePayloadINTEL(IdResult resultId);

        public void GenerateOpTypeAvcMceResultINTEL(IdResult resultId);

        public void GenerateOpTypeAvcImeResultINTEL(IdResult resultId);

        public void GenerateOpTypeAvcImeResultSingleReferenceStreamoutINTEL(IdResult resultId);

        public void GenerateOpTypeAvcImeResultDualReferenceStreamoutINTEL(IdResult resultId);

        public void GenerateOpTypeAvcImeSingleReferenceStreaminINTEL(IdResult resultId);

        public void GenerateOpTypeAvcImeDualReferenceStreaminINTEL(IdResult resultId);

        public void GenerateOpTypeAvcRefResultINTEL(IdResult resultId);

        public void GenerateOpTypeAvcSicResultINTEL(IdResult resultId);

        public void GenerateOpSubgroupAvcMceGetDefaultInterBaseMultiReferencePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceSetInterBaseMultiReferencePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef referenceBasePenalty, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetDefaultInterShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceSetInterShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef packedShapePenalty, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetDefaultInterDirectionPenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceSetInterDirectionPenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef directionCost, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceGetDefaultInterMotionVectorCostTableINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceGetDefaultHighPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpSubgroupAvcMceGetDefaultMediumPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpSubgroupAvcMceGetDefaultLowPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpSubgroupAvcMceSetMotionVectorCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef packedCostCenterDelta, IdRef packedCostTable, IdRef costPrecision, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaModePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp);

        public void GenerateOpSubgroupAvcMceGetDefaultNonDcLumaIntraPenaltyINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpSubgroupAvcMceGetDefaultIntraChromaModeBasePenaltyINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpSubgroupAvcMceSetAcOnlyHaarINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceSetSourceInterlacedFieldPolarityINTEL(IdResultType resultType, IdResult resultId, IdRef sourceFieldPolarity, IdRef payload);

        public void GenerateOpSubgroupAvcMceSetSingleReferenceInterlacedFieldPolarityINTEL(IdResultType resultType, IdResult resultId, IdRef referenceFieldPolarity, IdRef payload);

        public void GenerateOpSubgroupAvcMceSetDualReferenceInterlacedFieldPolaritiesINTEL(IdResultType resultType, IdResult resultId, IdRef forwardReferenceFieldPolarity, IdRef backwardReferenceFieldPolarity, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToImePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToImeResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToRefPayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToRefResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToSicPayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceConvertToSicResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetBestInterDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterMajorShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterMinorShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterDirectionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterMotionVectorCountINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcMceGetInterReferenceInterlacedFieldPolaritiesINTEL(IdResultType resultType, IdResult resultId, IdRef packedReferenceIds, IdRef packedReferenceParameterFieldPolarities, IdRef payload);

        public void GenerateOpSubgroupAvcImeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef partitionMask, IdRef sADAdjustment);

        public void GenerateOpSubgroupAvcImeSetSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef refOffset, IdRef searchWindowConfig, IdRef payload);

        public void GenerateOpSubgroupAvcImeSetDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef fwdRefOffset, IdRef bwdRefOffset, IdRef idSearchWindowConfig, IdRef payload);

        public void GenerateOpSubgroupAvcImeRefWindowSizeINTEL(IdResultType resultType, IdResult resultId, IdRef searchWindowConfig, IdRef dualRef);

        public void GenerateOpSubgroupAvcImeAdjustRefOffsetINTEL(IdResultType resultType, IdResult resultId, IdRef refOffset, IdRef srcCoord, IdRef refWindowSize, IdRef imageSize);

        public void GenerateOpSubgroupAvcImeConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeSetMaxMotionVectorCountINTEL(IdResultType resultType, IdResult resultId, IdRef maxMotionVectorCount, IdRef payload);

        public void GenerateOpSubgroupAvcImeSetUnidirectionalMixDisableINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeSetEarlySearchTerminationThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef threshold, IdRef payload);

        public void GenerateOpSubgroupAvcImeSetWeightedSadINTEL(IdResultType resultType, IdResult resultId, IdRef packedSadWeights, IdRef payload);

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload);

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload);

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload, IdRef streaminComponents);

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload, IdRef streaminComponents);

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload);

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload);

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload, IdRef streaminComponents);

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload, IdRef streaminComponents);

        public void GenerateOpSubgroupAvcImeConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetSingleReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetDualReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeStripSingleReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeStripDualReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape);

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape);

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape);

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction);

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction);

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction);

        public void GenerateOpSubgroupAvcImeGetBorderReachedINTEL(IdResultType resultType, IdResult resultId, IdRef imageSelect, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetTruncatedSearchIndicationINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetUnidirectionalEarlySearchTerminationINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumMotionVectorINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcFmeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef motionVectors, IdRef majorShapes, IdRef minorShapes, IdRef direction, IdRef pixelResolution, IdRef sadAdjustment);

        public void GenerateOpSubgroupAvcBmeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef motionVectors, IdRef majorShapes, IdRef minorShapes, IdRef direction, IdRef pixelResolution, IdRef bidirectionalWeight, IdRef sadAdjustment);

        public void GenerateOpSubgroupAvcRefConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcRefSetBidirectionalMixDisableINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcRefSetBilinearFilterEnableINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcRefEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload);

        public void GenerateOpSubgroupAvcRefEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload);

        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef payload);

        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceInterlacedINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef packedReferenceFieldPolarities, IdRef payload);

        public void GenerateOpSubgroupAvcRefConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord);

        public void GenerateOpSubgroupAvcSicConfigureSkcINTEL(IdResultType resultType, IdResult resultId, IdRef skipBlockPartitionType, IdRef skipMotionVectorMask, IdRef motionVectors, IdRef bidirectionalWeight, IdRef sadAdjustment, IdRef payload);

        public void GenerateOpSubgroupAvcSicConfigureIpeLumaINTEL(IdResultType resultType, IdResult resultId, IdRef lumaIntraPartitionMask, IdRef intraNeighbourAvailabilty, IdRef leftEdgeLumaPixels, IdRef upperLeftCornerLumaPixel, IdRef upperEdgeLumaPixels, IdRef upperRightEdgeLumaPixels, IdRef sadAdjustment, IdRef payload);

        public void GenerateOpSubgroupAvcSicConfigureIpeLumaChromaINTEL(IdResultType resultType, IdResult resultId, IdRef lumaIntraPartitionMask, IdRef intraNeighbourAvailabilty, IdRef leftEdgeLumaPixels, IdRef upperLeftCornerLumaPixel, IdRef upperEdgeLumaPixels, IdRef upperRightEdgeLumaPixels, IdRef leftEdgeChromaPixels, IdRef upperLeftCornerChromaPixel, IdRef upperEdgeChromaPixels, IdRef sadAdjustment, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetMotionVectorMaskINTEL(IdResultType resultType, IdResult resultId, IdRef skipBlockPartitionType, IdRef direction);

        public void GenerateOpSubgroupAvcSicConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetIntraLumaShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef packedShapePenalty, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetIntraLumaModeCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef lumaModePenalty, IdRef lumaPackedNeighborModes, IdRef lumaPackedNonDcPenalty, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetIntraChromaModeCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef chromaModeBasePenalty, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetBilinearFilterEnableINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetSkcForwardTransformEnableINTEL(IdResultType resultType, IdResult resultId, IdRef packedSadCoefficients, IdRef payload);

        public void GenerateOpSubgroupAvcSicSetBlockBasedRawSkipSadINTEL(IdResultType resultType, IdResult resultId, IdRef blockBasedSkipType, IdRef payload);

        public void GenerateOpSubgroupAvcSicEvaluateIpeINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef payload);

        public void GenerateOpSubgroupAvcSicEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload);

        public void GenerateOpSubgroupAvcSicEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload);

        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef payload);

        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceInterlacedINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef packedReferenceFieldPolarities, IdRef payload);

        public void GenerateOpSubgroupAvcSicConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetIpeLumaShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetBestIpeLumaDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetBestIpeChromaDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetPackedIpeLumaModesINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetIpeChromaModeINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaCountThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaSumThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpSubgroupAvcSicGetInterRawSadsINTEL(IdResultType resultType, IdResult resultId, IdRef payload);

        public void GenerateOpVariableLengthArrayINTEL(IdResultType resultType, IdResult resultId, IdRef lenght);

        public void GenerateOpSaveMemoryINTEL(IdResultType resultType, IdResult resultId);

        public void GenerateOpRestoreMemoryINTEL(IdRef ptr);

        public void GenerateOpArbitraryFloatSinCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger fromSign, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCastINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCastFromIntINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger mout, LiteralInteger fromSign, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCastToIntINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatAddINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatSubINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatMulINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatDivINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatGTINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2);

        public void GenerateOpArbitraryFloatGEINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2);

        public void GenerateOpArbitraryFloatLTINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2);

        public void GenerateOpArbitraryFloatLEINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2);

        public void GenerateOpArbitraryFloatEQINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2);

        public void GenerateOpArbitraryFloatRecipINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatRSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCbrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatHypotINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatLogINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatLog2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatLog10INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatLog1pINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatExpINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatExp2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatExp10INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatExpm1INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatSinINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatSinCosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatSinPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatASinINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatASinPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatACosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatACosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatATanINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatATanPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatATan2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatPowINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatPowRINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpArbitraryFloatPowNINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy);

        public void GenerateOpLoopControlINTEL(params LiteralInteger[] loopControlParameters);

        public void GenerateOpAliasDomainDeclINTEL(IdResult resultId, IdRef? name = null);

        public void GenerateOpAliasScopeDeclINTEL(IdResult resultId, IdRef aliasDomain, IdRef? name = null);

        public void GenerateOpAliasScopeListDeclINTEL(IdResult resultId, params IdRef[] aliasScope1AliasScope2);

        public void GenerateOpFixedSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedRecipINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedRsqrtINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedSinINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedCosINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedSinCosINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedSinPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedSinCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedLogINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpFixedExpINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o);

        public void GenerateOpPtrCastToCrossWorkgroupINTEL(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpCrossWorkgroupCastToPtrINTEL(IdResultType resultType, IdResult resultId, IdRef pointer);

        public void GenerateOpReadPipeBlockingINTEL(IdResultType resultType, IdResult resultId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpWritePipeBlockingINTEL(IdResultType resultType, IdResult resultId, IdRef packetSize, IdRef packetAlignment);

        public void GenerateOpFPGARegINTEL(IdResultType resultType, IdResult resultId, IdRef result, IdRef input);

        public void GenerateOpRayQueryGetRayTMinKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetRayFlagsKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetIntersectionTKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionInstanceCustomIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionInstanceIdKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionInstanceShaderBindingTableRecordOffsetKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionGeometryIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionPrimitiveIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionBarycentricsKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionFrontFaceKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionCandidateAABBOpaqueKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetIntersectionObjectRayDirectionKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionObjectRayOriginKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetWorldRayDirectionKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetWorldRayOriginKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery);

        public void GenerateOpRayQueryGetIntersectionObjectToWorldKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpRayQueryGetIntersectionWorldToObjectKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection);

        public void GenerateOpAtomicFAddEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value);

        public void GenerateOpTypeBufferSurfaceINTEL(IdResult resultId, AccessQualifier accessQualifier);

        public void GenerateOpTypeStructContinuedINTEL(params IdRef[] member0typemember1type);

        public void GenerateOpConstantCompositeContinuedINTEL(params IdRef[] constituents);

        public void GenerateOpSpecConstantCompositeContinuedINTEL(params IdRef[] constituents);

        public void GenerateOpControlBarrierArriveINTEL(IdScope execution, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpControlBarrierWaitINTEL(IdScope execution, IdScope memory, IdMemorySemantics semantics);

        public void GenerateOpGroupIMulKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupFMulKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupBitwiseAndKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupBitwiseOrKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupBitwiseXorKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupLogicalAndKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupLogicalOrKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

        public void GenerateOpGroupLogicalXorKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x);

   }
}

#nullable restore
