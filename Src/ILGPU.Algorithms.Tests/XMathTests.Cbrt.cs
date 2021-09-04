// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: XMathTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Tests;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{

    public abstract partial class XMathTests
    {

        internal static void CbrtKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data)
        {
            data[index] = XMath.Cbrt(data[index]);
        }

        public static TheoryData<object> SizeOfTestData => new TheoryData<object>
        {
            { 8 },
        };


        [Theory]
        [MemberData(nameof(SizeOfTestData))]
        [KernelMethod(nameof(CbrtKernel))]
        public void CbrtOf(double[] values)
        {
            using var buffer = Accelerator.Allocate1D<double>(values.Length);
            buffer.CopyFromCPU(values);
            Execute(buffer.IntExtent, buffer.View);

            var expected = new double[] { 2 };
            Verify(buffer.View, expected);
        }



    }




}
