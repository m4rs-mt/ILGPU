using System;

namespace ILGPU.Backends.SPIRV
{
    internal struct SPIRVWord
    {
        public uint Data { get; }
        private const int BytesPerWord = sizeof(uint);

        public SPIRVWord(uint value)
        {
            Data = value;
        }

        public static SPIRVWord FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > BytesPerWord)
            {
                throw new ArgumentException(
                    "The provided span must be at most 4 bytes long.",
                    nameof(bytes));
            }

            return new SPIRVWord(BitConverter.ToUInt32(bytes.ToArray(), 0));
        }

        public static SPIRVWord[] ManyFromBytes(ReadOnlySpan<byte> bytes)
        {
            // Round up bytes.Length / BytesPerWord
            var words = new SPIRVWord[(bytes.Length - 1) / BytesPerWord + 1];

            for (int i = 0; i < words.Length; i++)
            {
                int bytesIndex = i * BytesPerWord;
                // Check if we can take a word more bytes,
                // if we can't then just take what's left
                if (bytesIndex + BytesPerWord > bytes.Length)
                {
                    words[i] = FromBytes(bytes.Slice(bytesIndex));
                }
                else
                {
                    words[i] = FromBytes(bytes.Slice(bytesIndex, BytesPerWord));
                }
            }

            return words;
        }

        public override string ToString() => Data.ToString();

        public static implicit operator SPIRVWord(uint u) => new SPIRVWord(u);
    }
}
