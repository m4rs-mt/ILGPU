// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTWConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.Runtime.Cuda
{
    public static class CuFFTWConstants
    {
        // Transform Direction
        public const int FFTW_FORWARD = -1;
        public const int FFTW_INVERSE = 1;
        public const int FFTW_BACKWARD = FFTW_INVERSE;

        // Planner Flags
        public const int FFTW_ESTIMATE = 0x01;
        public const int FFTW_MEASURE = 0x02;
        public const int FFTW_PATIENT = 0x03;
        public const int FFTW_EXHAUSTIVE = 0x04;
        public const int FFTW_WISDOM_ONLY = 0x05;
    }
}

#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
