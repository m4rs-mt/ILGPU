using System;
using System.Collections.Generic;

#nullable enable
#pragma warning disable 1591

namespace ILGPU.Backends.SPIRV {

    /// <summary>
    /// Defines utility methods to generate SPIRV operations
    /// </summary>
    [CLSCompliant(false)]
    public interface ISPIRVBuilder
    {
    
        byte[] ToByteArray();
    
        void AddMetadata(uint magic, uint version, uint genMagic, uint bound, uint schema);
    
        // This is the best way I could come up with to
        // handle trying to merge different builders
        // Implementing classes will kinda just have to
        // deal with it
        void Merge(ISPIRVBuilder other);
    
        public void GenerateOpNop();
        
        [CLSCompliant(false)]
        public void GenerateOpUndef(uint resultType, uint resultId);
        
        public void GenerateOpSourceContinued(string ContinuedSource);
        
        [CLSCompliant(false)]
        public void GenerateOpSource(SourceLanguage param0, uint Version, uint? File = null, string? Source = null);
        
        public void GenerateOpSourceExtension(string Extension);
        
        [CLSCompliant(false)]
        public void GenerateOpName(uint Target, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberName(uint Type, uint Member, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpString(uint resultId, string String);
        
        [CLSCompliant(false)]
        public void GenerateOpLine(uint File, uint Line, uint Column);
        
        public void GenerateOpExtension(string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpExtInstImport(uint resultId, string Name);
        
        [CLSCompliant(false)]
        public void GenerateOpExtInst(uint resultType, uint resultId, uint Set, uint Instruction, params uint[] Operand1Operand2);
        
        public void GenerateOpMemoryModel(AddressingModel param0, MemoryModel param1);
        
        [CLSCompliant(false)]
        public void GenerateOpEntryPoint(ExecutionModel param0, uint EntryPoint, string Name, params uint[] Interface);
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionMode(uint EntryPoint, ExecutionMode Mode);
        
        public void GenerateOpCapability(Capability Capability);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVoid(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBool(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeInt(uint resultId, uint Width, uint Signedness);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFloat(uint resultId, uint Width);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVector(uint resultId, uint ComponentType, uint ComponentCount);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeMatrix(uint resultId, uint ColumnType, uint ColumnCount);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeImage(uint resultId, uint SampledType, Dim param2, uint Depth, uint Arrayed, uint MS, uint Sampled, ImageFormat param7, AccessQualifier? param8 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampler(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampledImage(uint resultId, uint ImageType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeArray(uint resultId, uint ElementType, uint Length);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRuntimeArray(uint resultId, uint ElementType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStruct(uint resultId, params uint[] Member0typemember1type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeOpaque(uint resultId, string Thenameoftheopaquetype);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePointer(uint resultId, StorageClass param1, uint Type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFunction(uint resultId, uint ReturnType, params uint[] Parameter0TypeParameter1Type);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeEvent(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeDeviceEvent(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeReserveId(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeQueue(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipe(uint resultId, AccessQualifier Qualifier);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeForwardPointer(uint PointerType, StorageClass param1);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantTrue(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantFalse(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpConstant(uint resultType, uint resultId, double Value);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantComposite(uint resultType, uint resultId, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantSampler(uint resultType, uint resultId, SamplerAddressingMode param2, uint Param, SamplerFilterMode param4);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantNull(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantTrue(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantFalse(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstant(uint resultType, uint resultId, double Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantComposite(uint resultType, uint resultId, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantOp(uint resultType, uint resultId, uint Opcode);
        
        [CLSCompliant(false)]
        public void GenerateOpFunction(uint resultType, uint resultId, FunctionControl param2, uint FunctionType);
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionParameter(uint resultType, uint resultId);
        
        public void GenerateOpFunctionEnd();
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionCall(uint resultType, uint resultId, uint Function, params uint[] Argument0Argument1);
        
        [CLSCompliant(false)]
        public void GenerateOpVariable(uint resultType, uint resultId, StorageClass param2, uint? Initializer = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageTexelPointer(uint resultType, uint resultId, uint Image, uint Coordinate, uint Sample);
        
        [CLSCompliant(false)]
        public void GenerateOpLoad(uint resultType, uint resultId, uint Pointer, MemoryAccess? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpStore(uint Pointer, uint Object, MemoryAccess? param2 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemory(uint Target, uint Source, MemoryAccess? param2 = null, MemoryAccess? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemorySized(uint Target, uint Source, uint Size, MemoryAccess? param3 = null, MemoryAccess? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpAccessChain(uint resultType, uint resultId, uint Base, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsAccessChain(uint resultType, uint resultId, uint Base, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrAccessChain(uint resultType, uint resultId, uint Base, uint Element, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpArrayLength(uint resultType, uint resultId, uint Structure, uint Arraymember);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericPtrMemSemantics(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsPtrAccessChain(uint resultType, uint resultId, uint Base, uint Element, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorate(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorate(uint StructureType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorationGroup(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupDecorate(uint DecorationGroup, params uint[] Targets);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupMemberDecorate(uint DecorationGroup, params PairIdRefLiteralInteger[] Targets);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorExtractDynamic(uint resultType, uint resultId, uint Vector, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorInsertDynamic(uint resultType, uint resultId, uint Vector, uint Component, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorShuffle(uint resultType, uint resultId, uint Vector1, uint Vector2, params uint[] Components);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeConstruct(uint resultType, uint resultId, params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeExtract(uint resultType, uint resultId, uint Composite, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeInsert(uint resultType, uint resultId, uint Object, uint Composite, params uint[] Indexes);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyObject(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpTranspose(uint resultType, uint resultId, uint Matrix);
        
        [CLSCompliant(false)]
        public void GenerateOpSampledImage(uint resultType, uint resultId, uint Image, uint Sampler);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageFetch(uint resultType, uint resultId, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageGather(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageDrefGather(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageRead(uint resultType, uint resultId, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageWrite(uint Image, uint Coordinate, uint Texel, ImageOperands? param3 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImage(uint resultType, uint resultId, uint SampledImage);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryFormat(uint resultType, uint resultId, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryOrder(uint resultType, uint resultId, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySizeLod(uint resultType, uint resultId, uint Image, uint LevelofDetail);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySize(uint resultType, uint resultId, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLevels(uint resultType, uint resultId, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySamples(uint resultType, uint resultId, uint Image);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToU(uint resultType, uint resultId, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToS(uint resultType, uint resultId, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertSToF(uint resultType, uint resultId, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToF(uint resultType, uint resultId, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpUConvert(uint resultType, uint resultId, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpSConvert(uint resultType, uint resultId, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpFConvert(uint resultType, uint resultId, uint FloatValue);
        
        [CLSCompliant(false)]
        public void GenerateOpQuantizeToF16(uint resultType, uint resultId, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertPtrToU(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertSToU(uint resultType, uint resultId, uint SignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertUToS(uint resultType, uint resultId, uint UnsignedValue);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToPtr(uint resultType, uint resultId, uint IntegerValue);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToGeneric(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtr(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtrExplicit(uint resultType, uint resultId, uint Pointer, StorageClass Storage);
        
        [CLSCompliant(false)]
        public void GenerateOpBitcast(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpSNegate(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpFNegate(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpIAdd(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFAdd(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISub(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFSub(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIMul(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFMul(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUDiv(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSDiv(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFDiv(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMod(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSRem(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSMod(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFRem(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFMod(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesScalar(uint resultType, uint resultId, uint Vector, uint Scalar);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesScalar(uint resultType, uint resultId, uint Matrix, uint Scalar);
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesMatrix(uint resultType, uint resultId, uint Vector, uint Matrix);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesVector(uint resultType, uint resultId, uint Matrix, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesMatrix(uint resultType, uint resultId, uint LeftMatrix, uint RightMatrix);
        
        [CLSCompliant(false)]
        public void GenerateOpOuterProduct(uint resultType, uint resultId, uint Vector1, uint Vector2);
        
        [CLSCompliant(false)]
        public void GenerateOpDot(uint resultType, uint resultId, uint Vector1, uint Vector2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAddCarry(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISubBorrow(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMulExtended(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSMulExtended(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpAny(uint resultType, uint resultId, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpAll(uint resultType, uint resultId, uint Vector);
        
        [CLSCompliant(false)]
        public void GenerateOpIsNan(uint resultType, uint resultId, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsInf(uint resultType, uint resultId, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsFinite(uint resultType, uint resultId, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpIsNormal(uint resultType, uint resultId, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpSignBitSet(uint resultType, uint resultId, uint x);
        
        [CLSCompliant(false)]
        public void GenerateOpLessOrGreater(uint resultType, uint resultId, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpOrdered(uint resultType, uint resultId, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpUnordered(uint resultType, uint resultId, uint x, uint y);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNotEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalOr(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalAnd(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNot(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpSelect(uint resultType, uint resultId, uint Condition, uint Object1, uint Object2);
        
        [CLSCompliant(false)]
        public void GenerateOpIEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpINotEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpULessThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpULessThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdNotEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordNotEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThan(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThanEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightLogical(uint resultType, uint resultId, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightArithmetic(uint resultType, uint resultId, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpShiftLeftLogical(uint resultType, uint resultId, uint Base, uint Shift);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseOr(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseXor(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseAnd(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpNot(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldInsert(uint resultType, uint resultId, uint Base, uint Insert, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldSExtract(uint resultType, uint resultId, uint Base, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldUExtract(uint resultType, uint resultId, uint Base, uint Offset, uint Count);
        
        [CLSCompliant(false)]
        public void GenerateOpBitReverse(uint resultType, uint resultId, uint Base);
        
        [CLSCompliant(false)]
        public void GenerateOpBitCount(uint resultType, uint resultId, uint Base);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdx(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdy(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidth(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxFine(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyFine(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthFine(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxCoarse(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyCoarse(uint resultType, uint resultId, uint P);
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthCoarse(uint resultType, uint resultId, uint P);
        
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
        public void GenerateOpAtomicLoad(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicStore(uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicExchange(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchange(uint resultType, uint resultId, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchangeWeak(uint resultType, uint resultId, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIIncrement(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIDecrement(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIAdd(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicISub(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMin(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMin(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMax(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMax(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicAnd(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicOr(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicXor(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpPhi(uint resultType, uint resultId, params PairIdRefIdRef[] VariableParent);
        
        [CLSCompliant(false)]
        public void GenerateOpLoopMerge(uint MergeBlock, uint ContinueTarget, LoopControl param2);
        
        [CLSCompliant(false)]
        public void GenerateOpSelectionMerge(uint MergeBlock, SelectionControl param1);
        
        [CLSCompliant(false)]
        public void GenerateOpLabel(uint resultId);
        
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
        public void GenerateOpGroupAsyncCopy(uint resultType, uint resultId, uint Execution, uint Destination, uint Source, uint NumElements, uint Stride, uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupWaitEvents(uint Execution, uint NumEvents, uint EventsList);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAll(uint resultType, uint resultId, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAny(uint resultType, uint resultId, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupBroadcast(uint resultType, uint resultId, uint Execution, uint Value, uint LocalId);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAdd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAdd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipe(uint resultType, uint resultId, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipe(uint resultType, uint resultId, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReservedReadPipe(uint resultType, uint resultId, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReservedWritePipe(uint resultType, uint resultId, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReserveReadPipePackets(uint resultType, uint resultId, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpReserveWritePipePackets(uint resultType, uint resultId, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpCommitReadPipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpCommitWritePipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidReserveId(uint resultType, uint resultId, uint ReserveId);
        
        [CLSCompliant(false)]
        public void GenerateOpGetNumPipePackets(uint resultType, uint resultId, uint Pipe, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGetMaxPipePackets(uint resultType, uint resultId, uint Pipe, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveReadPipePackets(uint resultType, uint resultId, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveWritePipePackets(uint resultType, uint resultId, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitReadPipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitWritePipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueMarker(uint resultType, uint resultId, uint Queue, uint NumEvents, uint WaitEvents, uint RetEvent);
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueKernel(uint resultType, uint resultId, uint Queue, uint Flags, uint NDRange, uint NumEvents, uint WaitEvents, uint RetEvent, uint Invoke, uint Param, uint ParamSize, uint ParamAlign, params uint[] LocalSize);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeSubGroupCount(uint resultType, uint resultId, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeMaxSubGroupSize(uint resultType, uint resultId, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelWorkGroupSize(uint resultType, uint resultId, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelPreferredWorkGroupSizeMultiple(uint resultType, uint resultId, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpRetainEvent(uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpReleaseEvent(uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpCreateUserEvent(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidEvent(uint resultType, uint resultId, uint Event);
        
        [CLSCompliant(false)]
        public void GenerateOpSetUserEventStatus(uint Event, uint Status);
        
        [CLSCompliant(false)]
        public void GenerateOpCaptureEventProfilingInfo(uint Event, uint ProfilingInfo, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGetDefaultQueue(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpBuildNDRange(uint resultType, uint resultId, uint GlobalWorkSize, uint LocalWorkSize, uint GlobalWorkOffset);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, ImageOperands param4);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefImplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefExplicitLod(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands param5);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseFetch(uint resultType, uint resultId, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseGather(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseDrefGather(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseTexelsResident(uint resultType, uint resultId, uint ResidentCode);
        
        public void GenerateOpNoLine();
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagTestAndSet(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagClear(uint Pointer, uint Memory, uint Semantics);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseRead(uint resultType, uint resultId, uint Image, uint Coordinate, ImageOperands? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpSizeOf(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipeStorage(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantPipeStorage(uint resultType, uint resultId, uint PacketSize, uint PacketAlignment, uint Capacity);
        
        [CLSCompliant(false)]
        public void GenerateOpCreatePipeFromPipeStorage(uint resultType, uint resultId, uint PipeStorage);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelLocalSizeForSubgroupCount(uint resultType, uint resultId, uint SubgroupCount, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelMaxNumSubgroups(uint resultType, uint resultId, uint Invoke, uint Param, uint ParamSize, uint ParamAlign);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeNamedBarrier(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpNamedBarrierInitialize(uint resultType, uint resultId, uint SubgroupCount);
        
        [CLSCompliant(false)]
        public void GenerateOpMemoryNamedBarrier(uint NamedBarrier, uint Memory, uint Semantics);
        
        public void GenerateOpModuleProcessed(string Process);
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionModeId(uint EntryPoint, ExecutionMode Mode);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateId(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformElect(uint resultType, uint resultId, uint Execution);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAll(uint resultType, uint resultId, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAny(uint resultType, uint resultId, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAllEqual(uint resultType, uint resultId, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcast(uint resultType, uint resultId, uint Execution, uint Value, uint Id);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcastFirst(uint resultType, uint resultId, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallot(uint resultType, uint resultId, uint Execution, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformInverseBallot(uint resultType, uint resultId, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitExtract(uint resultType, uint resultId, uint Execution, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitCount(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindLSB(uint resultType, uint resultId, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindMSB(uint resultType, uint resultId, uint Execution, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffle(uint resultType, uint resultId, uint Execution, uint Value, uint Id);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleXor(uint resultType, uint resultId, uint Execution, uint Value, uint Mask);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleUp(uint resultType, uint resultId, uint Execution, uint Value, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleDown(uint resultType, uint resultId, uint Execution, uint Value, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIAdd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFAdd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIMul(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMul(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMin(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMax(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseAnd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseOr(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseXor(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalAnd(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalOr(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalXor(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadBroadcast(uint resultType, uint resultId, uint Execution, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadSwap(uint resultType, uint resultId, uint Execution, uint Value, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpCopyLogical(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrNotEqual(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrDiff(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        public void GenerateOpTerminateInvocation();
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBallotKHR(uint resultType, uint resultId, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupFirstInvocationKHR(uint resultType, uint resultId, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllKHR(uint resultType, uint resultId, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAnyKHR(uint resultType, uint resultId, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllEqualKHR(uint resultType, uint resultId, uint Predicate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupReadInvocationKHR(uint resultType, uint resultId, uint Value, uint Index);
        
        [CLSCompliant(false)]
        public void GenerateOpTraceRayKHR(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableKHR(uint SBTIndex, uint CallableData);
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToAccelerationStructureKHR(uint resultType, uint resultId, uint Accel);
        
        public void GenerateOpIgnoreIntersectionKHR();
        
        public void GenerateOpTerminateRayKHR();
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRayQueryKHR(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryInitializeKHR(uint RayQuery, uint Accel, uint RayFlags, uint CullMask, uint RayOrigin, uint RayTMin, uint RayDirection, uint RayTMax);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryTerminateKHR(uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGenerateIntersectionKHR(uint RayQuery, uint HitT);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryConfirmIntersectionKHR(uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryProceedKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTypeKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAddNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAddNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMinNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMinNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMinNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMaxNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMaxNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMaxNonUniformAMD(uint resultType, uint resultId, uint Execution, GroupOperation Operation, uint X);
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentMaskFetchAMD(uint resultType, uint resultId, uint Image, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentFetchAMD(uint resultType, uint resultId, uint Image, uint Coordinate, uint FragmentIndex);
        
        [CLSCompliant(false)]
        public void GenerateOpReadClockKHR(uint resultType, uint resultId, uint Execution);
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleFootprintNV(uint resultType, uint resultId, uint SampledImage, uint Coordinate, uint Granularity, uint Coarse, ImageOperands? param6 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformPartitionNV(uint resultType, uint resultId, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePackedPrimitiveIndices4x8NV(uint IndexOffset, uint PackedIndices);
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionNV(uint resultType, uint resultId, uint Hit, uint HitKind);
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionKHR(uint resultType, uint resultId, uint Hit, uint HitKind);
        
        public void GenerateOpIgnoreIntersectionNV();
        
        public void GenerateOpTerminateRayNV();
        
        [CLSCompliant(false)]
        public void GenerateOpTraceNV(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint PayloadId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureNV(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureKHR(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableNV(uint SBTIndex, uint CallableDataId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeCooperativeMatrixNV(uint resultId, uint ComponentType, uint Execution, uint Rows, uint Columns);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLoadNV(uint resultType, uint resultId, uint Pointer, uint Stride, uint ColumnMajor, MemoryAccess? param5 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixStoreNV(uint Pointer, uint Object, uint Stride, uint ColumnMajor, MemoryAccess? param4 = null);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixMulAddNV(uint resultType, uint resultId, uint A, uint B, uint C);
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLengthNV(uint resultType, uint resultId, uint Type);
        
        public void GenerateOpBeginInvocationInterlockEXT();
        
        public void GenerateOpEndInvocationInterlockEXT();
        
        public void GenerateOpDemoteToHelperInvocationEXT();
        
        [CLSCompliant(false)]
        public void GenerateOpIsHelperInvocationEXT(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleINTEL(uint resultType, uint resultId, uint Data, uint InvocationId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleDownINTEL(uint resultType, uint resultId, uint Current, uint Next, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleUpINTEL(uint resultType, uint resultId, uint Previous, uint Current, uint Delta);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleXorINTEL(uint resultType, uint resultId, uint Data, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockReadINTEL(uint resultType, uint resultId, uint Ptr);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockWriteINTEL(uint Ptr, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockReadINTEL(uint resultType, uint resultId, uint Image, uint Coordinate);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockWriteINTEL(uint Image, uint Coordinate, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockReadINTEL(uint resultType, uint resultId, uint Image, uint Coordinate, uint Width, uint Height);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockWriteINTEL(uint Image, uint Coordinate, uint Width, uint Height, uint Data);
        
        [CLSCompliant(false)]
        public void GenerateOpUCountLeadingZerosINTEL(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpUCountTrailingZerosINTEL(uint resultType, uint resultId, uint Operand);
        
        [CLSCompliant(false)]
        public void GenerateOpAbsISubINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpAbsUSubINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAddSatINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAddSatINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageRoundedINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageRoundedINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpISubSatINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUSubSatINTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpIMul32x16INTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpUMul32x16INTEL(uint resultType, uint resultId, uint Operand1, uint Operand2);
        
        [CLSCompliant(false)]
        public void GenerateOpConstFunctionPointerINTEL(uint resultType, uint resultId, uint Function);
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionPointerCallINTEL(uint resultType, uint resultId, params uint[] Operand1);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmTargetINTEL(uint resultType, uint resultId, string Asmtarget);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmINTEL(uint resultType, uint resultId, uint Asmtype, uint Target, string Asminstructions, string Constraints);
        
        [CLSCompliant(false)]
        public void GenerateOpAsmCallINTEL(uint resultType, uint resultId, uint Asm, params uint[] Argument0);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMinEXT(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMaxEXT(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateString(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateStringGOOGLE(uint Target, Decoration param1);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateString(uint StructType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateStringGOOGLE(uint StructType, uint Member, Decoration param2);
        
        [CLSCompliant(false)]
        public void GenerateOpVmeImageINTEL(uint resultType, uint resultId, uint ImageType, uint Sampler);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVmeImageINTEL(uint resultId, uint ImageType);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImePayloadINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefPayloadINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicPayloadINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMcePayloadINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMceResultINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultSingleReferenceStreamoutINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultDualReferenceStreamoutINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeSingleReferenceStreaminINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeDualReferenceStreaminINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefResultINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicResultINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterBaseMultiReferencePenaltyINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterBaseMultiReferencePenaltyINTEL(uint resultType, uint resultId, uint ReferenceBasePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterShapePenaltyINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterShapePenaltyINTEL(uint resultType, uint resultId, uint PackedShapePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterDirectionPenaltyINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterDirectionPenaltyINTEL(uint resultType, uint resultId, uint DirectionCost, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaShapePenaltyINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterMotionVectorCostTableINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultHighPenaltyCostTableINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultMediumPenaltyCostTableINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultLowPenaltyCostTableINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetMotionVectorCostFunctionINTEL(uint resultType, uint resultId, uint PackedCostCenterDelta, uint PackedCostTable, uint CostPrecision, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaModePenaltyINTEL(uint resultType, uint resultId, uint SliceType, uint Qp);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultNonDcLumaIntraPenaltyINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraChromaModeBasePenaltyINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetAcOnlyHaarINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSourceInterlacedFieldPolarityINTEL(uint resultType, uint resultId, uint SourceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSingleReferenceInterlacedFieldPolarityINTEL(uint resultType, uint resultId, uint ReferenceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetDualReferenceInterlacedFieldPolaritiesINTEL(uint resultType, uint resultId, uint ForwardReferenceFieldPolarity, uint BackwardReferenceFieldPolarity, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImePayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImeResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefPayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicPayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetMotionVectorsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDistortionsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetBestInterDistortionsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMajorShapeINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMinorShapeINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDirectionsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMotionVectorCountINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceIdsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceInterlacedFieldPolaritiesINTEL(uint resultType, uint resultId, uint PackedReferenceIds, uint PackedReferenceParameterFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeInitializeINTEL(uint resultType, uint resultId, uint SrcCoord, uint PartitionMask, uint SADAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetSingleReferenceINTEL(uint resultType, uint resultId, uint RefOffset, uint SearchWindowConfig, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetDualReferenceINTEL(uint resultType, uint resultId, uint FwdRefOffset, uint BwdRefOffset, uint idSearchWindowConfig, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeRefWindowSizeINTEL(uint resultType, uint resultId, uint SearchWindowConfig, uint DualRef);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeAdjustRefOffsetINTEL(uint resultType, uint resultId, uint RefOffset, uint SrcCoord, uint RefWindowSize, uint ImageSize);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMcePayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetMaxMotionVectorCountINTEL(uint resultType, uint resultId, uint MaxMotionVectorCount, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetUnidirectionalMixDisableINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetEarlySearchTerminationThresholdINTEL(uint resultType, uint resultId, uint Threshold, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetWeightedSadINTEL(uint resultType, uint resultId, uint PackedSadWeights, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreamoutINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreamoutINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminoutINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminoutINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMceResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetSingleReferenceStreaminINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetDualReferenceStreaminINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripSingleReferenceStreamoutINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripDualReferenceStreamoutINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeMotionVectorsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeDistortionsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeReferenceIdsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeMotionVectorsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeDistortionsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeReferenceIdsINTEL(uint resultType, uint resultId, uint Payload, uint MajorShape, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetBorderReachedINTEL(uint resultType, uint resultId, uint ImageSelect, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetTruncatedSearchIndicationINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetUnidirectionalEarlySearchTerminationINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumMotionVectorINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumDistortionINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcFmeInitializeINTEL(uint resultType, uint resultId, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint SadAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcBmeInitializeINTEL(uint resultType, uint resultId, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint BidirectionalWeight, uint SadAdjustment);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMcePayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBidirectionalMixDisableINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBilinearFilterEnableINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithSingleReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithDualReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint PackedReferenceIds, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceInterlacedINTEL(uint resultType, uint resultId, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMceResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicInitializeINTEL(uint resultType, uint resultId, uint SrcCoord);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureSkcINTEL(uint resultType, uint resultId, uint SkipBlockPartitionType, uint SkipMotionVectorMask, uint MotionVectors, uint BidirectionalWeight, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaINTEL(uint resultType, uint resultId, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaChromaINTEL(uint resultType, uint resultId, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint LeftEdgeChromaPixels, uint UpperLeftCornerChromaPixel, uint UpperEdgeChromaPixels, uint SadAdjustment, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetMotionVectorMaskINTEL(uint resultType, uint resultId, uint SkipBlockPartitionType, uint Direction);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMcePayloadINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaShapePenaltyINTEL(uint resultType, uint resultId, uint PackedShapePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaModeCostFunctionINTEL(uint resultType, uint resultId, uint LumaModePenalty, uint LumaPackedNeighborModes, uint LumaPackedNonDcPenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraChromaModeCostFunctionINTEL(uint resultType, uint resultId, uint ChromaModeBasePenalty, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBilinearFilterEnableINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetSkcForwardTransformEnableINTEL(uint resultType, uint resultId, uint PackedSadCoefficients, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBlockBasedRawSkipSadINTEL(uint resultType, uint resultId, uint BlockBasedSkipType, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateIpeINTEL(uint resultType, uint resultId, uint SrcImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithSingleReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint RefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithDualReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceINTEL(uint resultType, uint resultId, uint SrcImage, uint PackedReferenceIds, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceInterlacedINTEL(uint resultType, uint resultId, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMceResultINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeLumaShapeINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeLumaDistortionINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeChromaDistortionINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedIpeLumaModesINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeChromaModeINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaCountThresholdINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaSumThresholdINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetInterRawSadsINTEL(uint resultType, uint resultId, uint Payload);
        
        [CLSCompliant(false)]
        public void GenerateOpVariableLengthArrayINTEL(uint resultType, uint resultId, uint Lenght);
        
        [CLSCompliant(false)]
        public void GenerateOpSaveMemoryINTEL(uint resultType, uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpRestoreMemoryINTEL(uint Ptr);
        
        [CLSCompliant(false)]
        public void GenerateOpLoopControlINTEL(params uint[] LoopControlParameters);
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToCrossWorkgroupINTEL(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpCrossWorkgroupCastToPtrINTEL(uint resultType, uint resultId, uint Pointer);
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipeBlockingINTEL(uint resultType, uint resultId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipeBlockingINTEL(uint resultType, uint resultId, uint PacketSize, uint PacketAlignment);
        
        [CLSCompliant(false)]
        public void GenerateOpFPGARegINTEL(uint resultType, uint resultId, uint Result, uint Input);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayTMinKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayFlagsKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceCustomIndexKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceIdKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceShaderBindingTableRecordOffsetKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionGeometryIndexKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionPrimitiveIndexKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionBarycentricsKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionFrontFaceKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionCandidateAABBOpaqueKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayDirectionKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayOriginKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayDirectionKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayOriginKHR(uint resultType, uint resultId, uint RayQuery);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectToWorldKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionWorldToObjectKHR(uint resultType, uint resultId, uint RayQuery, uint Intersection);
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFAddEXT(uint resultType, uint resultId, uint Pointer, uint Memory, uint Semantics, uint Value);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBufferSurfaceINTEL(uint resultId);
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStructContinuedINTEL(params uint[] Member0typemember1type);
        
        [CLSCompliant(false)]
        public void GenerateOpConstantCompositeContinuedINTEL(params uint[] Constituents);
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantCompositeContinuedINTEL(params uint[] Constituents);
        
    }
}
#pragma warning restore 1591