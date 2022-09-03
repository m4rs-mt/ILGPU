using System;
using System.Text;

namespace ILGPU.Backends.SPIRV.Types
{
    internal readonly struct LiteralInteger : ISPIRVType
    {
        private readonly int _value;

        public LiteralInteger(int val)
        {
            _value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(_value))};

        public string ToRepr() => _value.ToString();
    }

    internal readonly struct LiteralFloat : ISPIRVType
    {
        private readonly float _value;

        public LiteralFloat(float val)
        {
            _value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(_value))};

        public string ToRepr() => _value.ToString();
    }

    internal readonly struct LiteralString : ISPIRVType
    {
        private readonly string _value;

        public LiteralString(string val)
        {
            _value = val + "\000";
        }

        public SPIRVWord[] ToWords() =>
            SPIRVWord.ManyFromBytes(Encoding.UTF8.GetBytes(_value));

        public string ToRepr() => _value;
    }

    internal readonly struct LiteralContextDependentNumber : ISPIRVType
    {
        private readonly LiteralFloat? _floatValue;
        private readonly LiteralInteger? _intValue;

        public LiteralContextDependentNumber(LiteralFloat val)
        {
            _floatValue = val;
            _intValue = null;
        }

        public LiteralContextDependentNumber(LiteralInteger val)
        {
            _intValue = val;
            _floatValue = null;
        }

        public SPIRVWord[] ToWords() => _floatValue?.ToWords() ?? _intValue?.ToWords();

        public string ToRepr() => _floatValue?.ToRepr() ?? _intValue?.ToRepr();
    }

    internal readonly struct LiteralExtInstInteger
    {
        private readonly uint _value;

        public LiteralExtInstInteger(uint val)
        {
            _value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(_value))};

        public string ToRepr() => _value.ToString();
    }

    internal readonly struct LiteralSpecConstantOpInteger
    {
        private readonly uint _value;

        public LiteralSpecConstantOpInteger(uint val)
        {
            _value = val;
        }

        public SPIRVWord[] ToWords() =>
            new[] {SPIRVWord.FromBytes(BitConverter.GetBytes(_value))};

        public string ToRepr() => _value.ToString();
    }
}