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

    public required List<SPIRVOperand>? Operands { get; set; }
}

public class SPIRVOperand
{
    public required string Kind { get; set; }

    public required string? Name { get; set; }

    public required string Quantifier { get; set; }
}

public class SPIRVType
{
    public required List<string> Bases { get; set; }

    public required string Category { get; set; }

    public required List<SPIRVEnumerant>? Enumerants { get; set; }

    [JsonPropertyName("kind")] public required string Name { get; set; }
}

public class SPIRVEnumerant
{
    [JsonPropertyName("enumerant")] public required string Name { get; set; }

    public required JsonElement Value { get; set; }
}
