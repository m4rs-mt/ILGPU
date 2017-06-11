using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;

namespace ILGPU.Tests
{
    [TestClass]
    public class ArrayViewTests
    {
        #region Instance

        private int[] data;
        private GCHandle handle;
        private ArrayView<int> dataView;

        [TestInitialize()]
        public void Initialize()
        {
            data = new int[1024];
            for (int i = 0; i < data.Length; ++i)
                data[i] = i;
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            dataView = new ArrayView<int>(
                handle.AddrOfPinnedObject(),
                data.Length);
        }

        [TestCleanup()]
        public void Cleanup()
        {
            handle.Free();
        }

        #endregion

        [TestMethod]
        public void ArrayViewLength()
        {
            Assert.AreEqual(data.Length, dataView.Length);
            Assert.AreEqual(data.Length * Interop.SizeOf<int>(), dataView.LengthInBytes);
        }

        [TestMethod]
        public void ArrayViewData()
        {
            Assert.AreEqual(handle.AddrOfPinnedObject(), dataView.Pointer);
            for (int i = 0; i < dataView.Length; ++i)
            {
                Assert.AreEqual(dataView[i], data[i]);
                Assert.AreEqual(dataView.Load(i), data[i]);

                dataView.Store(i, i + 1);
                Assert.AreEqual(dataView[i], i + 1);

                var variable = dataView.GetVariableView(i);
                Assert.AreEqual(variable.Value, i + 1);
                Assert.AreEqual(variable.Load(), i + 1);

                variable.Store(i);
                Assert.AreEqual(dataView[i], i);
            }
        }

        [TestMethod]
        public void ArrayViewSubView()
        {
            Assert.AreEqual(dataView.GetVariableView(0).Pointer, handle.AddrOfPinnedObject());
            for (int i = 0; i < dataView.Length - 1; ++i)
                Assert.AreEqual(dataView.GetSubView(i).Length, dataView.Length - i);
            Assert.AreEqual(dataView.GetSubView(0, 1).Length, 1);
        }

        [TestMethod]
        public void ArrayViewCast()
        {
            var casted = dataView.Cast<int>();
            Assert.AreEqual(dataView.Length, casted.Length);
            Assert.AreEqual(dataView.Pointer, casted.Pointer);
            Assert.AreEqual(casted, dataView.Cast<int>());

            var casted64 = dataView.Cast<long>();
            Assert.AreEqual(dataView.Length, casted64.Length * 2);
            Assert.AreEqual(dataView.Pointer, casted64.Pointer);

            var casted16 = dataView.Cast<short>();
            Assert.AreEqual(dataView.Length, casted16.Length / 2);
            Assert.AreEqual(dataView.Pointer, casted16.Pointer);
        }
    }
}
