// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FormatStringTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Util;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

public sealed class FormatStringTests
{
    [Fact]
    public void PlainString_NoArguments()
    {
        Assert.True(FormatString.TryParse("Hello World", out var expressions));
        Assert.Single(expressions);
        Assert.Equal("Hello World", expressions[0].String);
        Assert.False(expressions[0].HasArgument);
    }

    [Fact]
    public void SingleArgument()
    {
        Assert.True(FormatString.TryParse("Value: {0}", out var expressions));
        Assert.Equal(2, expressions.Length);
        Assert.Equal("Value: ", expressions[0].String);
        Assert.True(expressions[1].HasArgument);
        Assert.Equal(0, expressions[1].Argument);
    }

    [Fact]
    public void MultipleArguments()
    {
        Assert.True(FormatString.TryParse("{0} + {1} = {2}", out var expressions));
        Assert.Equal(5, expressions.Length);
        Assert.True(expressions[0].HasArgument);
        Assert.Equal(0, expressions[0].Argument);
        Assert.Equal(" + ", expressions[1].String);
        Assert.True(expressions[2].HasArgument);
        Assert.Equal(1, expressions[2].Argument);
        Assert.Equal(" = ", expressions[3].String);
        Assert.True(expressions[4].HasArgument);
        Assert.Equal(2, expressions[4].Argument);
    }

    [Fact]
    public void EscapedBrackets()
    {
        // {{escaped}} → "{" + "escaped}" ({{ escapes to {, then escaped is literal,
        // then }} escapes to })
        Assert.True(FormatString.TryParse("{{escaped}}", out var expressions));
        Assert.Equal(2, expressions.Length);
        Assert.Equal("{", expressions[0].String);
        Assert.Equal("escaped}", expressions[1].String);
    }

    [Fact]
    public void DanglingOpenBracket()
    {
        // Dangling { without matching } appends the { as a string expression
        Assert.True(FormatString.TryParse("hello {", out var expressions));
        Assert.Equal(2, expressions.Length);
        Assert.Equal("hello ", expressions[0].String);
        Assert.Equal("{", expressions[1].String);
    }

    [Fact]
    public void DanglingCloseBracket_ReturnsFalse()
    {
        // A singular } without opening { is invalid
        Assert.False(FormatString.TryParse("hello }", out _));
    }

    [Fact]
    public void NegativeArgument_ReturnsFalse()
    {
        Assert.False(FormatString.TryParse("{-1}", out _));
    }

    [Fact]
    public void EmptyString()
    {
        Assert.True(FormatString.TryParse("", out var expressions));
        Assert.Empty(expressions);
    }

    [Fact]
    public void AdjacentArguments()
    {
        Assert.True(FormatString.TryParse("{0}{1}", out var expressions));
        Assert.Equal(2, expressions.Length);
        Assert.True(expressions[0].HasArgument);
        Assert.Equal(0, expressions[0].Argument);
        Assert.True(expressions[1].HasArgument);
        Assert.Equal(1, expressions[1].Argument);
    }
}
