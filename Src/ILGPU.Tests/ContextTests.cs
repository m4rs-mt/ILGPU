using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ILGPU.Backends;

namespace ILGPU.Tests
{
    [TestClass]
    public class ContextTests
    {
        [TestMethod]
        public void ContextCreate()
        {
            using (var context = new Context())
            {
                Assert.AreEqual(0, context.DeviceFunctions.Count);
                Assert.AreEqual(0, context.DeviceTypes.Count);
                Assert.AreNotEqual(IntPtr.Zero, context.LLVMContext.Pointer);
            }
        }

        [TestMethod]
        public void MultipleContexts()
        {
            using (var context = new Context())
            {
                using (var context2 = new Context())
                {
                    Assert.AreNotEqual(context.LLVMContext.Pointer, context2.LLVMContext.Pointer);
                }
            }
        }

        [TestMethod]
        public void CreateCompileUnit()
        {
            const string Name = "[TestUnit]";
            const CompileUnitFlags Flags = CompileUnitFlags.InlineMutableStaticFieldValues | CompileUnitFlags.PTXFlushDenormalsToZero;
            using (var context = new Context())
            {
                using (var backend = new MSILBackend(context))
                {
                    Assert.AreEqual(context, backend.Context);
                    using (var unit = context.CreateCompileUnit(backend, Flags, Name))
                    {
                        Assert.AreSame(context, unit.Context);
                        Assert.AreSame(backend, unit.Backend);
                        Assert.AreEqual(Flags, unit.Flags);
                        Assert.AreEqual(Name, unit.Name);
                        Assert.AreNotEqual(IntPtr.Zero, unit.LLVMModule.Pointer);
                    }
                }
            }
        }
    }
}
