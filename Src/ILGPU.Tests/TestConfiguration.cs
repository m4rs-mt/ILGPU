using ILGPU.Runtime;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public class TestConfiguration
    {
        public AcceleratorId AcceleratorId { get; }
        public ContextFlags ContextFlags { get; }

        public TestConfiguration(AcceleratorId acceleratorId)
        {
            AcceleratorId = acceleratorId;
        }

        public TestConfiguration(AcceleratorId acceleratorId, ContextFlags contextFlags)
        {
            AcceleratorId = acceleratorId;
            ContextFlags = contextFlags;
        }

        public Context CreateContext()
        {
            return new Context(ContextFlags);
        }
        
        public Accelerator CreateAccelerator(Context context, ITestOutputHelper outputHelper = null)
        {
            var accelerator = Accelerator.Create(context, AcceleratorId);
            outputHelper?.WriteLine($"Performing operations on {accelerator}");
            return accelerator;
        }

        public override string ToString()
        {
            var str = AcceleratorId.AcceleratorType.ToString();
            if (AcceleratorId.DeviceId != 0)
                str += $" #{AcceleratorId.DeviceId}";
            if (ContextFlags != ContextFlags.None)
                str += $" [{ContextFlags}]";
            return str;
        }
    }
}