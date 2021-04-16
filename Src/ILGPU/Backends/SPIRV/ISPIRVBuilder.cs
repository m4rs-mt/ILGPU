using System;
using System.Collections.Generic;

#nullable enable
#pragma warning disable 1591

namespace ILGPU.Backends.SPIRV {

    /// <summary>
    /// Defines utility methods to generate SPIRV operations
    /// </summary>
    [CLSCompliant(false)]
    public interface ISPIRVBuilder {
    
        byte[] ToByteArray();
    
        void AddMetadata(uint magic, uint version, uint genMagic, uint bound, uint schema);
    
        public void GenerateOpNop();
        
        [CLSCompliant(false)]
        public void GenerateOpUndef(uint returnId, uint param1);
        
        public void GenerateOpSourceContinued(string ContinuedSource);
        
        [CLSCompliant(false)]
        public void GenerateOpSource(SourceLanguage param0, uint Version, uint? File = null, string? Source = null);
        
        public void GenerateOpSourceExtension(string Extension);
        
        [CLSCompliant(false)]
        public void GenerateOpName(uint Target, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberName(uint Type, uint Member, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpString(uint returnId, string String);
        
        [CLSCompliant(false)]
        public void GenerateOpLine(uint File, uint Line, uint Column);
        
        public void GenerateOpExtension(string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpExtInstImport(uint returnId, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpExtInst(uint returnId, uint param1, uint Set, uint Instruction, params uint[] Operand1Operand2);
        
        public void GenerateOpMemoryModel(AddressingModel param0, MemoryModel param1);
        
        [CLSCompliant(false)]
        public void GenerateOpEntryPoint(ExecutionModel param0, uint EntryPoint, string Name, params uint[] Interface);
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionMode(uint EntryPoint, ExecutionMode Mode);
        
        public void GenerateOpCapability(Capability Capability);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVoid(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBool(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeInt(uint returnId, uint Width, uint Signedness);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFloat(uint returnId, uint Width);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVector(uint returnId, uint ComponentType, uint ComponentCount);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeMatrix(uint returnId, uint ColumnType, uint ColumnCount);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeImage(uint returnId, uint SampledType, Dim param2, uint Depth, uint Arrayed, uint MS, uint Sampled, ImageFormat param7, AccessQualifier? param8 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampler(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampledImage(uint returnId, uint ImageType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeArray(uint returnId, uint ElementType, uint Length);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRuntimeArray(uint returnId, uint ElementType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStruct(uint returnId, params uint[] Member0typemember1type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeOpaque(uint returnId, string Thenameoftheopaquetype);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePointer(uint returnId, StorageClass param1, uint Type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFunction(uint returnId, uint ReturnType, params uint[] Parameter0TypeParameter1Type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeEvent(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeDeviceEvent(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeReserveId(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeQueue(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipe(uint returnId, AccessQualifier Qualifier);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeForwardPointer(uint PointerType, StorageClass param1);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantTrue(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantFalse(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpConstant(uint returnId, uint param1, double Value);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantComposite(uint returnId, uint param1, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantSampler(uint returnId, uint param1, SamplerAddressingMode param2, uint Param, SamplerFilterMode param4);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantNull(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantTrue(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantFalse(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstant(uint returnId, uint param1, double Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantComposite(uint returnId, uint param1, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantOp(uint returnId, uint param1, uint Opcode);
        
        [CLSCompliant(false)]
        public void GenerateOpFunction(uint returnId, uint param1, FunctionControl param2, uint FunctionType);
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionParameter(uint returnId, uint param1);
        
        public void GenerateOpFunctionEnd();
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionCall(uint returnId, uint param1, uint Function, params uint[] Argument0Argument1);
        
        [CLSCompliant(false)]
        public void GenerateOpVariable(uint returnId, uint param1, StorageClass param2, uint? Initializer = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageTexelPointer(uint returnId, uint param1, uint Image, uint Coordinate, uint Sample);
        
        [CLSCompliant(false)]
        public void GenerateOpLoad(uint returnId, uint param1, uint Pointer, MemoryAccess? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpStore(uint Pointer, uint Object, MemoryAccess? param2 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemory(uint Target, uint Source, MemoryAccess? param2 = null, MemoryAccess? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemorySized(uint Target, uint Source, uint Size, MemoryAccess? param3 = null, MemoryAccess? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpAccessChain(uint returnId, uint param1, uint Base, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsAccessChain(uint returnId, uint param1, uint Base, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrAccessChain(uint returnId, uint param1, uint Base, uint Element, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpArrayLength(uint returnId, uint param1, uint Structure, uint Arraymember);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericPtrMemSemantics(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsPtrAccessChain(uint returnId, uint param1, uint Base, uint Element, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorate(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorate(uint StructureType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorationGroup(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupDecorate(uint DecorationGroup, params uint[] Targets);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupMemberDecorate(uint DecorationGroup, params PairIdRefLiteralInteger[] Targets);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorExtractDynamic(uint returnId, uint param1, uint Vector, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorInsertDynamic(uint returnId, uint param1, uint Vector, uint Component, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorShuffle(uint returnId, uint param1, uint Vector1, uint Vector2, params uint[] Components);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeConstruct(uint returnId, uint param1, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeExtract(uint returnId, uint param1, uint Composite, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeInsert(uint returnId, uint param1, uint Object, uint Composite, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyObject(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpTranspose(uint returnId, uint param1, uint Matrix);
        
        [CLSCompliant(false)]
        public void GenerateOpSampledImage(uint returnId, uint param1, uint Image, uint Sampler);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageFetch(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageDrefGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageRead(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageWrite(uint Image, uint Coordinate, uint Texel, ImageOperands? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImage(uint returnId, uint param1, uint SampledImage);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryFormat(uint returnId, uint param1, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryOrder(uint returnId, uint param1, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySizeLod(uint returnId, uint param1, uint Image, uint LevelofDetail);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySize(uint returnId, uint param1, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLod(uint returnId, uint param1, uint SampledImage, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLevels(uint returnId, uint param1, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySamples(uint returnId, uint param1, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToU(uint returnId, uint param1, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToS(uint returnId, uint param1, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertSToF(uint returnId, uint param1, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToF(uint returnId, uint param1, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpUConvert(uint returnId, uint param1, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpSConvert(uint returnId, uint param1, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpFConvert(uint returnId, uint param1, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpQuantizeToF16(uint returnId, uint param1, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertPtrToU(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertSToU(uint returnId, uint param1, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertUToS(uint returnId, uint param1, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToPtr(uint returnId, uint param1, uint IntegerValue);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToGeneric(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtr(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtrExplicit(uint returnId, uint param1, uint Pointer, StorageClass Storage);
        
        [CLSCompliant(false)]
        public void GenerateOpBitcast(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpSNegate(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpFNegate(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpIAdd(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFAdd(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISub(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFSub(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIMul(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFMul(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUDiv(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSDiv(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFDiv(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMod(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSRem(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSMod(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFRem(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFMod(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesScalar(uint returnId, uint param1, uint Vector, uint Scalar);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesScalar(uint returnId, uint param1, uint Matrix, uint Scalar);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesMatrix(uint returnId, uint param1, uint Vector, uint Matrix);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesVector(uint returnId, uint param1, uint Matrix, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesMatrix(uint returnId, uint param1, uint LeftMatrix, uint RightMatrix);
        
        [CLSCompliant(false)]
        public void GenerateOpOuterProduct(uint returnId, uint param1, uint Vector1, uint Vector2);
        
        [CLSCompliant(false)]
        public void GenerateOpDot(uint returnId, uint param1, uint Vector1, uint Vector2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAddCarry(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISubBorrow(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMulExtended(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSMulExtended(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpAny(uint returnId, uint param1, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpAll(uint returnId, uint param1, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpIsNan(uint returnId, uint param1, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsInf(uint returnId, uint param1, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsFinite(uint returnId, uint param1, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsNormal(uint returnId, uint param1, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpSignBitSet(uint returnId, uint param1, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpLessOrGreater(uint returnId, uint param1, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpOrdered(uint returnId, uint param1, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpUnordered(uint returnId, uint param1, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalOr(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalAnd(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNot(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpSelect(uint returnId, uint param1, uint Condition, uint Object1, uint Object2);
        
        [CLSCompliant(false)]
        public void GenerateOpIEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpINotEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpULessThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpULessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightLogical(uint returnId, uint param1, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightArithmetic(uint returnId, uint param1, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftLeftLogical(uint returnId, uint param1, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseOr(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseXor(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseAnd(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpNot(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldInsert(uint returnId, uint param1, uint Base, uint Insert, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldSExtract(uint returnId, uint param1, uint Base, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldUExtract(uint returnId, uint param1, uint Base, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitReverse(uint returnId, uint param1, uint Base);
        
        [CLSCompliant(false)]
        public void GenerateOpBitCount(uint returnId, uint param1, uint Base);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdx(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdy(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidth(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxFine(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyFine(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthFine(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxCoarse(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyCoarse(uint returnId, uint param1, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthCoarse(uint returnId, uint param1, uint P);
        
        public void GenerateOpEmitVertex();
        
        public void GenerateOpEndPrimitive();
        
        [CLSCompliant(false)]
        public void GenerateOpEmitStreamVertex(uint Stream);
        
        [CLSCompliant(false)]
        public void GenerateOpEndStreamPrimitive(uint Stream);
        
        [CLSCompliant(false)]
        public void GenerateOpControlBarrier(uint Execution, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpMemoryBarrier(uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicLoad(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicStore(uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicExchange(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchange(uint returnId, uint param1, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchangeWeak(uint returnId, uint param1, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIIncrement(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIDecrement(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIAdd(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicISub(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMin(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMin(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMax(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMax(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicAnd(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicOr(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicXor(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpPhi(uint returnId, uint param1, params PairIdRefIdRef[] VariableParent);
        
        [CLSCompliant(false)]
        public void GenerateOpLoopMerge(uint MergeBlock, uint ContinueTarget, LoopControl param2);
        
        [CLSCompliant(false)]
        public void GenerateOpSelectionMerge(uint MergeBlock, SelectionControl param1);
        
        [CLSCompliant(false)]
        public void GenerateOpLabel(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpBranch(uint TargetLabel);
        
        [CLSCompliant(false)]
        public void GenerateOpBranchConditional(uint Condition, uint TrueLabel, uint FalseLabel, params uint[] Branchweights);
        
        [CLSCompliant(false)]
        public void GenerateOpSwitch(uint Selector, uint Default, params PairLiteralIntegerIdRef[] Target);
        
        public void GenerateOpKill();
        
        public void GenerateOpReturn();
        
        [CLSCompliant(false)]
        public void GenerateOpReturnValue(uint Value);
        
        public void GenerateOpUnreachable();
        
        [CLSCompliant(false)]
        public void GenerateOpLifetimeStart(uint Pointer, uint Size);
        
        [CLSCompliant(false)]
        public void GenerateOpLifetimeStop(uint Pointer, uint Size);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAsyncCopy(uint returnId, uint param1, uint Execution, uint Destination, uint Source, uint NumElements, uint Stride, uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupWaitEvents(uint Execution, uint NumEvents, uint EventsList);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAll(uint returnId, uint param1, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAny(uint returnId, uint param1, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint LocalId);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipe(uint returnId, uint param1, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipe(uint returnId, uint param1, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReservedReadPipe(uint returnId, uint param1, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReservedWritePipe(uint returnId, uint param1, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReserveReadPipePackets(uint returnId, uint param1, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReserveWritePipePackets(uint returnId, uint param1, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpCommitReadPipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpCommitWritePipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidReserveId(uint returnId, uint param1, uint ReserveId);
        
        [CLSCompliant(false)]
        public void GenerateOpGetNumPipePackets(uint returnId, uint param1, uint Pipe, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGetMaxPipePackets(uint returnId, uint param1, uint Pipe, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveReadPipePackets(uint returnId, uint param1, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveWritePipePackets(uint returnId, uint param1, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitReadPipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitWritePipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueMarker(uint returnId, uint param1, uint Queue, uint NumEvents, uint WaitEvents, uint RetEvent);
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueKernel(uint returnId, uint param1, uint Queue, uint Flags, uint NDRange, uint NumEvents, uint WaitEvents, uint RetEvent, uint Invoke, uint Param, uint ParamSize, uint ParamAlign, params uint[] LocalSize);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeSubGroupCount(uint returnId, uint param1, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeMaxSubGroupSize(uint returnId, uint param1, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelWorkGroupSize(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelPreferredWorkGroupSizeMultiple(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpRetainEvent(uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpReleaseEvent(uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpCreateUserEvent(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidEvent(uint returnId, uint param1, uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpSetUserEventStatus(uint Event, uint Status);
        
        [CLSCompliant(false)]
        public void GenerateOpCaptureEventProfilingInfo(uint Event, uint ProfilingInfo, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGetDefaultQueue(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpBuildNDRange(uint returnId, uint param1, uint GlobalWorkSize, uint LocalWorkSize, uint GlobalWorkOffset);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseFetch(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseDrefGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseTexelsResident(uint returnId, uint param1, uint ResidentCode);
        
        public void GenerateOpNoLine();
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagTestAndSet(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagClear(uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseRead(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpSizeOf(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipeStorage(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantPipeStorage(uint returnId, uint param1, uint PacketSize, uint PacketAlignment, uint Capacity);
        
        [CLSCompliant(false)]
        public void GenerateOpCreatePipeFromPipeStorage(uint returnId, uint param1, uint PipeStorage);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelLocalSizeForSubgroupCount(uint returnId, uint param1, uint SubgroupCount, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelMaxNumSubgroups(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeNamedBarrier(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpNamedBarrierInitialize(uint returnId, uint param1, uint SubgroupCount);
        
        [CLSCompliant(false)]
        public void GenerateOpMemoryNamedBarrier(uint NamedBarrier, uint Memory, uint Semantics);
        
        public void GenerateOpModuleProcessed(string Process);
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionModeId(uint EntryPoint, ExecutionMode Mode);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateId(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformElect(uint returnId, uint param1, uint Execution);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAll(uint returnId, uint param1, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAny(uint returnId, uint param1, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAllEqual(uint returnId, uint param1, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint Id);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcastFirst(uint returnId, uint param1, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallot(uint returnId, uint param1, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformInverseBallot(uint returnId, uint param1, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitExtract(uint returnId, uint param1, uint Execution, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitCount(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindLSB(uint returnId, uint param1, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindMSB(uint returnId, uint param1, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffle(uint returnId, uint param1, uint Execution, uint Value, uint Id);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleXor(uint returnId, uint param1, uint Execution, uint Value, uint Mask);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleUp(uint returnId, uint param1, uint Execution, uint Value, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleDown(uint returnId, uint param1, uint Execution, uint Value, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIMul(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMul(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseAnd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseOr(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseXor(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalAnd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalOr(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalXor(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadSwap(uint returnId, uint param1, uint Execution, uint Value, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyLogical(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrDiff(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        public void GenerateOpTerminateInvocation();
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBallotKHR(uint returnId, uint param1, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupFirstInvocationKHR(uint returnId, uint param1, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllKHR(uint returnId, uint param1, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAnyKHR(uint returnId, uint param1, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllEqualKHR(uint returnId, uint param1, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupReadInvocationKHR(uint returnId, uint param1, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpTraceRayKHR(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableKHR(uint SBTIndex, uint CallableData);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToAccelerationStructureKHR(uint returnId, uint param1, uint Accel);
        
        public void GenerateOpIgnoreIntersectionKHR();
        
        public void GenerateOpTerminateRayKHR();
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRayQueryKHR(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryInitializeKHR(uint RayQuery, uint Accel, uint RayFlags, uint CullMask, uint RayOrigin, uint RayTMin, uint RayDirection, uint RayTMax);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryTerminateKHR(uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGenerateIntersectionKHR(uint RayQuery, uint HitT);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryConfirmIntersectionKHR(uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryProceedKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTypeKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAddNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAddNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentMaskFetchAMD(uint returnId, uint param1, uint Image, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentFetchAMD(uint returnId, uint param1, uint Image, uint Coordinate, uint FragmentIndex);
        
        [CLSCompliant(false)]
        public void GenerateOpReadClockKHR(uint returnId, uint param1, uint Execution);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleFootprintNV(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Granularity, uint Coarse, ImageOperands? param6 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformPartitionNV(uint returnId, uint param1, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePackedPrimitiveIndices4x8NV(uint IndexOffset, uint PackedIndices);
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionNV(uint returnId, uint param1, uint Hit, uint HitKind);
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionKHR(uint returnId, uint param1, uint Hit, uint HitKind);
        
        public void GenerateOpIgnoreIntersectionNV();
        
        public void GenerateOpTerminateRayNV();
        
        [CLSCompliant(false)]
        public void GenerateOpTraceNV(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint PayloadId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureNV(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureKHR(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableNV(uint SBTIndex, uint CallableDataId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeCooperativeMatrixNV(uint returnId, uint ComponentType, uint Execution, uint Rows, uint Columns);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLoadNV(uint returnId, uint param1, uint Pointer, uint Stride, uint ColumnMajor, MemoryAccess? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixStoreNV(uint Pointer, uint Object, uint Stride, uint ColumnMajor, MemoryAccess? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixMulAddNV(uint returnId, uint param1, uint A, uint B, uint C);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLengthNV(uint returnId, uint param1, uint Type);
        
        public void GenerateOpBeginInvocationInterlockEXT();
        
        public void GenerateOpEndInvocationInterlockEXT();
        
        public void GenerateOpDemoteToHelperInvocationEXT();
        
        [CLSCompliant(false)]
        public void GenerateOpIsHelperInvocationEXT(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleINTEL(uint returnId, uint param1, uint Data, uint InvocationId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleDownINTEL(uint returnId, uint param1, uint Current, uint Next, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleUpINTEL(uint returnId, uint param1, uint Previous, uint Current, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleXorINTEL(uint returnId, uint param1, uint Data, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockReadINTEL(uint returnId, uint param1, uint Ptr);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockWriteINTEL(uint Ptr, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockReadINTEL(uint returnId, uint param1, uint Image, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockWriteINTEL(uint Image, uint Coordinate, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockReadINTEL(uint returnId, uint param1, uint Image, uint Coordinate, uint Width, uint Height);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockWriteINTEL(uint Image, uint Coordinate, uint Width, uint Height, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpUCountLeadingZerosINTEL(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpUCountTrailingZerosINTEL(uint returnId, uint param1, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpAbsISubINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpAbsUSubINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAddSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAddSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageRoundedINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageRoundedINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISubSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUSubSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIMul32x16INTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMul32x16INTEL(uint returnId, uint param1, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpConstFunctionPointerINTEL(uint returnId, uint param1, uint Function);
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionPointerCallINTEL(uint returnId, uint param1, params uint[] Operand1);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmTargetINTEL(uint returnId, uint param1, string Asmtarget);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmINTEL(uint returnId, uint param1, uint Asmtype, uint Target, string Asminstructions, string Constraints);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmCallINTEL(uint returnId, uint param1, uint Asm, params uint[] Argument0);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMinEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMaxEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateString(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateStringGOOGLE(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateString(uint StructType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateStringGOOGLE(uint StructType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpVmeImageINTEL(uint returnId, uint param1, uint ImageType, uint Sampler);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVmeImageINTEL(uint returnId, uint ImageType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImePayloadINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefPayloadINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicPayloadINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMcePayloadINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMceResultINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultSingleReferenceStreamoutINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultDualReferenceStreamoutINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeSingleReferenceStreaminINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeDualReferenceStreaminINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefResultINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicResultINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterBaseMultiReferencePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterBaseMultiReferencePenaltyINTEL(uint returnId, uint param1, uint ReferenceBasePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterShapePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterShapePenaltyINTEL(uint returnId, uint param1, uint PackedShapePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterDirectionPenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterDirectionPenaltyINTEL(uint returnId, uint param1, uint DirectionCost, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaShapePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterMotionVectorCostTableINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultHighPenaltyCostTableINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultMediumPenaltyCostTableINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultLowPenaltyCostTableINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetMotionVectorCostFunctionINTEL(uint returnId, uint param1, uint PackedCostCenterDelta, uint PackedCostTable, uint CostPrecision, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaModePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultNonDcLumaIntraPenaltyINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraChromaModeBasePenaltyINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetAcOnlyHaarINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSourceInterlacedFieldPolarityINTEL(uint returnId, uint param1, uint SourceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSingleReferenceInterlacedFieldPolarityINTEL(uint returnId, uint param1, uint ReferenceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetDualReferenceInterlacedFieldPolaritiesINTEL(uint returnId, uint param1, uint ForwardReferenceFieldPolarity, uint BackwardReferenceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImePayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImeResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefPayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicPayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetMotionVectorsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDistortionsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetBestInterDistortionsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMajorShapeINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMinorShapeINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDirectionsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMotionVectorCountINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceIdsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceInterlacedFieldPolaritiesINTEL(uint returnId, uint param1, uint PackedReferenceIds, uint PackedReferenceParameterFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint PartitionMask, uint SADAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetSingleReferenceINTEL(uint returnId, uint param1, uint RefOffset, uint SearchWindowConfig, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetDualReferenceINTEL(uint returnId, uint param1, uint FwdRefOffset, uint BwdRefOffset, uint idSearchWindowConfig, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeRefWindowSizeINTEL(uint returnId, uint param1, uint SearchWindowConfig, uint DualRef);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeAdjustRefOffsetINTEL(uint returnId, uint param1, uint RefOffset, uint SrcCoord, uint RefWindowSize, uint ImageSize);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetMaxMotionVectorCountINTEL(uint returnId, uint param1, uint MaxMotionVectorCount, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetUnidirectionalMixDisableINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetEarlySearchTerminationThresholdINTEL(uint returnId, uint param1, uint Threshold, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetWeightedSadINTEL(uint returnId, uint param1, uint PackedSadWeights, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreamoutINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreamoutINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminoutINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminoutINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMceResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetSingleReferenceStreaminINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetDualReferenceStreaminINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripSingleReferenceStreamoutINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripDualReferenceStreamoutINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeMotionVectorsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeDistortionsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeReferenceIdsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeMotionVectorsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeDistortionsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeReferenceIdsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetBorderReachedINTEL(uint returnId, uint param1, uint ImageSelect, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetTruncatedSearchIndicationINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetUnidirectionalEarlySearchTerminationINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumMotionVectorINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumDistortionINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcFmeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint SadAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcBmeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint BidirectionalWeight, uint SadAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBidirectionalMixDisableINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBilinearFilterEnableINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceInterlacedINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMceResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicInitializeINTEL(uint returnId, uint param1, uint SrcCoord);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureSkcINTEL(uint returnId, uint param1, uint SkipBlockPartitionType, uint SkipMotionVectorMask, uint MotionVectors, uint BidirectionalWeight, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaINTEL(uint returnId, uint param1, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaChromaINTEL(uint returnId, uint param1, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint LeftEdgeChromaPixels, uint UpperLeftCornerChromaPixel, uint UpperEdgeChromaPixels, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetMotionVectorMaskINTEL(uint returnId, uint param1, uint SkipBlockPartitionType, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaShapePenaltyINTEL(uint returnId, uint param1, uint PackedShapePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaModeCostFunctionINTEL(uint returnId, uint param1, uint LumaModePenalty, uint LumaPackedNeighborModes, uint LumaPackedNonDcPenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraChromaModeCostFunctionINTEL(uint returnId, uint param1, uint ChromaModeBasePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBilinearFilterEnableINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetSkcForwardTransformEnableINTEL(uint returnId, uint param1, uint PackedSadCoefficients, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBlockBasedRawSkipSadINTEL(uint returnId, uint param1, uint BlockBasedSkipType, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateIpeINTEL(uint returnId, uint param1, uint SrcImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceInterlacedINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMceResultINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeLumaShapeINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeLumaDistortionINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeChromaDistortionINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedIpeLumaModesINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeChromaModeINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaCountThresholdINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaSumThresholdINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetInterRawSadsINTEL(uint returnId, uint param1, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpVariableLengthArrayINTEL(uint returnId, uint param1, uint Lenght);
        
        [CLSCompliant(false)]
        public void GenerateOpSaveMemoryINTEL(uint returnId, uint param1);
        
        [CLSCompliant(false)]
        public void GenerateOpRestoreMemoryINTEL(uint Ptr);
        
        [CLSCompliant(false)]
        public void GenerateOpLoopControlINTEL(params uint[] LoopControlParameters);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToCrossWorkgroupINTEL(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpCrossWorkgroupCastToPtrINTEL(uint returnId, uint param1, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipeBlockingINTEL(uint returnId, uint param1, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipeBlockingINTEL(uint returnId, uint param1, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpFPGARegINTEL(uint returnId, uint param1, uint Result, uint Input);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayTMinKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayFlagsKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceCustomIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceIdKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceShaderBindingTableRecordOffsetKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionGeometryIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionPrimitiveIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionBarycentricsKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionFrontFaceKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionCandidateAABBOpaqueKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayDirectionKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayOriginKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayDirectionKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayOriginKHR(uint returnId, uint param1, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectToWorldKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionWorldToObjectKHR(uint returnId, uint param1, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFAddEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBufferSurfaceINTEL(uint returnId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStructContinuedINTEL(params uint[] Member0typemember1type);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantCompositeContinuedINTEL(params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantCompositeContinuedINTEL(params uint[] Constituents);
        
    }
}
#pragma warning restore 1591