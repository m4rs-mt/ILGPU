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
