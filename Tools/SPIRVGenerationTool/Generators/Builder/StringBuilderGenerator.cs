// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: StringBuilderGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using SPIRVGenerationTool.Grammar;

namespace SPIRVGenerationTool.Generators.Builder;

public class StringBuilderGenerator : BuilderGenerator
{
    private const string Start = @"using System;
using System.Text;
using ILGPU.Backends.SPIRV.Types;

// disable: max_line_length

#nullable enable

namespace ILGPU.Backends.SPIRV
{

<#
    PushIndent(standardIndent);
#>
internal class StringSPIRVBuilder : ISPIRVBuilder {

    private StringBuilder _builder = new StringBuilder();

    public byte[] ToByteArray() => Encoding.UTF8.GetBytes(_builder.ToString());

    public void AddMetadata(
        SPIRVWord magic,
        SPIRVWord version,
        SPIRVWord genMagic,
        SPIRVWord bound,
        SPIRVWord schema)
    {
        _builder.AppendLine($""; Magic: {magic.Data:X}"");
        _builder.AppendLine($""; Version: {version.Data:X}"");
        _builder.AppendLine($""; Generator Magic: {genMagic.Data:X}"");
        _builder.AppendLine($""; Bound: {bound}"");
        _builder.AppendLine($""; Schema: {schema}"");
    }

    public void Merge(ISPIRVBuilder other) {
        if(other == null) {
            throw new ArgumentNullException(nameof(other));
        }

        var otherString = other as StringSPIRVBuilder;
        if(otherString == null) {
            throw new InvalidCodeGenerationException(
                ""Attempted to merge binary builder with string representation builder""
            );
        }

        _builder.Append(otherString._builder);
    }
";

    public StringBuilderGenerator(string path) : base(Start, path)
    {
    }

    protected override void GenerateMethodBody(Operation info)
    {
    }
}
