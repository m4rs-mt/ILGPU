using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using ILGPU.Runtime;

namespace ILGPU.Tests
{
    public static class TestConfigurations
    {
        static TestConfigurations()
        {
            var configs = new List<TestConfiguration>();
            foreach (var id in Accelerator.Accelerators)
            {
                switch (id.AcceleratorType)
                {
                    case AcceleratorType.CPU:
                        configs.Add(new TestConfiguration(id));
                        configs.Add(new TestConfiguration(id, ContextFlags.SkipCPUCodeGeneration));
                        break;
                    default:
                        configs.Add(new TestConfiguration(id));
                        break;
                }
            }

            Default = configs.Select(x => new[] {x});
        }

        public static IEnumerable<object[]> Default { get; }
    }
}