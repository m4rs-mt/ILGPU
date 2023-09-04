// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using SPIRVGenerationTool.Grammar;
using System.Text.Json;

namespace SPIRVGenerationTool.Generators.Type;

public class TypeGenerator : GeneratorBase
{
    private const string Start = @"using System;
using System.Collections.Generic;

// disable: max_line_length

namespace ILGPU.Backends.SPIRV.Types
{
";

    private const string End = "}\n";

    public TypeGenerator(string path) : base(Start, End, path)
    {
    }

    protected override void GenerateCodeInternal(SPIRVGrammar grammar)
    {
        foreach (var type in grammar.Types)
        {
            GenerateType(type);
        }
    }

    void GenerateType(SPIRVType kind)
    {
        switch (kind.Category)
        {
            case "Id":
                GenerateId(kind);
                break;
            case "ValueEnum":
            case "BitEnum":
                GenerateEnum(kind);
                break;
            case "Composite":
                GenerateComposite(kind);
                break;
        }
    }

    void GenerateId(SPIRVType kind)
    {
        Builder.Indent()
            .AppendIndentedLine($"internal struct {kind.Name} : ISPIRVType")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("private SPIRVWord _value;")
            .AppendLine()
            .AppendIndentedLine($"public {kind.Name}(SPIRVWord word)")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("_value = word;")
            .Dedent()
            .AppendIndentedLine("}")
            .AppendLine()
            .AppendIndentedLine("public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };")
            .AppendLine()
            .AppendIndentedLine("public string ToRepr() => \"%\" + _value;")
            .AppendLine()
            .Dedent()
            .AppendIndentedLine("}")
            .Dedent();
    }

    void GenerateEnum(SPIRVType kind)
    {
        Builder.Indent()
            .AppendIndentedLine($"internal struct {kind.Name} : ISPIRVType")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("private SPIRVWord _value;")
            .AppendIndentedLine("private string _repr;")
            .AppendLine()
            .AppendIndentedLine($"public {kind.Name}(SPIRVWord word, string name)")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("_value = word;")
            .AppendIndentedLine("_repr = name;")
            .Dedent()
            .AppendIndentedLine("}")
            .AppendLine();

        // Enums always have enumerants
        foreach (var enumerant in kind.Enumerants!)
        {
            // Parse hex _value if string
            uint enumValue = enumerant.Value.ValueKind == JsonValueKind.String
                ? Convert.ToUInt32(enumerant.Value.GetString(), 16)
                : enumerant.Value.GetUInt32();

            string enumName = enumerant.Name;
            string kindName = kind.Name;

            Builder.AppendIndentedLine($"public static readonly {kindName} {enumName} =")
                .Indent()
                .AppendIndentedLine($"new {kindName}({enumValue}, \"{enumName}\");")
                .Dedent()
                .AppendLine();
        }

        Builder.AppendIndentedLine("public SPIRVWord[] ToWords() =>")
            .Indent()
            .AppendIndentedLine("new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };")
            .Dedent()
            .AppendLine()
            .AppendIndentedLine("public string ToRepr() => _repr;")
            .Dedent()
            .AppendIndentedLine("}")
            .Dedent()
            .AppendLine();
    }

    void GenerateComposite(SPIRVType kind)
    {
        Builder.Indent()
            .AppendIndentedLine($"internal struct {kind.Name} : ISPIRVType")
            .AppendIndentedLine("{")
            .Indent();

        for (int i = 0; i < kind.Bases.Count; i++)
        {
            Builder.AppendIndentedLine($"public {kind.Bases[i]} base{i};");
        }

        Builder.AppendLine();

        #region ToWords

        Builder.AppendIndentedLine("public SPIRVWord[] ToWords()")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("List<SPIRVWord> words = new List<SPIRVWord>();");

        for (int i = 0; i < kind.Bases.Count; i++)
        {
            Builder.AppendIndentedLine($"words.AddRange(base{i}.ToWords());");
        }

        Builder.AppendIndentedLine("return words.ToArray();")
            .Dedent()
            .AppendIndentedLine("}")
            .AppendLine();

        #endregion

        #region ToRepr

        Builder.AppendIndentedLine("public string ToRepr()")
            .AppendIndentedLine("{")
            .Indent()
            .AppendIndentedLine("string _repr = \"{ \";");

        for (int i = 0; i < kind.Bases.Count; i++)
        {
            Builder.AppendIndentedLine($"_repr += $\"base{i} = {{base{i}.ToRepr()}} \";");
        }

        Builder.AppendIndentedLine("_repr += \"}\";")
            .AppendIndentedLine("return _repr;")
            .Dedent()
            .AppendIndentedLine("}");

        #endregion

        Builder.Dedent()
            .AppendIndentedLine("}")
            .Dedent()
            .AppendLine();
    }
}
