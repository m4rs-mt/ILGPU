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

            private readonly ISPIRVBuilder spirvBuilder;

            public InstructionEmitter(ISPIRVBuilder builder)
            {
                spirvBuilder = builder;
            }

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() {}

            #endregion
        }

        #endregion
    }
}
