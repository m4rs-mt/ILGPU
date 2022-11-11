// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: BinarySPIRVBuilder.cs  
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Collections.Generic;
using ILGPU.Backends.SPIRV.Types;

// disable: max_line_length

#nullable enable

namespace ILGPU.Backends.SPIRV {

    internal class BinarySPIRVBuilder : ISPIRVBuilder
    {

        private readonly List<SPIRVWord> _instructions = new List<SPIRVWord>();

        public byte[] ToByteArray() => _instructions
            .Select(x => x.Data)
            .Select(x => BitConverter.GetBytes(x))
            .SelectMany(x => x)
            .ToArray();

        public void AddMetadata(
            SPIRVWord magic,
            SPIRVWord version,
            SPIRVWord genMagic,
            SPIRVWord bound,
            SPIRVWord schema)
        {
            _instructions.Add(magic);
            _instructions.Add(version);
            _instructions.Add(genMagic);
            _instructions.Add(bound);
            _instructions.Add(schema);
        }

        public void Merge(ISPIRVBuilder other)
        {
            if(other == null)
                throw new ArgumentNullException(nameof(other));

            if(other is BinarySPIRVBuilder otherBinary)
            {
                _instructions.AddRange(otherBinary._instructions);
                return;
            }

            throw new InvalidCodeGenerationException(
                "Attempted to merge string representation builder with binary builder"
            );
        }

        public void GenerateOpNop()
        {
            ushort opCode = 0;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpUndef(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 1;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSourceContinued(LiteralString continuedSource)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(continuedSource.ToWords());
            ushort opCode = 2;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSource(SourceLanguage param0, LiteralInteger version, IdRef? file = null, LiteralString? source = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(param0.ToWords());
            tempList.AddRange(version.ToWords());
            if(file is IdRef fileNotNull)
                tempList.AddRange(fileNotNull.ToWords());
            if(source is LiteralString sourceNotNull)
                tempList.AddRange(sourceNotNull.ToWords());
            ushort opCode = 3;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSourceExtension(LiteralString extension)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(extension.ToWords());
            ushort opCode = 4;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpName(IdRef target, LiteralString name)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(name.ToWords());
            ushort opCode = 5;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemberName(IdRef type, LiteralInteger member, LiteralString name)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(type.ToWords());
            tempList.AddRange(member.ToWords());
            tempList.AddRange(name.ToWords());
            ushort opCode = 6;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpString(IdResult resultId, LiteralString @string)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@string.ToWords());
            ushort opCode = 7;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLine(IdRef file, LiteralInteger line, LiteralInteger column)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(file.ToWords());
            tempList.AddRange(line.ToWords());
            tempList.AddRange(column.ToWords());
            ushort opCode = 8;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExtension(LiteralString name)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(name.ToWords());
            ushort opCode = 10;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExtInstImport(IdResult resultId, LiteralString name)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(name.ToWords());
            ushort opCode = 11;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExtInst(IdResultType resultType, IdResult resultId, IdRef set, LiteralExtInstInteger instruction, params IdRef[] operand1Operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(set.ToWords());
            tempList.AddRange(instruction.ToWords());
            foreach(var el in operand1Operand2)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 12;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemoryModel(AddressingModel param0, MemoryModel param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(param0.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 14;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpEntryPoint(ExecutionModel param0, IdRef entryPoint, LiteralString name, params IdRef[] @interface)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(param0.ToWords());
            tempList.AddRange(entryPoint.ToWords());
            tempList.AddRange(name.ToWords());
            foreach(var el in @interface)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 15;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExecutionMode(IdRef entryPoint, ExecutionMode mode)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(entryPoint.ToWords());
            tempList.AddRange(mode.ToWords());
            ushort opCode = 16;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCapability(Capability capability)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(capability.ToWords());
            ushort opCode = 17;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeVoid(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 19;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeBool(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 20;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeInt(IdResult resultId, LiteralInteger width, LiteralInteger signedness)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(width.ToWords());
            tempList.AddRange(signedness.ToWords());
            ushort opCode = 21;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeFloat(IdResult resultId, LiteralInteger width)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(width.ToWords());
            ushort opCode = 22;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeVector(IdResult resultId, IdRef componentType, LiteralInteger componentCount)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(componentType.ToWords());
            tempList.AddRange(componentCount.ToWords());
            ushort opCode = 23;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeMatrix(IdResult resultId, IdRef columnType, LiteralInteger columnCount)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(columnType.ToWords());
            tempList.AddRange(columnCount.ToWords());
            ushort opCode = 24;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeImage(IdResult resultId, IdRef sampledType, Dim param2, LiteralInteger depth, LiteralInteger arrayed, LiteralInteger mS, LiteralInteger sampled, ImageFormat param7, AccessQualifier? param8 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledType.ToWords());
            tempList.AddRange(param2.ToWords());
            tempList.AddRange(depth.ToWords());
            tempList.AddRange(arrayed.ToWords());
            tempList.AddRange(mS.ToWords());
            tempList.AddRange(sampled.ToWords());
            tempList.AddRange(param7.ToWords());
            if(param8 is AccessQualifier param8NotNull)
                tempList.AddRange(param8NotNull.ToWords());
            ushort opCode = 25;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeSampler(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 26;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeSampledImage(IdResult resultId, IdRef imageType)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(imageType.ToWords());
            ushort opCode = 27;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeArray(IdResult resultId, IdRef elementType, IdRef length)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(elementType.ToWords());
            tempList.AddRange(length.ToWords());
            ushort opCode = 28;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeRuntimeArray(IdResult resultId, IdRef elementType)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(elementType.ToWords());
            ushort opCode = 29;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeStruct(IdResult resultId, params IdRef[] member0typemember1type)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            foreach(var el in member0typemember1type)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 30;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeOpaque(IdResult resultId, LiteralString thenameoftheopaquetype)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(thenameoftheopaquetype.ToWords());
            ushort opCode = 31;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypePointer(IdResult resultId, StorageClass param1, IdRef type)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(param1.ToWords());
            tempList.AddRange(type.ToWords());
            ushort opCode = 32;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeFunction(IdResult resultId, IdRef returnType, params IdRef[] parameter0TypeParameter1Type)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(returnType.ToWords());
            foreach(var el in parameter0TypeParameter1Type)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 33;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeEvent(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 34;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeDeviceEvent(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 35;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeReserveId(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 36;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeQueue(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 37;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypePipe(IdResult resultId, AccessQualifier qualifier)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(qualifier.ToWords());
            ushort opCode = 38;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeForwardPointer(IdRef pointerType, StorageClass param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointerType.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 39;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantTrue(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 41;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantFalse(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 42;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstant(IdResultType resultType, IdResult resultId, LiteralContextDependentNumber value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 43;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantComposite(IdResultType resultType, IdResult resultId, params IdRef[] constituents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            foreach(var el in constituents)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 44;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantSampler(IdResultType resultType, IdResult resultId, SamplerAddressingMode param2, LiteralInteger param, SamplerFilterMode param4)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(param2.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(param4.ToWords());
            ushort opCode = 45;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantNull(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 46;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstantTrue(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 48;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstantFalse(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 49;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstant(IdResultType resultType, IdResult resultId, LiteralContextDependentNumber value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 50;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstantComposite(IdResultType resultType, IdResult resultId, params IdRef[] constituents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            foreach(var el in constituents)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 51;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstantOp(IdResultType resultType, IdResult resultId, LiteralSpecConstantOpInteger opcode)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(opcode.ToWords());
            ushort opCode = 52;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFunction(IdResultType resultType, IdResult resultId, FunctionControl param2, IdRef functionType)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(param2.ToWords());
            tempList.AddRange(functionType.ToWords());
            ushort opCode = 54;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFunctionParameter(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 55;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFunctionEnd()
        {
            ushort opCode = 56;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpFunctionCall(IdResultType resultType, IdResult resultId, IdRef function, params IdRef[] argument0Argument1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(function.ToWords());
            foreach(var el in argument0Argument1)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 57;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVariable(IdResultType resultType, IdResult resultId, StorageClass param2, IdRef? initializer = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(param2.ToWords());
            if(initializer is IdRef initializerNotNull)
                tempList.AddRange(initializerNotNull.ToWords());
            ushort opCode = 59;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageTexelPointer(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef sample)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(sample.ToWords());
            ushort opCode = 60;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLoad(IdResultType resultType, IdResult resultId, IdRef pointer, MemoryAccess? param3 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            if(param3 is MemoryAccess param3NotNull)
                tempList.AddRange(param3NotNull.ToWords());
            ushort opCode = 61;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpStore(IdRef pointer, IdRef @object, MemoryAccess? param2 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(@object.ToWords());
            if(param2 is MemoryAccess param2NotNull)
                tempList.AddRange(param2NotNull.ToWords());
            ushort opCode = 62;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCopyMemory(IdRef target, IdRef source, MemoryAccess? param2 = null, MemoryAccess? param3 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(source.ToWords());
            if(param2 is MemoryAccess param2NotNull)
                tempList.AddRange(param2NotNull.ToWords());
            if(param3 is MemoryAccess param3NotNull)
                tempList.AddRange(param3NotNull.ToWords());
            ushort opCode = 63;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCopyMemorySized(IdRef target, IdRef source, IdRef size, MemoryAccess? param3 = null, MemoryAccess? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(source.ToWords());
            tempList.AddRange(size.ToWords());
            if(param3 is MemoryAccess param3NotNull)
                tempList.AddRange(param3NotNull.ToWords());
            if(param4 is MemoryAccess param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 64;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, params IdRef[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 65;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpInBoundsAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, params IdRef[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 66;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, IdRef element, params IdRef[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(element.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 67;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArrayLength(IdResultType resultType, IdResult resultId, IdRef structure, LiteralInteger arraymember)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(structure.ToWords());
            tempList.AddRange(arraymember.ToWords());
            ushort opCode = 68;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGenericPtrMemSemantics(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 69;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpInBoundsPtrAccessChain(IdResultType resultType, IdResult resultId, IdRef @base, IdRef element, params IdRef[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(element.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 70;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDecorate(IdRef target, Decoration param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 71;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemberDecorate(IdRef structureType, LiteralInteger member, Decoration param2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(structureType.ToWords());
            tempList.AddRange(member.ToWords());
            tempList.AddRange(param2.ToWords());
            ushort opCode = 72;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDecorationGroup(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 73;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupDecorate(IdRef decorationGroup, params IdRef[] targets)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(decorationGroup.ToWords());
            foreach(var el in targets)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 74;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupMemberDecorate(IdRef decorationGroup, params PairIdRefLiteralInteger[] targets)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(decorationGroup.ToWords());
            foreach(var el in targets)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 75;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVectorExtractDynamic(IdResultType resultType, IdResult resultId, IdRef vector, IdRef index)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            tempList.AddRange(index.ToWords());
            ushort opCode = 77;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVectorInsertDynamic(IdResultType resultType, IdResult resultId, IdRef vector, IdRef component, IdRef index)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            tempList.AddRange(component.ToWords());
            tempList.AddRange(index.ToWords());
            ushort opCode = 78;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVectorShuffle(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, params LiteralInteger[] components)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            foreach(var el in components)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 79;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCompositeConstruct(IdResultType resultType, IdResult resultId, params IdRef[] constituents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            foreach(var el in constituents)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 80;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCompositeExtract(IdResultType resultType, IdResult resultId, IdRef composite, params LiteralInteger[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(composite.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 81;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCompositeInsert(IdResultType resultType, IdResult resultId, IdRef @object, IdRef composite, params LiteralInteger[] indexes)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@object.ToWords());
            tempList.AddRange(composite.ToWords());
            foreach(var el in indexes)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 82;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCopyObject(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 83;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTranspose(IdResultType resultType, IdResult resultId, IdRef matrix)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(matrix.ToWords());
            ushort opCode = 84;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSampledImage(IdResultType resultType, IdResult resultId, IdRef image, IdRef sampler)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(sampler.ToWords());
            ushort opCode = 86;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 87;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(param4.ToWords());
            ushort opCode = 88;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 89;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            tempList.AddRange(param5.ToWords());
            ushort opCode = 90;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleProjImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 91;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleProjExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(param4.ToWords());
            ushort opCode = 92;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleProjDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 93;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleProjDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            tempList.AddRange(param5.ToWords());
            ushort opCode = 94;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageFetch(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 95;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef component, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(component.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 96;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageDrefGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 97;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageRead(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 98;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageWrite(IdRef image, IdRef coordinate, IdRef texel, ImageOperands? param3 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(texel.ToWords());
            if(param3 is ImageOperands param3NotNull)
                tempList.AddRange(param3NotNull.ToWords());
            ushort opCode = 99;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImage(IdResultType resultType, IdResult resultId, IdRef sampledImage)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            ushort opCode = 100;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQueryFormat(IdResultType resultType, IdResult resultId, IdRef image)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            ushort opCode = 101;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQueryOrder(IdResultType resultType, IdResult resultId, IdRef image)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            ushort opCode = 102;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQuerySizeLod(IdResultType resultType, IdResult resultId, IdRef image, IdRef levelofDetail)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(levelofDetail.ToWords());
            ushort opCode = 103;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQuerySize(IdResultType resultType, IdResult resultId, IdRef image)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            ushort opCode = 104;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQueryLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            ushort opCode = 105;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQueryLevels(IdResultType resultType, IdResult resultId, IdRef image)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            ushort opCode = 106;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageQuerySamples(IdResultType resultType, IdResult resultId, IdRef image)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            ushort opCode = 107;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertFToU(IdResultType resultType, IdResult resultId, IdRef floatValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(floatValue.ToWords());
            ushort opCode = 109;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertFToS(IdResultType resultType, IdResult resultId, IdRef floatValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(floatValue.ToWords());
            ushort opCode = 110;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertSToF(IdResultType resultType, IdResult resultId, IdRef signedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(signedValue.ToWords());
            ushort opCode = 111;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToF(IdResultType resultType, IdResult resultId, IdRef unsignedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(unsignedValue.ToWords());
            ushort opCode = 112;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUConvert(IdResultType resultType, IdResult resultId, IdRef unsignedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(unsignedValue.ToWords());
            ushort opCode = 113;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSConvert(IdResultType resultType, IdResult resultId, IdRef signedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(signedValue.ToWords());
            ushort opCode = 114;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFConvert(IdResultType resultType, IdResult resultId, IdRef floatValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(floatValue.ToWords());
            ushort opCode = 115;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpQuantizeToF16(IdResultType resultType, IdResult resultId, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 116;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertPtrToU(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 117;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSatConvertSToU(IdResultType resultType, IdResult resultId, IdRef signedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(signedValue.ToWords());
            ushort opCode = 118;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSatConvertUToS(IdResultType resultType, IdResult resultId, IdRef unsignedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(unsignedValue.ToWords());
            ushort opCode = 119;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToPtr(IdResultType resultType, IdResult resultId, IdRef integerValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(integerValue.ToWords());
            ushort opCode = 120;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrCastToGeneric(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 121;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGenericCastToPtr(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 122;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGenericCastToPtrExplicit(IdResultType resultType, IdResult resultId, IdRef pointer, StorageClass storage)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(storage.ToWords());
            ushort opCode = 123;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitcast(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 124;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSNegate(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 126;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFNegate(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 127;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIAdd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 128;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFAdd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 129;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpISub(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 130;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFSub(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 131;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIMul(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 132;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFMul(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 133;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 134;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 135;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFDiv(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 136;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 137;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSRem(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 138;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 139;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFRem(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 140;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFMod(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 141;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVectorTimesScalar(IdResultType resultType, IdResult resultId, IdRef vector, IdRef scalar)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            tempList.AddRange(scalar.ToWords());
            ushort opCode = 142;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMatrixTimesScalar(IdResultType resultType, IdResult resultId, IdRef matrix, IdRef scalar)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(matrix.ToWords());
            tempList.AddRange(scalar.ToWords());
            ushort opCode = 143;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVectorTimesMatrix(IdResultType resultType, IdResult resultId, IdRef vector, IdRef matrix)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            tempList.AddRange(matrix.ToWords());
            ushort opCode = 144;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMatrixTimesVector(IdResultType resultType, IdResult resultId, IdRef matrix, IdRef vector)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(matrix.ToWords());
            tempList.AddRange(vector.ToWords());
            ushort opCode = 145;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMatrixTimesMatrix(IdResultType resultType, IdResult resultId, IdRef leftMatrix, IdRef rightMatrix)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(leftMatrix.ToWords());
            tempList.AddRange(rightMatrix.ToWords());
            ushort opCode = 146;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpOuterProduct(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            ushort opCode = 147;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            ushort opCode = 148;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIAddCarry(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 149;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpISubBorrow(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 150;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUMulExtended(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 151;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSMulExtended(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 152;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAny(IdResultType resultType, IdResult resultId, IdRef vector)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            ushort opCode = 154;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAll(IdResultType resultType, IdResult resultId, IdRef vector)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector.ToWords());
            ushort opCode = 155;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsNan(IdResultType resultType, IdResult resultId, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 156;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsInf(IdResultType resultType, IdResult resultId, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 157;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsFinite(IdResultType resultType, IdResult resultId, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 158;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsNormal(IdResultType resultType, IdResult resultId, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 159;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSignBitSet(IdResultType resultType, IdResult resultId, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 160;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLessOrGreater(IdResultType resultType, IdResult resultId, IdRef x, IdRef y)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            tempList.AddRange(y.ToWords());
            ushort opCode = 161;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpOrdered(IdResultType resultType, IdResult resultId, IdRef x, IdRef y)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            tempList.AddRange(y.ToWords());
            ushort opCode = 162;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUnordered(IdResultType resultType, IdResult resultId, IdRef x, IdRef y)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(x.ToWords());
            tempList.AddRange(y.ToWords());
            ushort opCode = 163;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLogicalEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 164;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLogicalNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 165;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLogicalOr(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 166;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLogicalAnd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 167;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLogicalNot(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 168;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSelect(IdResultType resultType, IdResult resultId, IdRef condition, IdRef object1, IdRef object2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(condition.ToWords());
            tempList.AddRange(object1.ToWords());
            tempList.AddRange(object2.ToWords());
            ushort opCode = 169;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 170;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpINotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 171;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 172;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 173;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 174;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 175;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpULessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 176;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 177;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpULessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 178;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 179;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 180;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 181;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 182;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 183;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 184;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordLessThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 185;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 186;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordGreaterThan(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 187;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 188;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordLessThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 189;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFOrdGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 190;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFUnordGreaterThanEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 191;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpShiftRightLogical(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(shift.ToWords());
            ushort opCode = 194;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpShiftRightArithmetic(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(shift.ToWords());
            ushort opCode = 195;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpShiftLeftLogical(IdResultType resultType, IdResult resultId, IdRef @base, IdRef shift)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(shift.ToWords());
            ushort opCode = 196;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitwiseOr(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 197;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitwiseXor(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 198;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitwiseAnd(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 199;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpNot(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 200;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitFieldInsert(IdResultType resultType, IdResult resultId, IdRef @base, IdRef insert, IdRef offset, IdRef count)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(insert.ToWords());
            tempList.AddRange(offset.ToWords());
            tempList.AddRange(count.ToWords());
            ushort opCode = 201;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitFieldSExtract(IdResultType resultType, IdResult resultId, IdRef @base, IdRef offset, IdRef count)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(offset.ToWords());
            tempList.AddRange(count.ToWords());
            ushort opCode = 202;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitFieldUExtract(IdResultType resultType, IdResult resultId, IdRef @base, IdRef offset, IdRef count)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            tempList.AddRange(offset.ToWords());
            tempList.AddRange(count.ToWords());
            ushort opCode = 203;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitReverse(IdResultType resultType, IdResult resultId, IdRef @base)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            ushort opCode = 204;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBitCount(IdResultType resultType, IdResult resultId, IdRef @base)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@base.ToWords());
            ushort opCode = 205;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdx(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 207;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdy(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 208;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFwidth(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 209;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdxFine(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 210;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdyFine(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 211;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFwidthFine(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 212;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdxCoarse(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 213;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDPdyCoarse(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 214;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFwidthCoarse(IdResultType resultType, IdResult resultId, IdRef p)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(p.ToWords());
            ushort opCode = 215;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpEmitVertex()
        {
            ushort opCode = 218;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpEndPrimitive()
        {
            ushort opCode = 219;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpEmitStreamVertex(IdRef stream)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(stream.ToWords());
            ushort opCode = 220;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpEndStreamPrimitive(IdRef stream)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(stream.ToWords());
            ushort opCode = 221;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpControlBarrier(IdScope execution, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 224;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemoryBarrier(IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 225;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicLoad(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 227;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicStore(IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 228;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicExchange(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 229;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicCompareExchange(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics equal, IdMemorySemantics unequal, IdRef value, IdRef comparator)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(equal.ToWords());
            tempList.AddRange(unequal.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(comparator.ToWords());
            ushort opCode = 230;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicCompareExchangeWeak(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics equal, IdMemorySemantics unequal, IdRef value, IdRef comparator)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(equal.ToWords());
            tempList.AddRange(unequal.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(comparator.ToWords());
            ushort opCode = 231;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicIIncrement(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 232;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicIDecrement(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 233;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicIAdd(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 234;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicISub(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 235;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicSMin(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 236;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicUMin(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 237;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicSMax(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 238;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicUMax(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 239;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicAnd(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 240;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicOr(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 241;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicXor(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 242;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPhi(IdResultType resultType, IdResult resultId, params PairIdRefIdRef[] variableParent)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            foreach(var el in variableParent)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 245;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLoopMerge(IdRef mergeBlock, IdRef continueTarget, LoopControl param2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(mergeBlock.ToWords());
            tempList.AddRange(continueTarget.ToWords());
            tempList.AddRange(param2.ToWords());
            ushort opCode = 246;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSelectionMerge(IdRef mergeBlock, SelectionControl param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(mergeBlock.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 247;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLabel(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 248;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBranch(IdRef targetLabel)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(targetLabel.ToWords());
            ushort opCode = 249;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBranchConditional(IdRef condition, IdRef trueLabel, IdRef falseLabel, params LiteralInteger[] branchweights)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(condition.ToWords());
            tempList.AddRange(trueLabel.ToWords());
            tempList.AddRange(falseLabel.ToWords());
            foreach(var el in branchweights)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 250;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSwitch(IdRef selector, IdRef @default, params PairLiteralIntegerIdRef[] target)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(selector.ToWords());
            tempList.AddRange(@default.ToWords());
            foreach(var el in target)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 251;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpKill()
        {
            ushort opCode = 252;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpReturn()
        {
            ushort opCode = 253;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpReturnValue(IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(value.ToWords());
            ushort opCode = 254;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUnreachable()
        {
            ushort opCode = 255;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpLifetimeStart(IdRef pointer, LiteralInteger size)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(size.ToWords());
            ushort opCode = 256;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLifetimeStop(IdRef pointer, LiteralInteger size)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(size.ToWords());
            ushort opCode = 257;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupAsyncCopy(IdResultType resultType, IdResult resultId, IdScope execution, IdRef destination, IdRef source, IdRef numElements, IdRef stride, IdRef @event)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(destination.ToWords());
            tempList.AddRange(source.ToWords());
            tempList.AddRange(numElements.ToWords());
            tempList.AddRange(stride.ToWords());
            tempList.AddRange(@event.ToWords());
            ushort opCode = 259;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupWaitEvents(IdScope execution, IdRef numEvents, IdRef eventsList)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(numEvents.ToWords());
            tempList.AddRange(eventsList.ToWords());
            ushort opCode = 260;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupAll(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 261;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupAny(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 262;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef localId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(localId.ToWords());
            ushort opCode = 263;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupIAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 264;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 265;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 266;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupUMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 267;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupSMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 268;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 269;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupUMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 270;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupSMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 271;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReadPipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef pointer, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 274;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpWritePipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef pointer, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 275;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReservedReadPipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef reserveId, IdRef index, IdRef pointer, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(index.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 276;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReservedWritePipe(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef reserveId, IdRef index, IdRef pointer, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(index.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 277;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReserveReadPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(numPackets.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 278;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReserveWritePipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(numPackets.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 279;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCommitReadPipe(IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 280;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCommitWritePipe(IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 281;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsValidReserveId(IdResultType resultType, IdResult resultId, IdRef reserveId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(reserveId.ToWords());
            ushort opCode = 282;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetNumPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 283;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetMaxPipePackets(IdResultType resultType, IdResult resultId, IdRef pipe, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 284;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupReserveReadPipePackets(IdResultType resultType, IdResult resultId, IdScope execution, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(numPackets.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 285;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupReserveWritePipePackets(IdResultType resultType, IdResult resultId, IdScope execution, IdRef pipe, IdRef numPackets, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(numPackets.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 286;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupCommitReadPipe(IdScope execution, IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 287;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupCommitWritePipe(IdScope execution, IdRef pipe, IdRef reserveId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(pipe.ToWords());
            tempList.AddRange(reserveId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 288;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpEnqueueMarker(IdResultType resultType, IdResult resultId, IdRef queue, IdRef numEvents, IdRef waitEvents, IdRef retEvent)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(queue.ToWords());
            tempList.AddRange(numEvents.ToWords());
            tempList.AddRange(waitEvents.ToWords());
            tempList.AddRange(retEvent.ToWords());
            ushort opCode = 291;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpEnqueueKernel(IdResultType resultType, IdResult resultId, IdRef queue, IdRef flags, IdRef nDRange, IdRef numEvents, IdRef waitEvents, IdRef retEvent, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign, params IdRef[] localSize)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(queue.ToWords());
            tempList.AddRange(flags.ToWords());
            tempList.AddRange(nDRange.ToWords());
            tempList.AddRange(numEvents.ToWords());
            tempList.AddRange(waitEvents.ToWords());
            tempList.AddRange(retEvent.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            foreach(var el in localSize)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 292;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelNDrangeSubGroupCount(IdResultType resultType, IdResult resultId, IdRef nDRange, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(nDRange.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 293;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelNDrangeMaxSubGroupSize(IdResultType resultType, IdResult resultId, IdRef nDRange, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(nDRange.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 294;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelWorkGroupSize(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 295;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelPreferredWorkGroupSizeMultiple(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 296;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRetainEvent(IdRef @event)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(@event.ToWords());
            ushort opCode = 297;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReleaseEvent(IdRef @event)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(@event.ToWords());
            ushort opCode = 298;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCreateUserEvent(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 299;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIsValidEvent(IdResultType resultType, IdResult resultId, IdRef @event)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(@event.ToWords());
            ushort opCode = 300;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSetUserEventStatus(IdRef @event, IdRef status)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(@event.ToWords());
            tempList.AddRange(status.ToWords());
            ushort opCode = 301;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCaptureEventProfilingInfo(IdRef @event, IdRef profilingInfo, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(@event.ToWords());
            tempList.AddRange(profilingInfo.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 302;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetDefaultQueue(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 303;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBuildNDRange(IdResultType resultType, IdResult resultId, IdRef globalWorkSize, IdRef localWorkSize, IdRef globalWorkOffset)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(globalWorkSize.ToWords());
            tempList.AddRange(localWorkSize.ToWords());
            tempList.AddRange(globalWorkOffset.ToWords());
            ushort opCode = 304;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 305;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(param4.ToWords());
            ushort opCode = 306;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 307;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            tempList.AddRange(param5.ToWords());
            ushort opCode = 308;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleProjImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 309;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleProjExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, ImageOperands param4)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(param4.ToWords());
            ushort opCode = 310;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleProjDrefImplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 311;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseSampleProjDrefExplicitLod(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands param5)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            tempList.AddRange(param5.ToWords());
            ushort opCode = 312;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseFetch(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 313;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef component, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(component.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 314;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseDrefGather(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef dref, ImageOperands? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(dref.ToWords());
            if(param5 is ImageOperands param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 315;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseTexelsResident(IdResultType resultType, IdResult resultId, IdRef residentCode)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(residentCode.ToWords());
            ushort opCode = 316;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpNoLine()
        {
            ushort opCode = 317;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpAtomicFlagTestAndSet(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 318;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicFlagClear(IdRef pointer, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 319;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSparseRead(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, ImageOperands? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            if(param4 is ImageOperands param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 320;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSizeOf(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 321;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypePipeStorage(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 322;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantPipeStorage(IdResultType resultType, IdResult resultId, LiteralInteger packetSize, LiteralInteger packetAlignment, LiteralInteger capacity)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            tempList.AddRange(capacity.ToWords());
            ushort opCode = 323;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCreatePipeFromPipeStorage(IdResultType resultType, IdResult resultId, IdRef pipeStorage)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pipeStorage.ToWords());
            ushort opCode = 324;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelLocalSizeForSubgroupCount(IdResultType resultType, IdResult resultId, IdRef subgroupCount, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(subgroupCount.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 325;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGetKernelMaxNumSubgroups(IdResultType resultType, IdResult resultId, IdRef invoke, IdRef param, IdRef paramSize, IdRef paramAlign)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(invoke.ToWords());
            tempList.AddRange(param.ToWords());
            tempList.AddRange(paramSize.ToWords());
            tempList.AddRange(paramAlign.ToWords());
            ushort opCode = 326;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeNamedBarrier(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 327;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpNamedBarrierInitialize(IdResultType resultType, IdResult resultId, IdRef subgroupCount)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(subgroupCount.ToWords());
            ushort opCode = 328;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemoryNamedBarrier(IdRef namedBarrier, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(namedBarrier.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 329;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpModuleProcessed(LiteralString process)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(process.ToWords());
            ushort opCode = 330;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExecutionModeId(IdRef entryPoint, ExecutionMode mode)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(entryPoint.ToWords());
            tempList.AddRange(mode.ToWords());
            ushort opCode = 331;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDecorateId(IdRef target, Decoration param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 332;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformElect(IdResultType resultType, IdResult resultId, IdScope execution)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            ushort opCode = 333;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformAll(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformAny(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 335;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformAllEqual(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 336;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef id)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(id.ToWords());
            ushort opCode = 337;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBroadcastFirst(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 338;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBallot(IdResultType resultType, IdResult resultId, IdScope execution, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 339;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformInverseBallot(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 340;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBallotBitExtract(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef index)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(index.ToWords());
            ushort opCode = 341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBallotBitCount(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 342;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBallotFindLSB(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 343;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBallotFindMSB(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 344;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformShuffle(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef id)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(id.ToWords());
            ushort opCode = 345;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformShuffleXor(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef mask)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(mask.ToWords());
            ushort opCode = 346;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformShuffleUp(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(delta.ToWords());
            ushort opCode = 347;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformShuffleDown(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(delta.ToWords());
            ushort opCode = 348;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformIAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 349;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformFAdd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 350;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformIMul(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 351;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformFMul(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 352;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformSMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 353;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformUMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 354;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformFMin(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 355;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformSMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 356;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformUMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 357;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformFMax(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 358;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBitwiseAnd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 359;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBitwiseOr(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 360;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformBitwiseXor(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 361;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformLogicalAnd(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 362;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformLogicalOr(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 363;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformLogicalXor(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef value, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(value.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 364;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformQuadBroadcast(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef index)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(index.ToWords());
            ushort opCode = 365;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformQuadSwap(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef direction)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(direction.ToWords());
            ushort opCode = 366;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCopyLogical(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 400;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 401;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrNotEqual(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 402;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrDiff(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 403;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTerminateInvocation()
        {
            ushort opCode = 4416;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpSubgroupBallotKHR(IdResultType resultType, IdResult resultId, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 4421;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupFirstInvocationKHR(IdResultType resultType, IdResult resultId, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 4422;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAllKHR(IdResultType resultType, IdResult resultId, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 4428;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAnyKHR(IdResultType resultType, IdResult resultId, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 4429;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAllEqualKHR(IdResultType resultType, IdResult resultId, IdRef predicate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(predicate.ToWords());
            ushort opCode = 4430;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformRotateKHR(IdResultType resultType, IdResult resultId, IdScope execution, IdRef value, IdRef delta, IdRef? clusterSize = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(delta.ToWords());
            if(clusterSize is IdRef clusterSizeNotNull)
                tempList.AddRange(clusterSizeNotNull.ToWords());
            ushort opCode = 4431;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupReadInvocationKHR(IdResultType resultType, IdResult resultId, IdRef value, IdRef index)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(index.ToWords());
            ushort opCode = 4432;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTraceRayKHR(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(accel.ToWords());
            tempList.AddRange(rayFlags.ToWords());
            tempList.AddRange(cullMask.ToWords());
            tempList.AddRange(sBTOffset.ToWords());
            tempList.AddRange(sBTStride.ToWords());
            tempList.AddRange(missIndex.ToWords());
            tempList.AddRange(rayOrigin.ToWords());
            tempList.AddRange(rayTmin.ToWords());
            tempList.AddRange(rayDirection.ToWords());
            tempList.AddRange(rayTmax.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 4445;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExecuteCallableKHR(IdRef sBTIndex, IdRef callableData)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(sBTIndex.ToWords());
            tempList.AddRange(callableData.ToWords());
            ushort opCode = 4446;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToAccelerationStructureKHR(IdResultType resultType, IdResult resultId, IdRef accel)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(accel.ToWords());
            ushort opCode = 4447;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIgnoreIntersectionKHR()
        {
            ushort opCode = 4448;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpTerminateRayKHR()
        {
            ushort opCode = 4449;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpSDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4450;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4450;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4451;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4451;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSUDot(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4452;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSUDotKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4452;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4453;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4453;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4454;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4454;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSUDotAccSat(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4455;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSUDotAccSatKHR(IdResultType resultType, IdResult resultId, IdRef vector1, IdRef vector2, IdRef accumulator, PackedVectorFormat? packedVectorFormat = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(vector1.ToWords());
            tempList.AddRange(vector2.ToWords());
            tempList.AddRange(accumulator.ToWords());
            if(packedVectorFormat is PackedVectorFormat packedVectorFormatNotNull)
                tempList.AddRange(packedVectorFormatNotNull.ToWords());
            ushort opCode = 4455;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeRayQueryKHR(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 4472;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryInitializeKHR(IdRef rayQuery, IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef rayOrigin, IdRef rayTMin, IdRef rayDirection, IdRef rayTMax)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(accel.ToWords());
            tempList.AddRange(rayFlags.ToWords());
            tempList.AddRange(cullMask.ToWords());
            tempList.AddRange(rayOrigin.ToWords());
            tempList.AddRange(rayTMin.ToWords());
            tempList.AddRange(rayDirection.ToWords());
            tempList.AddRange(rayTMax.ToWords());
            ushort opCode = 4473;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryTerminateKHR(IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 4474;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGenerateIntersectionKHR(IdRef rayQuery, IdRef hitT)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(hitT.ToWords());
            ushort opCode = 4475;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryConfirmIntersectionKHR(IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 4476;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryProceedKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 4477;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionTypeKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 4479;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupIAddNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5000;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFAddNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5001;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5002;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupUMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5003;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupSMinNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5004;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5005;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupUMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5006;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupSMaxNonUniformAMD(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 5007;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFragmentMaskFetchAMD(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            ushort opCode = 5011;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFragmentFetchAMD(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef fragmentIndex)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(fragmentIndex.ToWords());
            ushort opCode = 5012;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReadClockKHR(IdResultType resultType, IdResult resultId, IdScope scope)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(scope.ToWords());
            ushort opCode = 5056;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpImageSampleFootprintNV(IdResultType resultType, IdResult resultId, IdRef sampledImage, IdRef coordinate, IdRef granularity, IdRef coarse, ImageOperands? param6 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sampledImage.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(granularity.ToWords());
            tempList.AddRange(coarse.ToWords());
            if(param6 is ImageOperands param6NotNull)
                tempList.AddRange(param6NotNull.ToWords());
            ushort opCode = 5283;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupNonUniformPartitionNV(IdResultType resultType, IdResult resultId, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 5296;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpWritePackedPrimitiveIndices4x8NV(IdRef indexOffset, IdRef packedIndices)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(indexOffset.ToWords());
            tempList.AddRange(packedIndices.ToWords());
            ushort opCode = 5299;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReportIntersectionNV(IdResultType resultType, IdResult resultId, IdRef hit, IdRef hitKind)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(hit.ToWords());
            tempList.AddRange(hitKind.ToWords());
            ushort opCode = 5334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReportIntersectionKHR(IdResultType resultType, IdResult resultId, IdRef hit, IdRef hitKind)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(hit.ToWords());
            tempList.AddRange(hitKind.ToWords());
            ushort opCode = 5334;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIgnoreIntersectionNV()
        {
            ushort opCode = 5335;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpTerminateRayNV()
        {
            ushort opCode = 5336;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpTraceNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef payloadId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(accel.ToWords());
            tempList.AddRange(rayFlags.ToWords());
            tempList.AddRange(cullMask.ToWords());
            tempList.AddRange(sBTOffset.ToWords());
            tempList.AddRange(sBTStride.ToWords());
            tempList.AddRange(missIndex.ToWords());
            tempList.AddRange(rayOrigin.ToWords());
            tempList.AddRange(rayTmin.ToWords());
            tempList.AddRange(rayDirection.ToWords());
            tempList.AddRange(rayTmax.ToWords());
            tempList.AddRange(payloadId.ToWords());
            ushort opCode = 5337;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTraceMotionNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef time, IdRef payloadId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(accel.ToWords());
            tempList.AddRange(rayFlags.ToWords());
            tempList.AddRange(cullMask.ToWords());
            tempList.AddRange(sBTOffset.ToWords());
            tempList.AddRange(sBTStride.ToWords());
            tempList.AddRange(missIndex.ToWords());
            tempList.AddRange(rayOrigin.ToWords());
            tempList.AddRange(rayTmin.ToWords());
            tempList.AddRange(rayDirection.ToWords());
            tempList.AddRange(rayTmax.ToWords());
            tempList.AddRange(time.ToWords());
            tempList.AddRange(payloadId.ToWords());
            ushort opCode = 5338;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTraceRayMotionNV(IdRef accel, IdRef rayFlags, IdRef cullMask, IdRef sBTOffset, IdRef sBTStride, IdRef missIndex, IdRef rayOrigin, IdRef rayTmin, IdRef rayDirection, IdRef rayTmax, IdRef time, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(accel.ToWords());
            tempList.AddRange(rayFlags.ToWords());
            tempList.AddRange(cullMask.ToWords());
            tempList.AddRange(sBTOffset.ToWords());
            tempList.AddRange(sBTStride.ToWords());
            tempList.AddRange(missIndex.ToWords());
            tempList.AddRange(rayOrigin.ToWords());
            tempList.AddRange(rayTmin.ToWords());
            tempList.AddRange(rayDirection.ToWords());
            tempList.AddRange(rayTmax.ToWords());
            tempList.AddRange(time.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5339;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAccelerationStructureNV(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAccelerationStructureKHR(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5341;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExecuteCallableNV(IdRef sBTIndex, IdRef callableDataId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(sBTIndex.ToWords());
            tempList.AddRange(callableDataId.ToWords());
            ushort opCode = 5344;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeCooperativeMatrixNV(IdResult resultId, IdRef componentType, IdScope execution, IdRef rows, IdRef columns)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(componentType.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(rows.ToWords());
            tempList.AddRange(columns.ToWords());
            ushort opCode = 5358;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCooperativeMatrixLoadNV(IdResultType resultType, IdResult resultId, IdRef pointer, IdRef stride, IdRef columnMajor, MemoryAccess? param5 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(stride.ToWords());
            tempList.AddRange(columnMajor.ToWords());
            if(param5 is MemoryAccess param5NotNull)
                tempList.AddRange(param5NotNull.ToWords());
            ushort opCode = 5359;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCooperativeMatrixStoreNV(IdRef pointer, IdRef @object, IdRef stride, IdRef columnMajor, MemoryAccess? param4 = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(@object.ToWords());
            tempList.AddRange(stride.ToWords());
            tempList.AddRange(columnMajor.ToWords());
            if(param4 is MemoryAccess param4NotNull)
                tempList.AddRange(param4NotNull.ToWords());
            ushort opCode = 5360;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCooperativeMatrixMulAddNV(IdResultType resultType, IdResult resultId, IdRef a, IdRef b, IdRef c)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(c.ToWords());
            ushort opCode = 5361;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCooperativeMatrixLengthNV(IdResultType resultType, IdResult resultId, IdRef type)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(type.ToWords());
            ushort opCode = 5362;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpBeginInvocationInterlockEXT()
        {
            ushort opCode = 5364;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpEndInvocationInterlockEXT()
        {
            ushort opCode = 5365;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpDemoteToHelperInvocation()
        {
            ushort opCode = 5380;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpDemoteToHelperInvocationEXT()
        {
            ushort opCode = 5380;
            ushort wordCount = 0;
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
        }

        public void GenerateOpIsHelperInvocationEXT(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5381;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToImageNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5391;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToSamplerNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5392;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertImageToUNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5393;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertSamplerToUNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5394;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertUToSampledImageNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5395;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConvertSampledImageToUNV(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5396;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSamplerImageAddressingModeNV(LiteralInteger bitWidth)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(bitWidth.ToWords());
            ushort opCode = 5397;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupShuffleINTEL(IdResultType resultType, IdResult resultId, IdRef data, IdRef invocationId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(data.ToWords());
            tempList.AddRange(invocationId.ToWords());
            ushort opCode = 5571;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupShuffleDownINTEL(IdResultType resultType, IdResult resultId, IdRef current, IdRef next, IdRef delta)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(current.ToWords());
            tempList.AddRange(next.ToWords());
            tempList.AddRange(delta.ToWords());
            ushort opCode = 5572;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupShuffleUpINTEL(IdResultType resultType, IdResult resultId, IdRef previous, IdRef current, IdRef delta)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(previous.ToWords());
            tempList.AddRange(current.ToWords());
            tempList.AddRange(delta.ToWords());
            ushort opCode = 5573;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupShuffleXorINTEL(IdResultType resultType, IdResult resultId, IdRef data, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(data.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 5574;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef ptr)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(ptr.ToWords());
            ushort opCode = 5575;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupBlockWriteINTEL(IdRef ptr, IdRef data)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(ptr.ToWords());
            tempList.AddRange(data.ToWords());
            ushort opCode = 5576;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupImageBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            ushort opCode = 5577;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupImageBlockWriteINTEL(IdRef image, IdRef coordinate, IdRef data)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(data.ToWords());
            ushort opCode = 5578;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupImageMediaBlockReadINTEL(IdResultType resultType, IdResult resultId, IdRef image, IdRef coordinate, IdRef width, IdRef height)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(width.ToWords());
            tempList.AddRange(height.ToWords());
            ushort opCode = 5580;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupImageMediaBlockWriteINTEL(IdRef image, IdRef coordinate, IdRef width, IdRef height, IdRef data)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(image.ToWords());
            tempList.AddRange(coordinate.ToWords());
            tempList.AddRange(width.ToWords());
            tempList.AddRange(height.ToWords());
            tempList.AddRange(data.ToWords());
            ushort opCode = 5581;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUCountLeadingZerosINTEL(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5585;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUCountTrailingZerosINTEL(IdResultType resultType, IdResult resultId, IdRef operand)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand.ToWords());
            ushort opCode = 5586;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAbsISubINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5587;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAbsUSubINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5588;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIAddSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5589;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUAddSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5590;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIAverageINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5591;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUAverageINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5592;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIAverageRoundedINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5593;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUAverageRoundedINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5594;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpISubSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5595;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUSubSatINTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5596;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpIMul32x16INTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5597;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpUMul32x16INTEL(IdResultType resultType, IdResult resultId, IdRef operand1, IdRef operand2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(operand1.ToWords());
            tempList.AddRange(operand2.ToWords());
            ushort opCode = 5598;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantFunctionPointerINTEL(IdResultType resultType, IdResult resultId, IdRef function)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(function.ToWords());
            ushort opCode = 5600;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFunctionPointerCallINTEL(IdResultType resultType, IdResult resultId, params IdRef[] operand1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            foreach(var el in operand1)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 5601;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAsmTargetINTEL(IdResultType resultType, IdResult resultId, LiteralString asmtarget)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(asmtarget.ToWords());
            ushort opCode = 5609;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAsmINTEL(IdResultType resultType, IdResult resultId, IdRef asmtype, IdRef target, LiteralString asminstructions, LiteralString constraints)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(asmtype.ToWords());
            tempList.AddRange(target.ToWords());
            tempList.AddRange(asminstructions.ToWords());
            tempList.AddRange(constraints.ToWords());
            ushort opCode = 5610;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAsmCallINTEL(IdResultType resultType, IdResult resultId, IdRef asm, params IdRef[] argument0)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(asm.ToWords());
            foreach(var el in argument0)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 5611;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicFMinEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 5614;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicFMaxEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 5615;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAssumeTrueKHR(IdRef condition)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(condition.ToWords());
            ushort opCode = 5630;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpExpectKHR(IdResultType resultType, IdResult resultId, IdRef value, IdRef expectedValue)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(value.ToWords());
            tempList.AddRange(expectedValue.ToWords());
            ushort opCode = 5631;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDecorateString(IdRef target, Decoration param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 5632;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpDecorateStringGOOGLE(IdRef target, Decoration param1)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(target.ToWords());
            tempList.AddRange(param1.ToWords());
            ushort opCode = 5632;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemberDecorateString(IdRef structType, LiteralInteger member, Decoration param2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(structType.ToWords());
            tempList.AddRange(member.ToWords());
            tempList.AddRange(param2.ToWords());
            ushort opCode = 5633;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpMemberDecorateStringGOOGLE(IdRef structType, LiteralInteger member, Decoration param2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(structType.ToWords());
            tempList.AddRange(member.ToWords());
            tempList.AddRange(param2.ToWords());
            ushort opCode = 5633;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVmeImageINTEL(IdResultType resultType, IdResult resultId, IdRef imageType, IdRef sampler)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(imageType.ToWords());
            tempList.AddRange(sampler.ToWords());
            ushort opCode = 5699;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeVmeImageINTEL(IdResult resultId, IdRef imageType)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(imageType.ToWords());
            ushort opCode = 5700;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImePayloadINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5701;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcRefPayloadINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5702;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcSicPayloadINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5703;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcMcePayloadINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5704;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcMceResultINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5705;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImeResultINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5706;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImeResultSingleReferenceStreamoutINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5707;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImeResultDualReferenceStreamoutINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5708;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImeSingleReferenceStreaminINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5709;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcImeDualReferenceStreaminINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5710;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcRefResultINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5711;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeAvcSicResultINTEL(IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5712;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultInterBaseMultiReferencePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5713;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetInterBaseMultiReferencePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef referenceBasePenalty, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(referenceBasePenalty.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5714;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultInterShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5715;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetInterShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef packedShapePenalty, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedShapePenalty.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5716;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultInterDirectionPenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5717;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetInterDirectionPenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef directionCost, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(directionCost.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5718;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5719;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultInterMotionVectorCostTableINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5720;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultHighPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5721;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultMediumPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5722;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultLowPenaltyCostTableINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5723;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetMotionVectorCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef packedCostCenterDelta, IdRef packedCostTable, IdRef costPrecision, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedCostCenterDelta.ToWords());
            tempList.AddRange(packedCostTable.ToWords());
            tempList.AddRange(costPrecision.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5724;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultIntraLumaModePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef sliceType, IdRef qp)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sliceType.ToWords());
            tempList.AddRange(qp.ToWords());
            ushort opCode = 5725;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultNonDcLumaIntraPenaltyINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5726;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetDefaultIntraChromaModeBasePenaltyINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5727;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetAcOnlyHaarINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5728;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetSourceInterlacedFieldPolarityINTEL(IdResultType resultType, IdResult resultId, IdRef sourceFieldPolarity, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(sourceFieldPolarity.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5729;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetSingleReferenceInterlacedFieldPolarityINTEL(IdResultType resultType, IdResult resultId, IdRef referenceFieldPolarity, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(referenceFieldPolarity.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5730;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceSetDualReferenceInterlacedFieldPolaritiesINTEL(IdResultType resultType, IdResult resultId, IdRef forwardReferenceFieldPolarity, IdRef backwardReferenceFieldPolarity, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(forwardReferenceFieldPolarity.ToWords());
            tempList.AddRange(backwardReferenceFieldPolarity.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5731;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToImePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5732;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToImeResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5733;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToRefPayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5734;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToRefResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5735;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToSicPayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5736;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceConvertToSicResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5737;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5738;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5739;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetBestInterDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5740;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterMajorShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5741;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterMinorShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5742;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterDirectionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5743;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterMotionVectorCountINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5744;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5745;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcMceGetInterReferenceInterlacedFieldPolaritiesINTEL(IdResultType resultType, IdResult resultId, IdRef packedReferenceIds, IdRef packedReferenceParameterFieldPolarities, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedReferenceIds.ToWords());
            tempList.AddRange(packedReferenceParameterFieldPolarities.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5746;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef partitionMask, IdRef sADAdjustment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcCoord.ToWords());
            tempList.AddRange(partitionMask.ToWords());
            tempList.AddRange(sADAdjustment.ToWords());
            ushort opCode = 5747;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef refOffset, IdRef searchWindowConfig, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(refOffset.ToWords());
            tempList.AddRange(searchWindowConfig.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5748;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef fwdRefOffset, IdRef bwdRefOffset, IdRef idSearchWindowConfig, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(fwdRefOffset.ToWords());
            tempList.AddRange(bwdRefOffset.ToWords());
            tempList.AddRange(idSearchWindowConfig.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5749;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeRefWindowSizeINTEL(IdResultType resultType, IdResult resultId, IdRef searchWindowConfig, IdRef dualRef)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(searchWindowConfig.ToWords());
            tempList.AddRange(dualRef.ToWords());
            ushort opCode = 5750;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeAdjustRefOffsetINTEL(IdResultType resultType, IdResult resultId, IdRef refOffset, IdRef srcCoord, IdRef refWindowSize, IdRef imageSize)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(refOffset.ToWords());
            tempList.AddRange(srcCoord.ToWords());
            tempList.AddRange(refWindowSize.ToWords());
            tempList.AddRange(imageSize.ToWords());
            ushort opCode = 5751;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5752;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetMaxMotionVectorCountINTEL(IdResultType resultType, IdResult resultId, IdRef maxMotionVectorCount, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(maxMotionVectorCount.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5753;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetUnidirectionalMixDisableINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5754;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetEarlySearchTerminationThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef threshold, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(threshold.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5755;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeSetWeightedSadINTEL(IdResultType resultType, IdResult resultId, IdRef packedSadWeights, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedSadWeights.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5756;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5757;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5758;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload, IdRef streaminComponents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(streaminComponents.ToWords());
            ushort opCode = 5759;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload, IdRef streaminComponents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(streaminComponents.ToWords());
            ushort opCode = 5760;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5761;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5762;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithSingleReferenceStreaminoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload, IdRef streaminComponents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(streaminComponents.ToWords());
            ushort opCode = 5763;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeEvaluateWithDualReferenceStreaminoutINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload, IdRef streaminComponents)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(streaminComponents.ToWords());
            ushort opCode = 5764;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5765;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetSingleReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5766;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetDualReferenceStreaminINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5767;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeStripSingleReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5768;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeStripDualReferenceStreamoutINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5769;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            ushort opCode = 5770;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            ushort opCode = 5771;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutSingleReferenceMajorShapeReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            ushort opCode = 5772;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeMotionVectorsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            tempList.AddRange(direction.ToWords());
            ushort opCode = 5773;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeDistortionsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            tempList.AddRange(direction.ToWords());
            ushort opCode = 5774;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetStreamoutDualReferenceMajorShapeReferenceIdsINTEL(IdResultType resultType, IdResult resultId, IdRef payload, IdRef majorShape, IdRef direction)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            tempList.AddRange(majorShape.ToWords());
            tempList.AddRange(direction.ToWords());
            ushort opCode = 5775;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetBorderReachedINTEL(IdResultType resultType, IdResult resultId, IdRef imageSelect, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(imageSelect.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5776;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetTruncatedSearchIndicationINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5777;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetUnidirectionalEarlySearchTerminationINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5778;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumMotionVectorINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5779;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcImeGetWeightingPatternMinimumDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5780;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcFmeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef motionVectors, IdRef majorShapes, IdRef minorShapes, IdRef direction, IdRef pixelResolution, IdRef sadAdjustment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcCoord.ToWords());
            tempList.AddRange(motionVectors.ToWords());
            tempList.AddRange(majorShapes.ToWords());
            tempList.AddRange(minorShapes.ToWords());
            tempList.AddRange(direction.ToWords());
            tempList.AddRange(pixelResolution.ToWords());
            tempList.AddRange(sadAdjustment.ToWords());
            ushort opCode = 5781;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcBmeInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord, IdRef motionVectors, IdRef majorShapes, IdRef minorShapes, IdRef direction, IdRef pixelResolution, IdRef bidirectionalWeight, IdRef sadAdjustment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcCoord.ToWords());
            tempList.AddRange(motionVectors.ToWords());
            tempList.AddRange(majorShapes.ToWords());
            tempList.AddRange(minorShapes.ToWords());
            tempList.AddRange(direction.ToWords());
            tempList.AddRange(pixelResolution.ToWords());
            tempList.AddRange(bidirectionalWeight.ToWords());
            tempList.AddRange(sadAdjustment.ToWords());
            ushort opCode = 5782;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5783;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefSetBidirectionalMixDisableINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5784;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefSetBilinearFilterEnableINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5785;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5786;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5787;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(packedReferenceIds.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5788;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefEvaluateWithMultiReferenceInterlacedINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef packedReferenceFieldPolarities, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(packedReferenceIds.ToWords());
            tempList.AddRange(packedReferenceFieldPolarities.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5789;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcRefConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5790;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicInitializeINTEL(IdResultType resultType, IdResult resultId, IdRef srcCoord)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcCoord.ToWords());
            ushort opCode = 5791;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicConfigureSkcINTEL(IdResultType resultType, IdResult resultId, IdRef skipBlockPartitionType, IdRef skipMotionVectorMask, IdRef motionVectors, IdRef bidirectionalWeight, IdRef sadAdjustment, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(skipBlockPartitionType.ToWords());
            tempList.AddRange(skipMotionVectorMask.ToWords());
            tempList.AddRange(motionVectors.ToWords());
            tempList.AddRange(bidirectionalWeight.ToWords());
            tempList.AddRange(sadAdjustment.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5792;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicConfigureIpeLumaINTEL(IdResultType resultType, IdResult resultId, IdRef lumaIntraPartitionMask, IdRef intraNeighbourAvailabilty, IdRef leftEdgeLumaPixels, IdRef upperLeftCornerLumaPixel, IdRef upperEdgeLumaPixels, IdRef upperRightEdgeLumaPixels, IdRef sadAdjustment, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(lumaIntraPartitionMask.ToWords());
            tempList.AddRange(intraNeighbourAvailabilty.ToWords());
            tempList.AddRange(leftEdgeLumaPixels.ToWords());
            tempList.AddRange(upperLeftCornerLumaPixel.ToWords());
            tempList.AddRange(upperEdgeLumaPixels.ToWords());
            tempList.AddRange(upperRightEdgeLumaPixels.ToWords());
            tempList.AddRange(sadAdjustment.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5793;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicConfigureIpeLumaChromaINTEL(IdResultType resultType, IdResult resultId, IdRef lumaIntraPartitionMask, IdRef intraNeighbourAvailabilty, IdRef leftEdgeLumaPixels, IdRef upperLeftCornerLumaPixel, IdRef upperEdgeLumaPixels, IdRef upperRightEdgeLumaPixels, IdRef leftEdgeChromaPixels, IdRef upperLeftCornerChromaPixel, IdRef upperEdgeChromaPixels, IdRef sadAdjustment, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(lumaIntraPartitionMask.ToWords());
            tempList.AddRange(intraNeighbourAvailabilty.ToWords());
            tempList.AddRange(leftEdgeLumaPixels.ToWords());
            tempList.AddRange(upperLeftCornerLumaPixel.ToWords());
            tempList.AddRange(upperEdgeLumaPixels.ToWords());
            tempList.AddRange(upperRightEdgeLumaPixels.ToWords());
            tempList.AddRange(leftEdgeChromaPixels.ToWords());
            tempList.AddRange(upperLeftCornerChromaPixel.ToWords());
            tempList.AddRange(upperEdgeChromaPixels.ToWords());
            tempList.AddRange(sadAdjustment.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5794;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetMotionVectorMaskINTEL(IdResultType resultType, IdResult resultId, IdRef skipBlockPartitionType, IdRef direction)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(skipBlockPartitionType.ToWords());
            tempList.AddRange(direction.ToWords());
            ushort opCode = 5795;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicConvertToMcePayloadINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5796;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetIntraLumaShapePenaltyINTEL(IdResultType resultType, IdResult resultId, IdRef packedShapePenalty, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedShapePenalty.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5797;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetIntraLumaModeCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef lumaModePenalty, IdRef lumaPackedNeighborModes, IdRef lumaPackedNonDcPenalty, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(lumaModePenalty.ToWords());
            tempList.AddRange(lumaPackedNeighborModes.ToWords());
            tempList.AddRange(lumaPackedNonDcPenalty.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5798;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetIntraChromaModeCostFunctionINTEL(IdResultType resultType, IdResult resultId, IdRef chromaModeBasePenalty, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(chromaModeBasePenalty.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5799;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetBilinearFilterEnableINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5800;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetSkcForwardTransformEnableINTEL(IdResultType resultType, IdResult resultId, IdRef packedSadCoefficients, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packedSadCoefficients.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5801;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicSetBlockBasedRawSkipSadINTEL(IdResultType resultType, IdResult resultId, IdRef blockBasedSkipType, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(blockBasedSkipType.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5802;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicEvaluateIpeINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5803;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicEvaluateWithSingleReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef refImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(refImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5804;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicEvaluateWithDualReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef fwdRefImage, IdRef bwdRefImage, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(fwdRefImage.ToWords());
            tempList.AddRange(bwdRefImage.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5805;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(packedReferenceIds.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5806;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicEvaluateWithMultiReferenceInterlacedINTEL(IdResultType resultType, IdResult resultId, IdRef srcImage, IdRef packedReferenceIds, IdRef packedReferenceFieldPolarities, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(srcImage.ToWords());
            tempList.AddRange(packedReferenceIds.ToWords());
            tempList.AddRange(packedReferenceFieldPolarities.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5807;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicConvertToMceResultINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5808;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetIpeLumaShapeINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5809;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetBestIpeLumaDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5810;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetBestIpeChromaDistortionINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5811;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetPackedIpeLumaModesINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5812;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetIpeChromaModeINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5813;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaCountThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5814;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetPackedSkcLumaSumThresholdINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5815;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSubgroupAvcSicGetInterRawSadsINTEL(IdResultType resultType, IdResult resultId, IdRef payload)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(payload.ToWords());
            ushort opCode = 5816;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpVariableLengthArrayINTEL(IdResultType resultType, IdResult resultId, IdRef lenght)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(lenght.ToWords());
            ushort opCode = 5818;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSaveMemoryINTEL(IdResultType resultType, IdResult resultId)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            ushort opCode = 5819;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRestoreMemoryINTEL(IdRef ptr)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(ptr.ToWords());
            ushort opCode = 5820;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSinCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger fromSign, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(fromSign.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5840;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCastINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5841;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCastFromIntINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger mout, LiteralInteger fromSign, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(fromSign.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5842;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCastToIntINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5843;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatAddINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5846;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSubINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5847;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatMulINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5848;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatDivINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5849;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatGTINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            ushort opCode = 5850;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatGEINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            ushort opCode = 5851;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLTINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            ushort opCode = 5852;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLEINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            ushort opCode = 5853;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatEQINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            ushort opCode = 5854;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatRecipINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5855;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatRSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5856;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCbrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5857;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatHypotINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5858;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5859;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLogINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5860;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLog2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5861;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLog10INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5862;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatLog1pINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5863;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatExpINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5864;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatExp2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5865;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatExp10INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5866;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatExpm1INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5867;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSinINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5868;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5869;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSinCosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5870;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatSinPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5871;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5872;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatASinINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5873;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatASinPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5874;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatACosINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5875;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatACosPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5876;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatATanINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5877;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatATanPiINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5878;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatATan2INTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5879;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatPowINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5880;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatPowRINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger m2, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(m2.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5881;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpArbitraryFloatPowNINTEL(IdResultType resultType, IdResult resultId, IdRef a, LiteralInteger m1, IdRef b, LiteralInteger mout, LiteralInteger enableSubnormals, LiteralInteger roundingMode, LiteralInteger roundingAccuracy)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(a.ToWords());
            tempList.AddRange(m1.ToWords());
            tempList.AddRange(b.ToWords());
            tempList.AddRange(mout.ToWords());
            tempList.AddRange(enableSubnormals.ToWords());
            tempList.AddRange(roundingMode.ToWords());
            tempList.AddRange(roundingAccuracy.ToWords());
            ushort opCode = 5882;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpLoopControlINTEL(params LiteralInteger[] loopControlParameters)
        {
            var tempList = new List<SPIRVWord>();
            foreach(var el in loopControlParameters)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 5887;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAliasDomainDeclINTEL(IdResult resultId, IdRef? name = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            if(name is IdRef nameNotNull)
                tempList.AddRange(nameNotNull.ToWords());
            ushort opCode = 5911;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAliasScopeDeclINTEL(IdResult resultId, IdRef aliasDomain, IdRef? name = null)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(aliasDomain.ToWords());
            if(name is IdRef nameNotNull)
                tempList.AddRange(nameNotNull.ToWords());
            ushort opCode = 5912;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAliasScopeListDeclINTEL(IdResult resultId, params IdRef[] aliasScope1AliasScope2)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            foreach(var el in aliasScope1AliasScope2)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 5913;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedSqrtINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5923;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedRecipINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5924;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedRsqrtINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5925;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedSinINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5926;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedCosINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5927;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedSinCosINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5928;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedSinPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5929;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5930;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedSinCosPiINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5931;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedLogINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5932;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFixedExpINTEL(IdResultType resultType, IdResult resultId, IdRef inputType, IdRef input, LiteralInteger s, LiteralInteger i, LiteralInteger rI, LiteralInteger q, LiteralInteger o)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(inputType.ToWords());
            tempList.AddRange(input.ToWords());
            tempList.AddRange(s.ToWords());
            tempList.AddRange(i.ToWords());
            tempList.AddRange(rI.ToWords());
            tempList.AddRange(q.ToWords());
            tempList.AddRange(o.ToWords());
            ushort opCode = 5933;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpPtrCastToCrossWorkgroupINTEL(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 5934;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpCrossWorkgroupCastToPtrINTEL(IdResultType resultType, IdResult resultId, IdRef pointer)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            ushort opCode = 5938;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpReadPipeBlockingINTEL(IdResultType resultType, IdResult resultId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 5946;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpWritePipeBlockingINTEL(IdResultType resultType, IdResult resultId, IdRef packetSize, IdRef packetAlignment)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(packetSize.ToWords());
            tempList.AddRange(packetAlignment.ToWords());
            ushort opCode = 5947;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpFPGARegINTEL(IdResultType resultType, IdResult resultId, IdRef result, IdRef input)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(result.ToWords());
            tempList.AddRange(input.ToWords());
            ushort opCode = 5949;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetRayTMinKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 6016;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetRayFlagsKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 6017;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionTKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6018;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionInstanceCustomIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6019;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionInstanceIdKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6020;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionInstanceShaderBindingTableRecordOffsetKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6021;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionGeometryIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6022;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionPrimitiveIndexKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6023;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionBarycentricsKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6024;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionFrontFaceKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6025;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionCandidateAABBOpaqueKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 6026;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionObjectRayDirectionKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6027;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionObjectRayOriginKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6028;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetWorldRayDirectionKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 6029;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetWorldRayOriginKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            ushort opCode = 6030;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionObjectToWorldKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6031;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpRayQueryGetIntersectionWorldToObjectKHR(IdResultType resultType, IdResult resultId, IdRef rayQuery, IdRef intersection)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(rayQuery.ToWords());
            tempList.AddRange(intersection.ToWords());
            ushort opCode = 6032;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpAtomicFAddEXT(IdResultType resultType, IdResult resultId, IdRef pointer, IdScope memory, IdMemorySemantics semantics, IdRef value)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(pointer.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            tempList.AddRange(value.ToWords());
            ushort opCode = 6035;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeBufferSurfaceINTEL(IdResult resultId, AccessQualifier accessQualifier)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(accessQualifier.ToWords());
            ushort opCode = 6086;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpTypeStructContinuedINTEL(params IdRef[] member0typemember1type)
        {
            var tempList = new List<SPIRVWord>();
            foreach(var el in member0typemember1type)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 6090;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpConstantCompositeContinuedINTEL(params IdRef[] constituents)
        {
            var tempList = new List<SPIRVWord>();
            foreach(var el in constituents)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 6091;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpSpecConstantCompositeContinuedINTEL(params IdRef[] constituents)
        {
            var tempList = new List<SPIRVWord>();
            foreach(var el in constituents)
            {
                tempList.AddRange(el.ToWords());
            }
            ushort opCode = 6092;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpControlBarrierArriveINTEL(IdScope execution, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 6142;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpControlBarrierWaitINTEL(IdScope execution, IdScope memory, IdMemorySemantics semantics)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(memory.ToWords());
            tempList.AddRange(semantics.ToWords());
            ushort opCode = 6143;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupIMulKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6401;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupFMulKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6402;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupBitwiseAndKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6403;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupBitwiseOrKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6404;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupBitwiseXorKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6405;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupLogicalAndKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6406;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupLogicalOrKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6407;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

        public void GenerateOpGroupLogicalXorKHR(IdResultType resultType, IdResult resultId, IdScope execution, GroupOperation operation, IdRef x)
        {
            var tempList = new List<SPIRVWord>();
            tempList.AddRange(resultType.ToWords());
            tempList.AddRange(resultId.ToWords());
            tempList.AddRange(execution.ToWords());
            tempList.AddRange(operation.ToWords());
            tempList.AddRange(x.ToWords());
            ushort opCode = 6408;
            ushort wordCount = (ushort) (tempList.Count + 1);
            uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);
            _instructions.Add(new SPIRVWord(combined));
            _instructions.AddRange(tempList);
        }

   }
}

#nullable restore
