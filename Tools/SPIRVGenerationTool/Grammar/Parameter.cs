namespace SPIRVGenerationTool.Grammar;

public class Parameter
{
    public string Type { get; }
    public string Quantifier { get; }
    public string Name { get; }
    public string FullParameter { get; }

    public Parameter(SPIRVOperand operand)
    {
        Type = operand.Kind;
        Quantifier = operand.Quantifier;

        Name = operand.Name!; // Will be filled by preprocessor

        FullParameter = operand.Quantifier switch
        {
            "*" => $"params {Type}[] {Name}",
            "?" => $"{Type}? {Name} = null",
            _ => $"{Type} {Name}"
        };
    }
}
