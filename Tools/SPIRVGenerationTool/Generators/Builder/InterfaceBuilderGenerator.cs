// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterfaceBuilderGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using SPIRVGenerationTool.Grammar;

namespace SPIRVGenerationTool.Generators.Builder;

public class InterfaceGenerator : BuilderGenerator
{
    private const string Start = @"using System;
using ILGPU.Backends.SPIRV.Types;

// disable: max_line_length

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
";

    public InterfaceGenerator(string path) : base(Start, path)
    {
    }

    protected override void GenerateMethodBody(Operation info)
    {
        // No body, just finish method header
        Builder.AppendLine(";");
    }
}
