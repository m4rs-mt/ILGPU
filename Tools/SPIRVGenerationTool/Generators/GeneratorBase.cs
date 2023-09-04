// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: GeneratorBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using SPIRVGenerationTool.Grammar;
using SPIRVGenerationTool.Util;

namespace SPIRVGenerationTool.Generators;

public abstract class GeneratorBase
{
    private readonly string _end;
    protected readonly IndentedStringBuilder Builder;
    private readonly string _path;

    protected GeneratorBase(string start, string end, string path)
    {
        _end = end;
        _path = path;

        string fileName = Path.GetFileName(path);

        string copyright = $"""
// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: {fileName}
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------
""" ;

        Builder = new IndentedStringBuilder();
        Builder.AppendLine(copyright)
            .AppendLine()
            .Append(start);
    }

    public void GenerateCode(SPIRVGrammar grammar)
    {
        GenerateCodeInternal(grammar);
        Builder.Append(_end);
    }

    protected abstract void GenerateCodeInternal(SPIRVGrammar grammar);

    public void WriteToFile()
    {
        string? dir = Path.GetDirectoryName(_path);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, Builder.ToString());
    }
}
