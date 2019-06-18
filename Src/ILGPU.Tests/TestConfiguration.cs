using ILGPU.Runtime;

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

        public Accelerator CreateAccelerator(Context context)
        {
            return Accelerator.Create(context, AcceleratorId);
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