using System;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ILGPU.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            CudaAccelerator cuda = (CudaAccelerator) Accelerator.Create(new Context(), CudaAccelerator.CudaAccelerators[0]);
            
            
        }
    }
}
