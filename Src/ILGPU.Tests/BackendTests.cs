using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ILGPU.Backends;
using System.Reflection;
using System.Collections.Generic;

namespace ILGPU.Tests
{
    [TestClass]
    public class BackendTests
    {
        #region Test Kernels

        // Kernels

        private static void TestKernel1D(Index index)
        { }

        private static readonly MethodInfo TestKernel1DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernel1D), BindingFlags.NonPublic | BindingFlags.Static);

        private static void TestKernel2D(Index2 index)
        { }

        private static readonly MethodInfo TestKernel2DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernel2D), BindingFlags.NonPublic | BindingFlags.Static);

        private static void TestKernel3D(Index3 index)
        { }

        private static readonly MethodInfo TestKernel3DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernel3D), BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly Tuple<MethodInfo, IndexType>[] UngroupedKernels =
        {
            new Tuple<MethodInfo, IndexType>(TestKernel1DInfo, IndexType.Index1D),
            new Tuple<MethodInfo, IndexType>(TestKernel2DInfo, IndexType.Index2D),
            new Tuple<MethodInfo, IndexType>(TestKernel3DInfo, IndexType.Index3D),
        };

        // Grouped kernels

        private static void TestKernelGrouped1D(GroupedIndex index)
        { }

        private static readonly MethodInfo TestKernelGrouped1DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernelGrouped1D), BindingFlags.NonPublic | BindingFlags.Static);

        private static void TestKernelGrouped2D(GroupedIndex2 index)
        { }

        private static readonly MethodInfo TestKernelGrouped2DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernelGrouped2D), BindingFlags.NonPublic | BindingFlags.Static);

        private static void TestKernelGrouped3D(GroupedIndex3 index)
        { }

        private static readonly MethodInfo TestKernelGrouped3DInfo =
            typeof(BackendTests).GetMethod(nameof(TestKernelGrouped3D), BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly Tuple<MethodInfo, IndexType>[] GroupedKernels =
        {
            new Tuple<MethodInfo, IndexType>(TestKernelGrouped1DInfo, IndexType.GroupedIndex1D),
            new Tuple<MethodInfo, IndexType>(TestKernelGrouped2DInfo, IndexType.GroupedIndex2D),
            new Tuple<MethodInfo, IndexType>(TestKernelGrouped3DInfo, IndexType.GroupedIndex3D),
        };

        // All kernels

        private static readonly Tuple<MethodInfo, IndexType>[] AllKernels;

        static BackendTests()
        {
            var allKernels = new List<Tuple<MethodInfo, IndexType>>();

            allKernels.AddRange(UngroupedKernels);
            allKernels.AddRange(GroupedKernels);

            AllKernels = allKernels.ToArray();
        }

        #endregion

        #region Instance

        private Context context;

        [TestInitialize()]
        public void Initialize()
        {
            context = new Context();
        }

        [TestCleanup()]
        public void Cleanup()
        {
            context.Dispose();
        }

        #endregion

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InvalidBackend()
        {
            context.CreateCompileUnit(null);
        }

        [TestMethod]
        public void SimpleMSILBackendInput()
        {
            using (var backend = new MSILBackend(context))
            {
                using (var unit = context.CreateCompileUnit(backend))
                {
                    foreach (var kernelMethod in AllKernels)
                    {
                        var kernel = backend.Compile(unit, kernelMethod.Item1);
                        Assert.AreEqual(0, kernel.GetBuffer().Length);
                        Assert.AreSame(kernelMethod.Item1, kernel.SourceMethod);
                        Assert.AreEqual(kernelMethod.Item2, kernel.IndexType);
                    }
                }
            }
        }

        [TestMethod]
        public void SimplePTXBackendInput()
        {
            using (var backend = new PTXBackend(context, PTXArchitecture.SM_50))
            {
                Assert.IsNotNull(backend.LibDevicePath);
                Assert.IsTrue(System.IO.File.Exists(backend.LibDevicePath));
                using (var unit = context.CreateCompileUnit(backend))
                {
                    foreach (var kernelMethod in AllKernels)
                    {
                        var kernel = backend.Compile(unit, kernelMethod.Item1);
                        Assert.AreNotEqual(0, kernel.GetBuffer().Length);
                        Assert.AreSame(kernelMethod.Item1, kernel.SourceMethod);
                        Assert.AreEqual(kernelMethod.Item2, kernel.IndexType);
                    }
                }
            }
        }
    }
}
