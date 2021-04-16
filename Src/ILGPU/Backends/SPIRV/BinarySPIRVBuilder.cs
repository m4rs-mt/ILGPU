using System;
using System.Collections.Generic;

#nullable enable
#pragma warning disable 1591

namespace ILGPU.Backends.SPIRV {

    /// <summary>
    /// Defines utility methods to generate SPIRV operations
    /// </summary>
    [CLSCompliant(false)]
    public class BinarySPIRVBuilder : ISPIRVBuilder {
    
        private List<uint> _instructions = new List<uint>();
    
        public byte[] ToByteArray() {
            uint[] uintArray = _instructions.ToArray();
            byte[] byteArray = new byte[uintArray.Length * 4];
            Buffer.BlockCopy(uintArray, 0, byteArray, 0, uintArray.Length * 4);
            return byteArray;
        }
    
        public void AddMetadata(
            uint magic,
            uint version,
            uint genMagic,
            uint bound,
            uint schema)
        {
            _instructions.Add(magic);
            _instructions.Add(version);
            _instructions.Add(genMagic);
            _instructions.Add(bound);
            _instructions.Add(schema);
        }
    
        public void GenerateOpNop() {
            ushort opCode = 0;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUndef(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 1;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpSourceContinued(string ContinuedSource) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ContinuedSource));
            ushort opCode = 2;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSource(SourceLanguage param0, uint Version, uint? File = null, string? Source = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param0));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Version));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(File));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Source));
            ushort opCode = 3;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpSourceExtension(string Extension) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Extension));
            ushort opCode = 4;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpName(uint Target, string Name) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Name));
            ushort opCode = 5;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemberName(uint Type, uint Member, string Name) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Type));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Name));
            ushort opCode = 6;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpString(uint returnId, string String) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(String));
            ushort opCode = 7;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLine(uint File, uint Line, uint Column) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(File));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Line));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Column));
            ushort opCode = 8;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpExtension(string Name) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Name));
            ushort opCode = 10;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExtInstImport(uint returnId, string Name) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Name));
            ushort opCode = 11;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExtInst(uint returnId, uint param1, uint Set, uint Instruction, params uint[] Operand1Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Set));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Instruction));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1Operand2));
            ushort opCode = 12;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpMemoryModel(AddressingModel param0, MemoryModel param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param0));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 14;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpEntryPoint(ExecutionModel param0, uint EntryPoint, string Name, params uint[] Interface) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param0));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(EntryPoint));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Name));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Interface));
            ushort opCode = 15;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionMode(uint EntryPoint, ExecutionMode Mode) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(EntryPoint));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Mode));
            ushort opCode = 16;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpCapability(Capability Capability) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Capability));
            ushort opCode = 17;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVoid(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 19;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBool(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 20;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeInt(uint returnId, uint Width, uint Signedness) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Width));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Signedness));
            ushort opCode = 21;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFloat(uint returnId, uint Width) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Width));
            ushort opCode = 22;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVector(uint returnId, uint ComponentType, uint ComponentCount) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ComponentType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ComponentCount));
            ushort opCode = 23;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeMatrix(uint returnId, uint ColumnType, uint ColumnCount) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ColumnType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ColumnCount));
            ushort opCode = 24;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeImage(uint returnId, uint SampledType, Dim param2, uint Depth, uint Arrayed, uint MS, uint Sampled, ImageFormat param7, AccessQualifier? param8 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Depth));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Arrayed));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MS));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Sampled));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param7));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param8));
            ushort opCode = 25;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampler(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 26;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeSampledImage(uint returnId, uint ImageType) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ImageType));
            ushort opCode = 27;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeArray(uint returnId, uint ElementType, uint Length) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ElementType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Length));
            ushort opCode = 28;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRuntimeArray(uint returnId, uint ElementType) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ElementType));
            ushort opCode = 29;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStruct(uint returnId, params uint[] Member0typemember1type) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member0typemember1type));
            ushort opCode = 30;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeOpaque(uint returnId, string Thenameoftheopaquetype) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Thenameoftheopaquetype));
            ushort opCode = 31;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypePointer(uint returnId, StorageClass param1, uint Type) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Type));
            ushort opCode = 32;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeFunction(uint returnId, uint ReturnType, params uint[] Parameter0TypeParameter1Type) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReturnType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Parameter0TypeParameter1Type));
            ushort opCode = 33;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeEvent(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 34;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeDeviceEvent(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 35;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeReserveId(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 36;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeQueue(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 37;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipe(uint returnId, AccessQualifier Qualifier) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qualifier));
            ushort opCode = 38;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeForwardPointer(uint PointerType, StorageClass param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PointerType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 39;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantTrue(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 41;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantFalse(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 42;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstant(uint returnId, uint param1, double Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 43;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantComposite(uint returnId, uint param1, params uint[] Constituents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constituents));
            ushort opCode = 44;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantSampler(uint returnId, uint param1, SamplerAddressingMode param2, uint Param, SamplerFilterMode param4) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 45;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantNull(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 46;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantTrue(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 48;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantFalse(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 49;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstant(uint returnId, uint param1, double Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 50;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantComposite(uint returnId, uint param1, params uint[] Constituents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constituents));
            ushort opCode = 51;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantOp(uint returnId, uint param1, uint Opcode) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Opcode));
            ushort opCode = 52;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFunction(uint returnId, uint param1, FunctionControl param2, uint FunctionType) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FunctionType));
            ushort opCode = 54;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionParameter(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 55;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpFunctionEnd() {
            ushort opCode = 56;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionCall(uint returnId, uint param1, uint Function, params uint[] Argument0Argument1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Function));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Argument0Argument1));
            ushort opCode = 57;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVariable(uint returnId, uint param1, StorageClass param2, uint? Initializer = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Initializer));
            ushort opCode = 59;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageTexelPointer(uint returnId, uint param1, uint Image, uint Coordinate, uint Sample) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Sample));
            ushort opCode = 60;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLoad(uint returnId, uint param1, uint Pointer, MemoryAccess? param3 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param3));
            ushort opCode = 61;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpStore(uint Pointer, uint Object, MemoryAccess? param2 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Object));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            ushort opCode = 62;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemory(uint Target, uint Source, MemoryAccess? param2 = null, MemoryAccess? param3 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Source));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param3));
            ushort opCode = 63;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCopyMemorySized(uint Target, uint Source, uint Size, MemoryAccess? param3 = null, MemoryAccess? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Source));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Size));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param3));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 64;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAccessChain(uint returnId, uint param1, uint Base, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 65;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsAccessChain(uint returnId, uint param1, uint Base, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 66;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrAccessChain(uint returnId, uint param1, uint Base, uint Element, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Element));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 67;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpArrayLength(uint returnId, uint param1, uint Structure, uint Arraymember) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Structure));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Arraymember));
            ushort opCode = 68;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGenericPtrMemSemantics(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 69;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpInBoundsPtrAccessChain(uint returnId, uint param1, uint Base, uint Element, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Element));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 70;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDecorate(uint Target, Decoration param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 71;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorate(uint StructureType, uint Member, Decoration param2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StructureType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            ushort opCode = 72;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDecorationGroup(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 73;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupDecorate(uint DecorationGroup, params uint[] Targets) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(DecorationGroup));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Targets));
            ushort opCode = 74;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupMemberDecorate(uint DecorationGroup, params PairIdRefLiteralInteger[] Targets) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(DecorationGroup));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Targets));
            ushort opCode = 75;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVectorExtractDynamic(uint returnId, uint param1, uint Vector, uint Index) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            ushort opCode = 77;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVectorInsertDynamic(uint returnId, uint param1, uint Vector, uint Component, uint Index) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Component));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            ushort opCode = 78;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVectorShuffle(uint returnId, uint param1, uint Vector1, uint Vector2, params uint[] Components) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector2));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Components));
            ushort opCode = 79;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeConstruct(uint returnId, uint param1, params uint[] Constituents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constituents));
            ushort opCode = 80;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeExtract(uint returnId, uint param1, uint Composite, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Composite));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 81;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCompositeInsert(uint returnId, uint param1, uint Object, uint Composite, params uint[] Indexes) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Object));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Composite));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Indexes));
            ushort opCode = 82;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCopyObject(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 83;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTranspose(uint returnId, uint param1, uint Matrix) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Matrix));
            ushort opCode = 84;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSampledImage(uint returnId, uint param1, uint Image, uint Sampler) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Sampler));
            ushort opCode = 86;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 87;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 88;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 89;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 90;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 91;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 92;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 93;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleProjDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 94;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageFetch(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 95;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Component));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 96;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageDrefGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 97;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageRead(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 98;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageWrite(uint Image, uint Coordinate, uint Texel, ImageOperands? param3 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Texel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param3));
            ushort opCode = 99;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImage(uint returnId, uint param1, uint SampledImage) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            ushort opCode = 100;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryFormat(uint returnId, uint param1, uint Image) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            ushort opCode = 101;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryOrder(uint returnId, uint param1, uint Image) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            ushort opCode = 102;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySizeLod(uint returnId, uint param1, uint Image, uint LevelofDetail) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LevelofDetail));
            ushort opCode = 103;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySize(uint returnId, uint param1, uint Image) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            ushort opCode = 104;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLod(uint returnId, uint param1, uint SampledImage, uint Coordinate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            ushort opCode = 105;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQueryLevels(uint returnId, uint param1, uint Image) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            ushort opCode = 106;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageQuerySamples(uint returnId, uint param1, uint Image) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            ushort opCode = 107;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToU(uint returnId, uint param1, uint FloatValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FloatValue));
            ushort opCode = 109;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertFToS(uint returnId, uint param1, uint FloatValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FloatValue));
            ushort opCode = 110;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertSToF(uint returnId, uint param1, uint SignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SignedValue));
            ushort opCode = 111;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToF(uint returnId, uint param1, uint UnsignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UnsignedValue));
            ushort opCode = 112;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUConvert(uint returnId, uint param1, uint UnsignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UnsignedValue));
            ushort opCode = 113;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSConvert(uint returnId, uint param1, uint SignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SignedValue));
            ushort opCode = 114;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFConvert(uint returnId, uint param1, uint FloatValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FloatValue));
            ushort opCode = 115;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpQuantizeToF16(uint returnId, uint param1, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 116;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertPtrToU(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 117;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertSToU(uint returnId, uint param1, uint SignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SignedValue));
            ushort opCode = 118;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSatConvertUToS(uint returnId, uint param1, uint UnsignedValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UnsignedValue));
            ushort opCode = 119;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToPtr(uint returnId, uint param1, uint IntegerValue) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(IntegerValue));
            ushort opCode = 120;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToGeneric(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 121;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtr(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 122;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGenericCastToPtrExplicit(uint returnId, uint param1, uint Pointer, StorageClass Storage) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Storage));
            ushort opCode = 123;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitcast(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 124;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSNegate(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 126;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFNegate(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 127;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIAdd(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 128;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFAdd(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 129;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpISub(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 130;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFSub(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 131;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIMul(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 132;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFMul(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 133;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUDiv(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 134;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSDiv(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 135;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFDiv(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 136;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUMod(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 137;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSRem(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 138;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSMod(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 139;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFRem(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 140;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFMod(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 141;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesScalar(uint returnId, uint param1, uint Vector, uint Scalar) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Scalar));
            ushort opCode = 142;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesScalar(uint returnId, uint param1, uint Matrix, uint Scalar) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Matrix));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Scalar));
            ushort opCode = 143;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVectorTimesMatrix(uint returnId, uint param1, uint Vector, uint Matrix) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Matrix));
            ushort opCode = 144;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesVector(uint returnId, uint param1, uint Matrix, uint Vector) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Matrix));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            ushort opCode = 145;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMatrixTimesMatrix(uint returnId, uint param1, uint LeftMatrix, uint RightMatrix) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LeftMatrix));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RightMatrix));
            ushort opCode = 146;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpOuterProduct(uint returnId, uint param1, uint Vector1, uint Vector2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector2));
            ushort opCode = 147;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDot(uint returnId, uint param1, uint Vector1, uint Vector2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector2));
            ushort opCode = 148;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIAddCarry(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 149;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpISubBorrow(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 150;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUMulExtended(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 151;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSMulExtended(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 152;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAny(uint returnId, uint param1, uint Vector) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            ushort opCode = 154;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAll(uint returnId, uint param1, uint Vector) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Vector));
            ushort opCode = 155;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsNan(uint returnId, uint param1, uint x) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            ushort opCode = 156;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsInf(uint returnId, uint param1, uint x) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            ushort opCode = 157;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsFinite(uint returnId, uint param1, uint x) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            ushort opCode = 158;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsNormal(uint returnId, uint param1, uint x) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            ushort opCode = 159;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSignBitSet(uint returnId, uint param1, uint x) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            ushort opCode = 160;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLessOrGreater(uint returnId, uint param1, uint x, uint y) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(y));
            ushort opCode = 161;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpOrdered(uint returnId, uint param1, uint x, uint y) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(y));
            ushort opCode = 162;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUnordered(uint returnId, uint param1, uint x, uint y) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(x));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(y));
            ushort opCode = 163;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 164;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 165;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalOr(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 166;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalAnd(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 167;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLogicalNot(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 168;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSelect(uint returnId, uint param1, uint Condition, uint Object1, uint Object2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Condition));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Object1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Object2));
            ushort opCode = 169;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 170;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpINotEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 171;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 172;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 173;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 174;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 175;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpULessThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 176;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 177;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpULessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 178;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 179;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 180;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 181;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 182;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 183;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 184;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 185;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 186;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThan(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 187;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 188;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordLessThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 189;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFOrdGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 190;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFUnordGreaterThanEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 191;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightLogical(uint returnId, uint param1, uint Base, uint Shift) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Shift));
            ushort opCode = 194;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpShiftRightArithmetic(uint returnId, uint param1, uint Base, uint Shift) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Shift));
            ushort opCode = 195;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpShiftLeftLogical(uint returnId, uint param1, uint Base, uint Shift) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Shift));
            ushort opCode = 196;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseOr(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 197;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseXor(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 198;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitwiseAnd(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 199;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpNot(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 200;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldInsert(uint returnId, uint param1, uint Base, uint Insert, uint Offset, uint Count) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Insert));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Offset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Count));
            ushort opCode = 201;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldSExtract(uint returnId, uint param1, uint Base, uint Offset, uint Count) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Offset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Count));
            ushort opCode = 202;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitFieldUExtract(uint returnId, uint param1, uint Base, uint Offset, uint Count) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Offset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Count));
            ushort opCode = 203;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitReverse(uint returnId, uint param1, uint Base) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            ushort opCode = 204;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBitCount(uint returnId, uint param1, uint Base) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Base));
            ushort opCode = 205;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdx(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 207;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdy(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 208;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFwidth(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 209;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxFine(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 210;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyFine(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 211;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthFine(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 212;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdxCoarse(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 213;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDPdyCoarse(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 214;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFwidthCoarse(uint returnId, uint param1, uint P) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(P));
            ushort opCode = 215;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpEmitVertex() {
            ushort opCode = 218;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpEndPrimitive() {
            ushort opCode = 219;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpEmitStreamVertex(uint Stream) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Stream));
            ushort opCode = 220;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpEndStreamPrimitive(uint Stream) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Stream));
            ushort opCode = 221;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpControlBarrier(uint Execution, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 224;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemoryBarrier(uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 225;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicLoad(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 227;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicStore(uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 228;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicExchange(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 229;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchange(uint returnId, uint param1, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Equal));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Unequal));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Comparator));
            ushort opCode = 230;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicCompareExchangeWeak(uint returnId, uint param1, uint Pointer, uint Memory, uint Equal, uint Unequal, uint Value, uint Comparator) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Equal));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Unequal));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Comparator));
            ushort opCode = 231;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIIncrement(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 232;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIDecrement(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 233;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicIAdd(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 234;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicISub(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 235;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMin(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 236;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMin(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 237;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicSMax(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 238;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicUMax(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 239;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicAnd(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 240;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicOr(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 241;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicXor(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 242;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPhi(uint returnId, uint param1, params PairIdRefIdRef[] VariableParent) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(VariableParent));
            ushort opCode = 245;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLoopMerge(uint MergeBlock, uint ContinueTarget, LoopControl param2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MergeBlock));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ContinueTarget));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            ushort opCode = 246;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSelectionMerge(uint MergeBlock, SelectionControl param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MergeBlock));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 247;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLabel(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 248;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBranch(uint TargetLabel) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(TargetLabel));
            ushort opCode = 249;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBranchConditional(uint Condition, uint TrueLabel, uint FalseLabel, params uint[] Branchweights) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Condition));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(TrueLabel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FalseLabel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Branchweights));
            ushort opCode = 250;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSwitch(uint Selector, uint Default, params PairLiteralIntegerIdRef[] Target) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Selector));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Default));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            ushort opCode = 251;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpKill() {
            ushort opCode = 252;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpReturn() {
            ushort opCode = 253;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReturnValue(uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 254;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpUnreachable() {
            ushort opCode = 255;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLifetimeStart(uint Pointer, uint Size) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Size));
            ushort opCode = 256;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLifetimeStop(uint Pointer, uint Size) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Size));
            ushort opCode = 257;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAsyncCopy(uint returnId, uint param1, uint Execution, uint Destination, uint Source, uint NumElements, uint Stride, uint Event) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Destination));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Source));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumElements));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Stride));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            ushort opCode = 259;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupWaitEvents(uint Execution, uint NumEvents, uint EventsList) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumEvents));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(EventsList));
            ushort opCode = 260;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAll(uint returnId, uint param1, uint Execution, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 261;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupAny(uint returnId, uint param1, uint Execution, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 262;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint LocalId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LocalId));
            ushort opCode = 263;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 264;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 265;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 266;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 267;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 268;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 269;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 270;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 271;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipe(uint returnId, uint param1, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 274;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipe(uint returnId, uint param1, uint Pipe, uint Pointer, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 275;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReservedReadPipe(uint returnId, uint param1, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 276;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReservedWritePipe(uint returnId, uint param1, uint Pipe, uint ReserveId, uint Index, uint Pointer, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 277;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReserveReadPipePackets(uint returnId, uint param1, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumPackets));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 278;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReserveWritePipePackets(uint returnId, uint param1, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumPackets));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 279;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCommitReadPipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 280;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCommitWritePipe(uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 281;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidReserveId(uint returnId, uint param1, uint ReserveId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            ushort opCode = 282;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetNumPipePackets(uint returnId, uint param1, uint Pipe, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 283;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetMaxPipePackets(uint returnId, uint param1, uint Pipe, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 284;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveReadPipePackets(uint returnId, uint param1, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumPackets));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 285;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupReserveWritePipePackets(uint returnId, uint param1, uint Execution, uint Pipe, uint NumPackets, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumPackets));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 286;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitReadPipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 287;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupCommitWritePipe(uint Execution, uint Pipe, uint ReserveId, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pipe));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReserveId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 288;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueMarker(uint returnId, uint param1, uint Queue, uint NumEvents, uint WaitEvents, uint RetEvent) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Queue));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumEvents));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(WaitEvents));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RetEvent));
            ushort opCode = 291;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpEnqueueKernel(uint returnId, uint param1, uint Queue, uint Flags, uint NDRange, uint NumEvents, uint WaitEvents, uint RetEvent, uint Invoke, uint Param, uint ParamSize, uint ParamAlign, params uint[] LocalSize) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Queue));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Flags));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NDRange));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NumEvents));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(WaitEvents));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RetEvent));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LocalSize));
            ushort opCode = 292;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeSubGroupCount(uint returnId, uint param1, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NDRange));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 293;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelNDrangeMaxSubGroupSize(uint returnId, uint param1, uint NDRange, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NDRange));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 294;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelWorkGroupSize(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 295;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelPreferredWorkGroupSizeMultiple(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 296;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRetainEvent(uint Event) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            ushort opCode = 297;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReleaseEvent(uint Event) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            ushort opCode = 298;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCreateUserEvent(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 299;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsValidEvent(uint returnId, uint param1, uint Event) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            ushort opCode = 300;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSetUserEventStatus(uint Event, uint Status) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Status));
            ushort opCode = 301;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCaptureEventProfilingInfo(uint Event, uint ProfilingInfo, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Event));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ProfilingInfo));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 302;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetDefaultQueue(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 303;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpBuildNDRange(uint returnId, uint param1, uint GlobalWorkSize, uint LocalWorkSize, uint GlobalWorkOffset) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(GlobalWorkSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LocalWorkSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(GlobalWorkOffset));
            ushort opCode = 304;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 305;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 306;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 307;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 308;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 309;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, ImageOperands param4) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 310;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefImplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 311;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseSampleProjDrefExplicitLod(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands param5) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 312;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseFetch(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 313;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Component, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Component));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 314;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseDrefGather(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint D, ImageOperands? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(D));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 315;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseTexelsResident(uint returnId, uint param1, uint ResidentCode) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ResidentCode));
            ushort opCode = 316;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpNoLine() {
            ushort opCode = 317;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagTestAndSet(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 318;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFlagClear(uint Pointer, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 319;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSparseRead(uint returnId, uint param1, uint Image, uint Coordinate, ImageOperands? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 320;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSizeOf(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 321;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypePipeStorage(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 322;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantPipeStorage(uint returnId, uint param1, uint PacketSize, uint PacketAlignment, uint Capacity) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Capacity));
            ushort opCode = 323;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCreatePipeFromPipeStorage(uint returnId, uint param1, uint PipeStorage) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PipeStorage));
            ushort opCode = 324;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelLocalSizeForSubgroupCount(uint returnId, uint param1, uint SubgroupCount, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SubgroupCount));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 325;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGetKernelMaxNumSubgroups(uint returnId, uint param1, uint Invoke, uint Param, uint ParamSize, uint ParamAlign) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Invoke));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Param));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ParamAlign));
            ushort opCode = 326;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeNamedBarrier(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 327;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpNamedBarrierInitialize(uint returnId, uint param1, uint SubgroupCount) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SubgroupCount));
            ushort opCode = 328;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemoryNamedBarrier(uint NamedBarrier, uint Memory, uint Semantics) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(NamedBarrier));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            ushort opCode = 329;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpModuleProcessed(string Process) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Process));
            ushort opCode = 330;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExecutionModeId(uint EntryPoint, ExecutionMode Mode) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(EntryPoint));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Mode));
            ushort opCode = 331;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateId(uint Target, Decoration param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 332;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformElect(uint returnId, uint param1, uint Execution) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            ushort opCode = 333;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAll(uint returnId, uint param1, uint Execution, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAny(uint returnId, uint param1, uint Execution, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 335;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformAllEqual(uint returnId, uint param1, uint Execution, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 336;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint Id) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Id));
            ushort opCode = 337;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBroadcastFirst(uint returnId, uint param1, uint Execution, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 338;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallot(uint returnId, uint param1, uint Execution, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 339;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformInverseBallot(uint returnId, uint param1, uint Execution, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 340;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitExtract(uint returnId, uint param1, uint Execution, uint Value, uint Index) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            ushort opCode = 341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotBitCount(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 342;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindLSB(uint returnId, uint param1, uint Execution, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 343;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBallotFindMSB(uint returnId, uint param1, uint Execution, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 344;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffle(uint returnId, uint param1, uint Execution, uint Value, uint Id) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Id));
            ushort opCode = 345;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleXor(uint returnId, uint param1, uint Execution, uint Value, uint Mask) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Mask));
            ushort opCode = 346;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleUp(uint returnId, uint param1, uint Execution, uint Value, uint Delta) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Delta));
            ushort opCode = 347;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformShuffleDown(uint returnId, uint param1, uint Execution, uint Value, uint Delta) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Delta));
            ushort opCode = 348;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 349;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFAdd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 350;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformIMul(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 351;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMul(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 352;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 353;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 354;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMin(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 355;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformSMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 356;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformUMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 357;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformFMax(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 358;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseAnd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 359;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseOr(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 360;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformBitwiseXor(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 361;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalAnd(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 362;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalOr(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 363;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformLogicalXor(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint Value, uint? ClusterSize = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ClusterSize));
            ushort opCode = 364;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadBroadcast(uint returnId, uint param1, uint Execution, uint Value, uint Index) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            ushort opCode = 365;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformQuadSwap(uint returnId, uint param1, uint Execution, uint Value, uint Direction) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            ushort opCode = 366;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCopyLogical(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 400;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 401;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrNotEqual(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 402;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrDiff(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 403;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpTerminateInvocation() {
            ushort opCode = 4416;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBallotKHR(uint returnId, uint param1, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 4421;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupFirstInvocationKHR(uint returnId, uint param1, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 4422;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllKHR(uint returnId, uint param1, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 4428;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAnyKHR(uint returnId, uint param1, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 4429;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAllEqualKHR(uint returnId, uint param1, uint Predicate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Predicate));
            ushort opCode = 4430;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupReadInvocationKHR(uint returnId, uint param1, uint Value, uint Index) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Index));
            ushort opCode = 4432;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTraceRayKHR(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Accel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayFlags));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CullMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTStride));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MissIndex));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayOrigin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTmin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayDirection));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTmax));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 4445;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableKHR(uint SBTIndex, uint CallableData) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTIndex));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CallableData));
            ushort opCode = 4446;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConvertUToAccelerationStructureKHR(uint returnId, uint param1, uint Accel) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Accel));
            ushort opCode = 4447;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpIgnoreIntersectionKHR() {
            ushort opCode = 4448;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpTerminateRayKHR() {
            ushort opCode = 4449;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeRayQueryKHR(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 4472;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryInitializeKHR(uint RayQuery, uint Accel, uint RayFlags, uint CullMask, uint RayOrigin, uint RayTMin, uint RayDirection, uint RayTMax) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Accel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayFlags));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CullMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayOrigin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTMin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayDirection));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTMax));
            ushort opCode = 4473;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryTerminateKHR(uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 4474;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGenerateIntersectionKHR(uint RayQuery, uint HitT) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(HitT));
            ushort opCode = 4475;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryConfirmIntersectionKHR(uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 4476;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryProceedKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 4477;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTypeKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 4479;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupIAddNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5000;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFAddNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5001;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5002;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5003;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMinNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5004;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupFMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5005;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupUMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5006;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupSMaxNonUniformAMD(uint returnId, uint param1, uint Execution, GroupOperation Operation, uint X) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operation));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(X));
            ushort opCode = 5007;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentMaskFetchAMD(uint returnId, uint param1, uint Image, uint Coordinate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            ushort opCode = 5011;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFragmentFetchAMD(uint returnId, uint param1, uint Image, uint Coordinate, uint FragmentIndex) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FragmentIndex));
            ushort opCode = 5012;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReadClockKHR(uint returnId, uint param1, uint Execution) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            ushort opCode = 5056;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpImageSampleFootprintNV(uint returnId, uint param1, uint SampledImage, uint Coordinate, uint Granularity, uint Coarse, ImageOperands? param6 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SampledImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Granularity));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coarse));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param6));
            ushort opCode = 5283;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpGroupNonUniformPartitionNV(uint returnId, uint param1, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 5296;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpWritePackedPrimitiveIndices4x8NV(uint IndexOffset, uint PackedIndices) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(IndexOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedIndices));
            ushort opCode = 5299;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionNV(uint returnId, uint param1, uint Hit, uint HitKind) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Hit));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(HitKind));
            ushort opCode = 5334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReportIntersectionKHR(uint returnId, uint param1, uint Hit, uint HitKind) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Hit));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(HitKind));
            ushort opCode = 5334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpIgnoreIntersectionNV() {
            ushort opCode = 5335;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpTerminateRayNV() {
            ushort opCode = 5336;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTraceNV(uint Accel, uint RayFlags, uint CullMask, uint SBTOffset, uint SBTStride, uint MissIndex, uint RayOrigin, uint RayTmin, uint RayDirection, uint RayTmax, uint PayloadId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Accel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayFlags));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CullMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTStride));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MissIndex));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayOrigin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTmin));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayDirection));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayTmax));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PayloadId));
            ushort opCode = 5337;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureNV(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAccelerationStructureKHR(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpExecuteCallableNV(uint SBTIndex, uint CallableDataId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SBTIndex));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CallableDataId));
            ushort opCode = 5344;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeCooperativeMatrixNV(uint returnId, uint ComponentType, uint Execution, uint Rows, uint Columns) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ComponentType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Execution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Rows));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Columns));
            ushort opCode = 5358;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLoadNV(uint returnId, uint param1, uint Pointer, uint Stride, uint ColumnMajor, MemoryAccess? param5 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Stride));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ColumnMajor));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param5));
            ushort opCode = 5359;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixStoreNV(uint Pointer, uint Object, uint Stride, uint ColumnMajor, MemoryAccess? param4 = null) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Object));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Stride));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ColumnMajor));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param4));
            ushort opCode = 5360;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixMulAddNV(uint returnId, uint param1, uint A, uint B, uint C) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(A));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(B));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(C));
            ushort opCode = 5361;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCooperativeMatrixLengthNV(uint returnId, uint param1, uint Type) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Type));
            ushort opCode = 5362;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        public void GenerateOpBeginInvocationInterlockEXT() {
            ushort opCode = 5364;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpEndInvocationInterlockEXT() {
            ushort opCode = 5365;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        public void GenerateOpDemoteToHelperInvocationEXT() {
            ushort opCode = 5380;
            ushort wordCount = 1;
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIsHelperInvocationEXT(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5381;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleINTEL(uint returnId, uint param1, uint Data, uint InvocationId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Data));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(InvocationId));
            ushort opCode = 5571;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleDownINTEL(uint returnId, uint param1, uint Current, uint Next, uint Delta) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Current));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Next));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Delta));
            ushort opCode = 5572;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleUpINTEL(uint returnId, uint param1, uint Previous, uint Current, uint Delta) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Previous));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Current));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Delta));
            ushort opCode = 5573;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupShuffleXorINTEL(uint returnId, uint param1, uint Data, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Data));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 5574;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockReadINTEL(uint returnId, uint param1, uint Ptr) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Ptr));
            ushort opCode = 5575;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupBlockWriteINTEL(uint Ptr, uint Data) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Ptr));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Data));
            ushort opCode = 5576;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockReadINTEL(uint returnId, uint param1, uint Image, uint Coordinate) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            ushort opCode = 5577;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageBlockWriteINTEL(uint Image, uint Coordinate, uint Data) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Data));
            ushort opCode = 5578;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockReadINTEL(uint returnId, uint param1, uint Image, uint Coordinate, uint Width, uint Height) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Width));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Height));
            ushort opCode = 5580;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupImageMediaBlockWriteINTEL(uint Image, uint Coordinate, uint Width, uint Height, uint Data) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Image));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Coordinate));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Width));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Height));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Data));
            ushort opCode = 5581;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUCountLeadingZerosINTEL(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 5585;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUCountTrailingZerosINTEL(uint returnId, uint param1, uint Operand) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand));
            ushort opCode = 5586;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAbsISubINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5587;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAbsUSubINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5588;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIAddSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5589;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUAddSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5590;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5591;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5592;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIAverageRoundedINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5593;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUAverageRoundedINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5594;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpISubSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5595;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUSubSatINTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5596;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpIMul32x16INTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5597;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpUMul32x16INTEL(uint returnId, uint param1, uint Operand1, uint Operand2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand2));
            ushort opCode = 5598;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstFunctionPointerINTEL(uint returnId, uint param1, uint Function) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Function));
            ushort opCode = 5600;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFunctionPointerCallINTEL(uint returnId, uint param1, params uint[] Operand1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Operand1));
            ushort opCode = 5601;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAsmTargetINTEL(uint returnId, uint param1, string Asmtarget) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Asmtarget));
            ushort opCode = 5609;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAsmINTEL(uint returnId, uint param1, uint Asmtype, uint Target, string Asminstructions, string Constraints) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Asmtype));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Asminstructions));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constraints));
            ushort opCode = 5610;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAsmCallINTEL(uint returnId, uint param1, uint Asm, params uint[] Argument0) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Asm));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Argument0));
            ushort opCode = 5611;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMinEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 5614;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFMaxEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 5615;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateString(uint Target, Decoration param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5632;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpDecorateStringGOOGLE(uint Target, Decoration param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Target));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5632;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateString(uint StructType, uint Member, Decoration param2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StructType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            ushort opCode = 5633;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpMemberDecorateStringGOOGLE(uint StructType, uint Member, Decoration param2) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StructType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param2));
            ushort opCode = 5633;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVmeImageINTEL(uint returnId, uint param1, uint ImageType, uint Sampler) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ImageType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Sampler));
            ushort opCode = 5699;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeVmeImageINTEL(uint returnId, uint ImageType) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ImageType));
            ushort opCode = 5700;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImePayloadINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5701;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefPayloadINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5702;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicPayloadINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5703;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMcePayloadINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5704;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcMceResultINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5705;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5706;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultSingleReferenceStreamoutINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5707;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeResultDualReferenceStreamoutINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5708;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeSingleReferenceStreaminINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5709;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcImeDualReferenceStreaminINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5710;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcRefResultINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5711;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeAvcSicResultINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 5712;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterBaseMultiReferencePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5713;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterBaseMultiReferencePenaltyINTEL(uint returnId, uint param1, uint ReferenceBasePenalty, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReferenceBasePenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5714;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterShapePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5715;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterShapePenaltyINTEL(uint returnId, uint param1, uint PackedShapePenalty, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedShapePenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5716;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterDirectionPenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5717;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetInterDirectionPenaltyINTEL(uint returnId, uint param1, uint DirectionCost, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(DirectionCost));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5718;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaShapePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5719;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultInterMotionVectorCostTableINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5720;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultHighPenaltyCostTableINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5721;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultMediumPenaltyCostTableINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5722;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultLowPenaltyCostTableINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5723;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetMotionVectorCostFunctionINTEL(uint returnId, uint param1, uint PackedCostCenterDelta, uint PackedCostTable, uint CostPrecision, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedCostCenterDelta));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedCostTable));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(CostPrecision));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5724;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaModePenaltyINTEL(uint returnId, uint param1, uint SliceType, uint Qp) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SliceType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Qp));
            ushort opCode = 5725;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultNonDcLumaIntraPenaltyINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5726;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetDefaultIntraChromaModeBasePenaltyINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5727;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetAcOnlyHaarINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5728;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSourceInterlacedFieldPolarityINTEL(uint returnId, uint param1, uint SourceFieldPolarity, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SourceFieldPolarity));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5729;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetSingleReferenceInterlacedFieldPolarityINTEL(uint returnId, uint param1, uint ReferenceFieldPolarity, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ReferenceFieldPolarity));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5730;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceSetDualReferenceInterlacedFieldPolaritiesINTEL(uint returnId, uint param1, uint ForwardReferenceFieldPolarity, uint BackwardReferenceFieldPolarity, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ForwardReferenceFieldPolarity));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BackwardReferenceFieldPolarity));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5731;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImePayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5732;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToImeResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5733;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefPayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5734;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToRefResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5735;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicPayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5736;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceConvertToSicResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5737;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetMotionVectorsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5738;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDistortionsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5739;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetBestInterDistortionsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5740;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMajorShapeINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5741;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMinorShapeINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5742;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterDirectionsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5743;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterMotionVectorCountINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5744;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceIdsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5745;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcMceGetInterReferenceInterlacedFieldPolaritiesINTEL(uint returnId, uint param1, uint PackedReferenceIds, uint PackedReferenceParameterFieldPolarities, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceIds));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceParameterFieldPolarities));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5746;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint PartitionMask, uint SADAdjustment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcCoord));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PartitionMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SADAdjustment));
            ushort opCode = 5747;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetSingleReferenceINTEL(uint returnId, uint param1, uint RefOffset, uint SearchWindowConfig, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SearchWindowConfig));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5748;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetDualReferenceINTEL(uint returnId, uint param1, uint FwdRefOffset, uint BwdRefOffset, uint idSearchWindowConfig, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(idSearchWindowConfig));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5749;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeRefWindowSizeINTEL(uint returnId, uint param1, uint SearchWindowConfig, uint DualRef) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SearchWindowConfig));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(DualRef));
            ushort opCode = 5750;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeAdjustRefOffsetINTEL(uint returnId, uint param1, uint RefOffset, uint SrcCoord, uint RefWindowSize, uint ImageSize) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefOffset));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcCoord));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefWindowSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ImageSize));
            ushort opCode = 5751;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5752;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetMaxMotionVectorCountINTEL(uint returnId, uint param1, uint MaxMotionVectorCount, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MaxMotionVectorCount));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5753;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetUnidirectionalMixDisableINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5754;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetEarlySearchTerminationThresholdINTEL(uint returnId, uint param1, uint Threshold, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Threshold));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5755;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeSetWeightedSadINTEL(uint returnId, uint param1, uint PackedSadWeights, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedSadWeights));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5756;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5757;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5758;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StreaminComponents));
            ushort opCode = 5759;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StreaminComponents));
            ushort opCode = 5760;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreamoutINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5761;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreamoutINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5762;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminoutINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload, uint StreaminComponents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StreaminComponents));
            ushort opCode = 5763;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminoutINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload, uint StreaminComponents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(StreaminComponents));
            ushort opCode = 5764;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeConvertToMceResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5765;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetSingleReferenceStreaminINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5766;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetDualReferenceStreaminINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5767;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripSingleReferenceStreamoutINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5768;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeStripDualReferenceStreamoutINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5769;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeMotionVectorsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            ushort opCode = 5770;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeDistortionsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            ushort opCode = 5771;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeReferenceIdsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            ushort opCode = 5772;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeMotionVectorsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            ushort opCode = 5773;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeDistortionsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            ushort opCode = 5774;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeReferenceIdsINTEL(uint returnId, uint param1, uint Payload, uint MajorShape, uint Direction) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShape));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            ushort opCode = 5775;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetBorderReachedINTEL(uint returnId, uint param1, uint ImageSelect, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ImageSelect));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5776;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetTruncatedSearchIndicationINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5777;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetUnidirectionalEarlySearchTerminationINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5778;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumMotionVectorINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5779;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumDistortionINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5780;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcFmeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint SadAdjustment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcCoord));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MotionVectors));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShapes));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MinorShapes));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PixelResolution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SadAdjustment));
            ushort opCode = 5781;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcBmeInitializeINTEL(uint returnId, uint param1, uint SrcCoord, uint MotionVectors, uint MajorShapes, uint MinorShapes, uint Direction, uint PixelResolution, uint BidirectionalWeight, uint SadAdjustment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcCoord));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MotionVectors));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MajorShapes));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MinorShapes));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PixelResolution));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BidirectionalWeight));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SadAdjustment));
            ushort opCode = 5782;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5783;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBidirectionalMixDisableINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5784;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefSetBilinearFilterEnableINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5785;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5786;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5787;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceIds));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5788;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceInterlacedINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceIds));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceFieldPolarities));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5789;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcRefConvertToMceResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5790;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicInitializeINTEL(uint returnId, uint param1, uint SrcCoord) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcCoord));
            ushort opCode = 5791;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureSkcINTEL(uint returnId, uint param1, uint SkipBlockPartitionType, uint SkipMotionVectorMask, uint MotionVectors, uint BidirectionalWeight, uint SadAdjustment, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SkipBlockPartitionType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SkipMotionVectorMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(MotionVectors));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BidirectionalWeight));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SadAdjustment));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5792;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaINTEL(uint returnId, uint param1, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint SadAdjustment, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LumaIntraPartitionMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(IntraNeighbourAvailabilty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LeftEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperLeftCornerLumaPixel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperRightEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SadAdjustment));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5793;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConfigureIpeLumaChromaINTEL(uint returnId, uint param1, uint LumaIntraPartitionMask, uint IntraNeighbourAvailabilty, uint LeftEdgeLumaPixels, uint UpperLeftCornerLumaPixel, uint UpperEdgeLumaPixels, uint UpperRightEdgeLumaPixels, uint LeftEdgeChromaPixels, uint UpperLeftCornerChromaPixel, uint UpperEdgeChromaPixels, uint SadAdjustment, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LumaIntraPartitionMask));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(IntraNeighbourAvailabilty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LeftEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperLeftCornerLumaPixel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperRightEdgeLumaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LeftEdgeChromaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperLeftCornerChromaPixel));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(UpperEdgeChromaPixels));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SadAdjustment));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5794;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetMotionVectorMaskINTEL(uint returnId, uint param1, uint SkipBlockPartitionType, uint Direction) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SkipBlockPartitionType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Direction));
            ushort opCode = 5795;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMcePayloadINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5796;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaShapePenaltyINTEL(uint returnId, uint param1, uint PackedShapePenalty, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedShapePenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5797;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraLumaModeCostFunctionINTEL(uint returnId, uint param1, uint LumaModePenalty, uint LumaPackedNeighborModes, uint LumaPackedNonDcPenalty, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LumaModePenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LumaPackedNeighborModes));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LumaPackedNonDcPenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5798;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetIntraChromaModeCostFunctionINTEL(uint returnId, uint param1, uint ChromaModeBasePenalty, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(ChromaModeBasePenalty));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5799;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBilinearFilterEnableINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5800;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetSkcForwardTransformEnableINTEL(uint returnId, uint param1, uint PackedSadCoefficients, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedSadCoefficients));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5801;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicSetBlockBasedRawSkipSadINTEL(uint returnId, uint param1, uint BlockBasedSkipType, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BlockBasedSkipType));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5802;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateIpeINTEL(uint returnId, uint param1, uint SrcImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5803;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithSingleReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint RefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5804;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithDualReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint FwdRefImage, uint BwdRefImage, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(FwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(BwdRefImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5805;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceIds));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5806;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceInterlacedINTEL(uint returnId, uint param1, uint SrcImage, uint PackedReferenceIds, uint PackedReferenceFieldPolarities, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(SrcImage));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceIds));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PackedReferenceFieldPolarities));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5807;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicConvertToMceResultINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5808;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeLumaShapeINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5809;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeLumaDistortionINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5810;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetBestIpeChromaDistortionINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5811;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedIpeLumaModesINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5812;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetIpeChromaModeINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5813;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaCountThresholdINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5814;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaSumThresholdINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5815;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSubgroupAvcSicGetInterRawSadsINTEL(uint returnId, uint param1, uint Payload) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Payload));
            ushort opCode = 5816;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpVariableLengthArrayINTEL(uint returnId, uint param1, uint Lenght) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Lenght));
            ushort opCode = 5818;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSaveMemoryINTEL(uint returnId, uint param1) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            ushort opCode = 5819;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRestoreMemoryINTEL(uint Ptr) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Ptr));
            ushort opCode = 5820;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpLoopControlINTEL(params uint[] LoopControlParameters) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(LoopControlParameters));
            ushort opCode = 5887;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpPtrCastToCrossWorkgroupINTEL(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 5934;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpCrossWorkgroupCastToPtrINTEL(uint returnId, uint param1, uint Pointer) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            ushort opCode = 5938;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpReadPipeBlockingINTEL(uint returnId, uint param1, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 5946;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpWritePipeBlockingINTEL(uint returnId, uint param1, uint PacketSize, uint PacketAlignment) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketSize));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(PacketAlignment));
            ushort opCode = 5947;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpFPGARegINTEL(uint returnId, uint param1, uint Result, uint Input) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Result));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Input));
            ushort opCode = 5949;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayTMinKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 6016;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetRayFlagsKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 6017;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionTKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6018;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceCustomIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6019;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceIdKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6020;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionInstanceShaderBindingTableRecordOffsetKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6021;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionGeometryIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6022;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionPrimitiveIndexKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6023;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionBarycentricsKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6024;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionFrontFaceKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6025;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionCandidateAABBOpaqueKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 6026;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayDirectionKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6027;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectRayOriginKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6028;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayDirectionKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 6029;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetWorldRayOriginKHR(uint returnId, uint param1, uint RayQuery) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            ushort opCode = 6030;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionObjectToWorldKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6031;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpRayQueryGetIntersectionWorldToObjectKHR(uint returnId, uint param1, uint RayQuery, uint Intersection) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(RayQuery));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Intersection));
            ushort opCode = 6032;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpAtomicFAddEXT(uint returnId, uint param1, uint Pointer, uint Memory, uint Semantics, uint Value) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(param1));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Pointer));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Memory));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Semantics));
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Value));
            ushort opCode = 6035;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeBufferSurfaceINTEL(uint returnId) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(returnId));
            ushort opCode = 6086;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpTypeStructContinuedINTEL(params uint[] Member0typemember1type) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Member0typemember1type));
            ushort opCode = 6090;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpConstantCompositeContinuedINTEL(params uint[] Constituents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constituents));
            ushort opCode = 6091;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
        [CLSCompliant(false)]
        public void GenerateOpSpecConstantCompositeContinuedINTEL(params uint[] Constituents) {
            var tempList = new List<uint>();
            tempList.AddRange(SPIRVBuilderUtils.ToUintList(Constituents));
            ushort opCode = 6092;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combinedWord = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(combinedWord);
            _instructions.AddRange(tempList);
        }
        
    }
}
#pragma warning restore 1591