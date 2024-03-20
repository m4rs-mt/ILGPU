// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IndentedStringBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text;

namespace SPIRVGenerationTool.Util;

public class IndentedStringBuilder
{
    private StringBuilder _builder = new();
    private int _indentLevel = 0;

    public IndentedStringBuilder Append(string s)
    {
        _builder.Append(s);
        return this;
    }

    public IndentedStringBuilder AppendLine()
    {
        _builder.AppendLine();
        return this;
    }

    public IndentedStringBuilder AppendLine(string s)
    {
        _builder.AppendLine(s);
        return this;
    }

    public IndentedStringBuilder AppendJoin<T>(string sep, IEnumerable<T> e)
    {
        _builder.AppendJoin(sep, e);
        return this;
    }

    public IndentedStringBuilder AppendIndented(string s)
    {
        for (int i = 0; i < _indentLevel; i++)
        {
            Append("    ");
        }

        return Append(s);
    }

    public IndentedStringBuilder AppendIndentedLine(string s)
    {
        return AppendIndented(s).AppendLine();
    }

    public IndentedStringBuilder Indent()
    {
        _indentLevel++;
        return this;
    }

    public IndentedStringBuilder Dedent()
    {
        _indentLevel--;
        return this;
    }

    public override string ToString() => _builder.ToString();
}
