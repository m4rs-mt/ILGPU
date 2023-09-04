using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPIRVGenerationTool.Grammar;

public class SPIRVGrammar
{
    public List<SPIRVOp> Instructions { get; set; }

    [JsonPropertyName("operand_kinds")]
    public List<SPIRVType> Types { get; set; }
}

public class SPIRVOp
{
    public string OpName { get; }

    public int OpCode { get; }

    public List<SPIRVOperand>? Operands { get; }
}

public class SPIRVOperand
{
    public string Kind { get; }

    public string? Name { get; set; }

    public string Quantifier { get; }
}

public class SPIRVType
{
    public List<string> Bases { get; }

    public string Category { get; }

    public List<SPIRVEnumerant>? Enumerants { get; }

    [JsonPropertyName("kind")]
    public string Name { get; }
}

public class SPIRVEnumerant
{
    [JsonPropertyName("enumerant")]
    public string Name { get; set; }

    public JsonElement Value { get; set; }
}
