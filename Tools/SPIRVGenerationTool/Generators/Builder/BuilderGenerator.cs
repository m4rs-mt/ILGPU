using SPIRVGenerationTool.Grammar;

namespace SPIRVGenerationTool.Generators.Builder;

public abstract class BuilderGenerator : GeneratorBase
{
    private const string End = @"   }
}

#nullable restore
";

    protected BuilderGenerator(string start, string path) : base(start, End, path)
    {
        Builder.Indent().Indent();
    }

    protected override void GenerateCodeInternal(SPIRVGrammar grammar)
    {
        foreach (var instruction in grammar.Instructions)
        {
            var parameters = instruction.Operands?.Select(x => new Parameter(x));
            var parametersList = parameters?.ToList() ?? new List<Parameter>();
            var info = new Operation(instruction.OpName, instruction.OpCode, parametersList);
            GenerateMethodHeader(info);
            GenerateMethodBody(info);
            Builder.AppendLine();
        }
    }

    protected abstract void GenerateMethodBody(Operation info);

    private void GenerateMethodHeader(Operation info)
    {
        var fullParameters = info.Parameters.Select(x => x.FullParameter);

        Builder.AppendIndented($"public void Generate{info.Name}(")
            .AppendJoin(", ", fullParameters)
            .Append(")");
    }
}
