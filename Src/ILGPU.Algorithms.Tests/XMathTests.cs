// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: XMathTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Tests;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class XMathTests : AlgorithmsTestBase
    {
        protected XMathTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal readonly struct XMathTuple<T> where T : struct
        {
            public XMathTuple(T x, T y)
            {
                X = x;
                Y = y;
            }

            public T X { get; }
            public T Y { get; }
        }
    }
}
