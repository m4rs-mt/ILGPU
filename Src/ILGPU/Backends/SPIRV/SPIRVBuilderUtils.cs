// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: SPIRVBuilderUtils.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Backends.SPIRV
{
    internal static class SPIRVBuilderUtils
    {
        public static uint JoinOpCodeWordCount(ushort opCode, ushort wordCount)
        {
            uint opCodeUint = opCode;
            uint wordCountUint = wordCount;

            uint shiftedWordCount = wordCountUint << 16;

            return shiftedWordCount | opCodeUint;
        }
    }
}
