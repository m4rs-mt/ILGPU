using SPIRVGenerationTool.Grammar;

namespace SPIRVGenerationTool.Generators.Builder;

public class BinaryBuilderGenerator : BuilderGenerator
{
    private const string Start = @"using System;
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
                ""Attempted to merge string representation builder with binary builder""
            );
        }

";

    public BinaryBuilderGenerator(string path) : base(Start, path)
    {
    }

    protected override void GenerateMethodBody(Operation info)
    {
        Builder.AppendLine();
        Builder.AppendIndentedLine("{");
        Builder.Indent();

        if (info.Parameters.Count != 0)
        {
            Builder.AppendIndentedLine("var tempList = new List<SPIRVWord>();");
        }

        foreach (var parameter in info.Parameters)
        {
            var name = parameter.Name;

            if (parameter.Quantifier == "?")
            {
                // There *is* a performance penalty for string interpolation
                // because we're not calling actual StringBuilder methods which would
                // compile to efficient code that just appends to the StringBuilder.

                // It shouldn't matter anyways, we're running this script like once a year.
                Builder.AppendIndentedLine($"if({name} is {parameter.Type} {name}NotNull)")
                    .Indent()
                    .AppendIndentedLine($"tempList.AddRange({name}NotNull.ToWords());")
                    .Dedent();
            }
            else if (parameter.Quantifier == "*")
            {
                Builder.AppendIndentedLine($"foreach(var el in {name})")
                    .AppendIndentedLine("{")
                    .Indent()
                    .AppendIndentedLine("tempList.AddRange(el.ToWords());")
                    .Dedent()
                    .AppendIndentedLine("}");
            }
            else
            {
                Builder.AppendIndentedLine($"tempList.AddRange({name}.ToWords());");
            }
        }

        Builder.AppendIndentedLine($"ushort opCode = {info.OpCode};");

        if (info.Parameters.Count == 0)
            Builder.AppendIndentedLine("ushort wordCount = 0;");
        else
            Builder.AppendIndentedLine("ushort wordCount = (ushort) (tempList.Count + 1);");

        Builder.AppendIndentedLine("uint combined = SPIRVBuilderUtils.JoinOpCodeWordCount(opCode, wordCount);")
            .AppendIndentedLine("_instructions.Add(new SPIRVWord(combined));");

        if (info.Parameters.Count != 0)
            Builder.AppendIndentedLine("_instructions.AddRange(tempList);");

        Builder.Dedent().AppendIndentedLine("}");
    }
}
