// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: XMathTests.Cbrt.cs
//
// This file was made by Marcel Pawelczyk, to be used freely without restriction by
// the ILGPU project. 
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Tests;
using Xunit;

namespace ILGPU.Algorithms.Tests
{

    public abstract partial class XMathTests
    {


        internal static void CbrtDoubleKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> IO)
        {
            IO[index] = XMath.Cbrt(IO[index]);
        }

        public static TheoryData<object> DoubleDataSet => new TheoryData<object>
        {
            { new double[] {-27,-8,-2, 0, 2, 8, 27, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1000, 87621863} },
        };

        [Theory]
        [MemberData(nameof(DoubleDataSet))]
        [KernelMethod(nameof(CbrtDoubleKernel))]
        public void CbrtOfDouble(double[] values)
        {
            using var buffer = Accelerator.Allocate1D<double>(values.Length);
            buffer.CopyFromCPU(values);
            Execute(buffer.IntExtent, buffer.View);

            var expected = new double[] {
                -3,-2, -1.259921049894873164767210607278228,0, 1.259921049894873164767210607278228, 2 , 3, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 10, 444.1580055421544394056176363406254725,
            };
            VerifyWithinRelativeError(buffer.View, expected, 1e-8);
        }




        internal static void CbrtFloatKernel(Index1D index, ArrayView1D<float, Stride1D.Dense> Input, ArrayView1D<double, Stride1D.Dense> Output)
        {
            Output[index] = XMath.Cbrt(Input[index]);
        }

        public static TheoryData<object> FloatDataSet => new TheoryData<object>
        {
            { new float[] {-27f,-8f,-2f, 0f, 2f, 8f, 27f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1000f, 87621863f} },
        };

        [Theory]
        [MemberData(nameof(FloatDataSet))]
        [KernelMethod(nameof(CbrtFloatKernel))]
        public void CbrtOfFloat(float[] values)
        {
            using var buffer = Accelerator.Allocate1D<float>(values.Length);
            using var buffer2 = Accelerator.Allocate1D<double>(values.Length);
            buffer.CopyFromCPU(values);
            Execute(buffer.IntExtent, buffer.View, buffer2.View);

            var expected = new double[] {
                -3,-2, -1.259921049894873164767210607278228,0, 1.259921049894873164767210607278228, 2 , 3, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 10, 444.1580055421544394056176363406254725,
            };
            VerifyWithinRelativeError(buffer2.View, expected, 1e-8);
        }



        internal static void CbrtFastDoubletKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> Input,ArrayView1D<float, Stride1D.Dense> Output)
        {
            Output[index] = XMath.CbrtFast(Input[index]);
        }

        [Theory]
        [MemberData(nameof(DoubleDataSet))]
        [KernelMethod(nameof(CbrtFastDoubletKernel))]
        public void CbrtOfDoubleFast(double[] values)
        {
            using var buffer = Accelerator.Allocate1D<double>(values.Length);
            using var buffer2 = Accelerator.Allocate1D<float>(values.Length);
            buffer.CopyFromCPU(values);
            Execute(buffer.IntExtent, buffer.View, buffer2.View);

            var expected = new float[] {
                -3f,-2f, -1.259921049894873164767210607278228f,0f, 1.259921049894873164767210607278228f, 2f , 3f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 10f, 444.1580055421544394056176363406254725f,
            };
            VerifyWithinRelativeError(buffer2.View, expected, 1e-5);
        }



        internal static void CbrtFastFloatKernel(Index1D index, ArrayView1D<float, Stride1D.Dense> IO)
        {
            IO[index] = XMath.CbrtFast(IO[index]);
        }

        [Theory]
        [MemberData(nameof(FloatDataSet))]
        [KernelMethod(nameof(CbrtFastFloatKernel))]
        public void CbrtOfFloatFast(float[] values)
        {
            using var buffer = Accelerator.Allocate1D<float>(values.Length);
            buffer.CopyFromCPU(values);
            Execute(buffer.IntExtent, buffer.View);

            var expected = new float[] {
                -3f,-2f, -1.259921049894873164767210607278228f,0f, 1.259921049894873164767210607278228f, 2f , 3f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 10f, 444.1580055421544394056176363406254725f,
            };
            VerifyWithinRelativeError(buffer.View, expected, 1e-5);
        }

    }

}
