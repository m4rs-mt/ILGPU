// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Preprocessor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace SPIRVGenerationTool.Grammar;

public static class Preprocessor
{
    public static void Process(SPIRVGrammar grammar)
    {
        // Instructions
        RemoveInvalidCharacters(grammar);
        CamelCase(grammar);
        SanitizeNames(grammar);
        Rename(grammar);

        // Types
        SanitizeEnumerantNames(grammar);
    }

    private static void SanitizeEnumerantNames(SPIRVGrammar grammar)
    {
        foreach (var type in grammar.Types)
        {
           if(type.Enumerants is null)
               continue;

           foreach (var enumerant in type.Enumerants)
           {
               if (char.IsDigit(enumerant.Name[0]))
                   enumerant.Name = "n" + enumerant.Name;
           }
        }
    }

    private static void RemoveInvalidCharacters(SPIRVGrammar grammar)
    {
        foreach (var inst in grammar.Instructions)
        {
            if (inst.Operands is null)
                continue;

            foreach (var operand in inst.Operands)
            {
                operand.Name = operand.Name?.Replace(" ", "").Replace("'", "").Replace(".", "").Replace(",", "")
                    .Replace("+", "").Replace(">", "").Replace("<", "").Replace("\n", "").Replace("~", "");
            }
        }
    }

    private static readonly HashSet<string> KeywordSet = new()
    {
        "base", "ref", "interface", "string", "object", "default", "event"
    };

    private static void SanitizeNames(SPIRVGrammar grammar)
    {
        foreach (var inst in grammar.Instructions)
        {
            if (inst.Operands is null)
                continue;

            foreach (var operand in inst.Operands)
            {
                if (string.IsNullOrEmpty(operand.Name))
                    continue;

                if (KeywordSet.Contains(operand.Name))
                    operand.Name = "@" + operand.Name;

            }
        }
    }

    private static void CamelCase(SPIRVGrammar grammar)
    {
        foreach (var inst in grammar.Instructions)
        {
            if (inst.Operands is null)
                continue;

            foreach (var operand in inst.Operands)
            {
                if (!string.IsNullOrEmpty(operand.Name))
                {
                    operand.Name = char.ToLower(operand.Name[0]) + operand.Name[1..];
                }
            }
        }
    }

    private static void Rename(SPIRVGrammar grammar)
    {
        foreach (var inst in grammar.Instructions)
        {
            if (inst.Operands is null)
                continue;

            for (int i = 0; i < inst.Operands.Count; i++)
            {
                var operand = inst.Operands[i];
                operand.Name = operand.Kind switch
                {
                    "IdResult" => "resultId",
                    "IdResultType" => "resultType",
                    _ => string.IsNullOrEmpty(operand.Name) ? $"param{i}" : operand.Name
                };
            }
        }
    }
}
