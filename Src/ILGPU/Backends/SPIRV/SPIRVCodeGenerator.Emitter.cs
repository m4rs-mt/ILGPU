using System;
using System.Text;

namespace ILGPU.Backends.SPIRV
{
    public partial class SPIRVCodeGenerator
    {
        #region Nested types

        public struct InstructionEmitter : IDisposable
        {
            #region Instance

            private readonly StringBuilder stringBuilder;

            public InstructionEmitter(StringBuilder builder)
            {
                stringBuilder = builder;
            }

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() => stringBuilder.AppendLine();

            #endregion
        }

        #endregion
    }
}
