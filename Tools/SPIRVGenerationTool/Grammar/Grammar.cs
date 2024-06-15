// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Grammar.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPIRVGenerationTool.Grammar;

public class SPIRVGrammar
{
    public required List<SPIRVOp> Instructions { get; set; }

    [JsonPropertyName("operand_kinds")]
    public required List<SPIRVType> Types { get; set; }
}

public class SPIRVOp
{
    public required string OpName { get; set; }

    public required int OpCode { get; set; }

    public List<SPIRVOperand>? Operands { get; set; }
}

public class SPIRVOperand
{
    public required string Kind { get; set; }

    public string? Name { get; set; }

    public string Quantifier { get; set; } = "";
}

public class SPIRVType
{
    public List<string> Bases { get; set; }

    public required string Category { get; set; }

    public List<SPIRVEnumerant>? Enumerants { get; }

    [JsonPropertyName("kind")] public required string Name { get; set; }
}

public class SPIRVEnumerant
{
    [JsonPropertyName("enumerant")] public required string Name { get; set; }

    public required JsonElement Value { get; set; }
}
