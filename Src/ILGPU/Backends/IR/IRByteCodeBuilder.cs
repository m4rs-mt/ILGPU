using ILGPU.IR;
using ILGPU.IR.Values;
using System;
using System.IO;

namespace ILGPU.Backends.IR
{
    class IRByteCodeBuilder
    {
        private readonly BinaryWriter _writer;

        public IRByteCodeBuilder()
        {
            _writer = new BinaryWriter(new MemoryStream());
        }

        public IRByteCodeBuilder(byte[] buffer)
        {
            _writer = new BinaryWriter(new MemoryStream(buffer));
        }

        public void EmitValue(Value value, Action<BinaryWriter> callback)
        {
            _writer.Write((int)value.ValueKind);
            callback(_writer);
        }
    }
}
