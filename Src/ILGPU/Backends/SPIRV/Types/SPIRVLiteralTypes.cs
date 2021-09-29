using System;
using System.Collections.Generic;
using System.Text;

namespace ILGPU.Backends.SPIRV.Types
{
    /// <summary>
    /// Represents a SPIR-V integer literal
    /// </summary>
    public readonly struct LiteralInteger : ISPIRVType
    {
        private readonly int value;

        public LiteralInteger(int val)
        {
            value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(value))};

        public string ToRepr() => value.ToString();
    }

    /// <summary>
    /// Represents a SPIR-V integer string
    /// </summary>
    public readonly struct LiteralString : ISPIRVType
    {
        private readonly string value;

        public LiteralString(string val)
        {
            value = val;
        }

        public SPIRVWord[] ToWords() =>
            SPIRVWord.ManyFromBytes(Encoding.UTF8.GetBytes(value + "\000"));

        public string ToRepr() => value.ToString();
    }
}
