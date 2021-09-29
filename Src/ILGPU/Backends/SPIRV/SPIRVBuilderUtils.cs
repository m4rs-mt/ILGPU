using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Utilities for generated functions in SPIRVBuilder.cs
    /// </summary>
    public static class SPIRVBuilderUtils
    {
        /// <summary>
        /// Joins an Opcode and word count into one word
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="wordCount"></param>
        /// <returns></returns>
        public static uint JoinOpCodeWordCount(ushort opCode, ushort wordCount)
        {
            uint opCodeUint = opCode;
            uint wordCountUint = wordCount;

            uint shiftedWordCount = wordCountUint << 16;

            return shiftedWordCount | opCodeUint;
        }
    }
}
