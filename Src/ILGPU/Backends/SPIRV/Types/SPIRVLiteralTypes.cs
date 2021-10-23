using System;
using System.Text;

namespace ILGPU.Backends.SPIRV.Types
{
    internal readonly struct LiteralInteger : ISPIRVType
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

    internal readonly struct LiteralFloat : ISPIRVType
    {
        private readonly float value;

        public LiteralFloat(float val)
        {
            value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(value))};

        public string ToRepr() => value.ToString();
    }

    internal readonly struct LiteralString : ISPIRVType
    {
        private readonly string value;

        public LiteralString(string val)
        {
            value = val + "\000";
        }

        public SPIRVWord[] ToWords() =>
            SPIRVWord.ManyFromBytes(Encoding.UTF8.GetBytes(value));

        public string ToRepr() => value;
    }

    internal readonly struct LiteralContextDependentNumber : ISPIRVType
    {
        private readonly LiteralFloat? floatValue;
        private readonly LiteralInteger? intValue;

        public LiteralContextDependentNumber(LiteralFloat val)
        {
            floatValue = val;
            intValue = null;
        }

        public LiteralContextDependentNumber(LiteralInteger val)
        {
            intValue = val;
            floatValue = null;
        }

        public SPIRVWord[] ToWords() => floatValue?.ToWords() ?? intValue?.ToWords();

        public string ToRepr() => floatValue?.ToRepr() ?? intValue?.ToRepr();
    }

    internal readonly struct LiteralExtInstInteger
    {
        private readonly uint value;

        public LiteralExtInstInteger(uint val)
        {
            value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(value))};

        public string ToRepr() => value.ToString();
    }

    internal readonly struct LiteralSpecConstantOpInteger
    {
        private readonly uint value;

        public LiteralSpecConstantOpInteger(uint val)
        {
            value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(value))};

        public string ToRepr() => value.ToString();
    }
}
