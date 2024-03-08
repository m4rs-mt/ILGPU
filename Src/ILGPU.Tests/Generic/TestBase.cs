// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    /// <summary>
    /// The test base class for all tests.
    /// </summary>
    public abstract class TestBase : DisposeBase
    {
        #region Static

        /// <summary>
        /// The name of the environment variable to control whether to clear all caches
        /// after running a test to have a clean test environment.
        /// </summary>
        public static readonly string CleanTestsEnvironmentVariable = "ILGPU_CLEAN_TESTS";

        /// <summary>
        /// Returns true if all tests should be executed in a clean environment.
        /// </summary>
        public static bool CleanTests
        {
            get
            {
                var env = Environment.GetEnvironmentVariable(
                    CleanTestsEnvironmentVariable);
                return !string.IsNullOrWhiteSpace(env)
                       && !env.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
                       && env != "0";
            }
        }

        /// <summary>
        /// Resolves a kernel method.
        /// </summary>
        /// <param name="type">The parent type.</param>
        /// <param name="name">The kernel name to look for.</param>
        /// <param name="typeArguments">The type arguments.</param>
        /// <returns>The resolved kernel method.</returns>
        internal static MethodInfo GetKernelMethod(
            Type type,
            string name,
            Type[]? typeArguments)
        {
            var method = type.GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
            {
                throw new InvalidOperationException(
                    $"Could not find kernel method '{name}' of type '{type}'");
            }
            if (method.IsGenericMethod)
            {
                if (typeArguments == null)
                    throw new ArgumentNullException(nameof(typeArguments));

                // Try to specialize the method
                method = method.MakeGenericMethod(typeArguments);
            }

            return method;
        }

        #endregion

        /// <summary>
        /// Constructs a new test base.
        /// </summary>
        /// <param name="output">The associated output module.</param>
        /// <param name="testContext">The test context instance to use.</param>
        protected TestBase(ITestOutputHelper output, TestContext testContext)
        {
            Output = output;

            TestContext = testContext;

            TestType = GetType();
        }

        #region Properties

        protected TestContext TestContext { get; }

        /// <summary>
        /// Returns the output helper.
        /// </summary>
        public ITestOutputHelper Output { get; }

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context => TestContext.Context;

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator => TestContext.Accelerator;

        /// <summary>
        /// Returns the associated test type.
        /// </summary>
        public Type TestType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Executes the specified kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="kernel">The kernel method.</param>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex>(
            MethodInfo kernel,
            TIndex dimension,
            params object[] arguments)
            where TIndex : struct, IIndex
        {
            Accelerator.Synchronize();
            using var stream = Accelerator.CreateStream();

            // Compile kernel manually and load the compiled kernel into the accelerator
            using var backend = Accelerator.GetBackend();
            Output.WriteLine($"Compiling '{kernel.Name}'");
            var entryPoint = typeof(TIndex) == typeof(KernelConfig)
                ? EntryPointDescription.FromExplicitlyGroupedKernel(kernel)
                : EntryPointDescription.FromImplicitlyGroupedKernel(kernel);

            var specialization = new KernelSpecialization();
            if (arguments.Length > 0 &&
                arguments[0] is KernelSpecialization userSpecialization)
            {
                // Use user-provided specialization
                specialization = userSpecialization;

                // Skip first argument
                var newArguments = new object[arguments.Length - 1];
                Array.Copy(arguments, 1, newArguments, 0, newArguments.Length);
                arguments = newArguments;
            }
            var compiled = backend.Compile(entryPoint, specialization);

            // Load the compiled kernel
            Output.WriteLine($"Loading '{kernel.Name}'");
            var acceleratorKernel = Accelerator.LoadKernel(compiled);

            // Launch the kernel
            Output.WriteLine($"Launching '{kernel.Name}'");
            acceleratorKernel.Launch(stream, dimension, arguments);

            stream.Synchronize();
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute(int dimension, params object[] arguments) =>
            Execute(new Index1D(dimension), arguments);

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute(long dimension, params object[] arguments) =>
            Execute(new LongIndex1D(dimension).ToIntIndex(), arguments);

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex>(TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
        {
            var kernelMethod = KernelMethodAttribute.GetKernelMethod(null);
            Execute(kernelMethod, dimension, arguments);
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex, T>(TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
            where T : unmanaged
        {
            var kernelMethod = KernelMethodAttribute.GetKernelMethod(new Type[]
            {
                typeof(T)
            });
            Execute(kernelMethod, dimension, arguments);
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex, T1, T2>(TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
            where T1 : unmanaged
            where T2 : unmanaged
        {
            var kernelMethod = KernelMethodAttribute.GetKernelMethod(new Type[]
            {
                typeof(T1),
                typeof(T2)
            });
            Execute(kernelMethod, dimension, arguments);
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex, T1, T2, T3>(
            TIndex dimension,
            params object[] arguments)
            where TIndex : struct, IIndex
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            var kernelMethod = KernelMethodAttribute.GetKernelMethod(new Type[]
            {
                typeof(T1),
                typeof(T2),
                typeof(T3)
            });
            Execute(kernelMethod, dimension, arguments);
        }

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="offset">The custom data offset to use (if any).</param>
        /// <param name="length">The custom data length to use (if any).</param>
        public void Verify<T>(
            ArrayView<T> view,
            T[] expected,
            int? offset = null,
            int? length = null)
            where T : unmanaged
        {
            var data = view.GetAsArray(Accelerator.DefaultStream);
            var dataLength = length ?? data.Length;
            Assert.True(dataLength <= data.Length);
            Assert.Equal(dataLength, expected.Length);
            for (int i = offset ?? 0, e = dataLength; i < e; ++i)
                Assert.Equal(expected[i], data[i]);
        }

        /// <summary>
        /// Verifies the contents of the given 1D arrays.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="expected">The expected values.</param>
        public static void Verify1D<T>(T[] source, T[] expected)
            where T : unmanaged
        {
            Assert.Equal(source.Length, expected.Length);
            for (int i = 0; i < expected.Length; ++i)
                Assert.Equal(expected[i], source[i]);
        }

        /// <summary>
        /// Verifies the contents of the given 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        [SuppressMessage(
            "Performance",
            "CA1814:Prefer jagged arrays over multidimensional",
            Justification = "Used for testing purposes")]
        public static void Verify2D<T, TStride>(
            ArrayView2D<T, TStride> view,
            T[,] expected)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            var data = view.AsContiguous().GetAsArray();
            Assert.Equal(data.Length, expected.Length);
            for (int i = 0; i < expected.GetLength(0); ++i)
                for (int j = 0; j < expected.GetLength(1); ++j)
                    Assert.Equal(expected[i, j], data[view.ComputeLinearIndex((i, j))]);
        }

        /// <summary>
        /// Verifies the contents of the given 2D arrays.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source values.</param>
        /// <param name="expected">The expected values.</param>
        [SuppressMessage(
            "Performance",
            "CA1814:Prefer jagged arrays over multidimensional",
            Justification = "Used for testing purposes")]
        public static void Verify2D<T>(T[,] source, T[,] expected)
            where T : unmanaged
        {
            Assert.Equal(source.Length, expected.Length);
            for (int i = 0; i < expected.GetLength(0); ++i)
                for (int j = 0; j < expected.GetLength(1); ++j)
                    Assert.Equal(expected[i, j], source[i, j]);
        }

        /// <summary>
        /// Verifies the contents of the given 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        [SuppressMessage(
            "Performance",
            "CA1814:Prefer jagged arrays over multidimensional",
            Justification = "Used for testing purposes")]
        public static void Verify3D<T, TStride>(
            ArrayView3D<T, TStride> view,
            T[,,] expected)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            var data = view.AsContiguous().GetAsArray();
            Assert.Equal(data.Length, expected.Length);
            for (int i = 0; i < expected.GetLength(0); ++i)
                for (int j = 0; j < expected.GetLength(1); ++j)
                    for (int k = 0; k < expected.GetLength(2); ++k)
                    {
                        Assert.Equal(
                            expected[i, j, k],
                            data[view.ComputeLinearIndex((i, j, k))]);
                    }
        }

        /// <summary>
        /// Verifies the contents of the given 3D arrays.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source values.</param>
        /// <param name="expected">The expected values.</param>
        [SuppressMessage(
            "Performance",
            "CA1814:Prefer jagged arrays over multidimensional",
            Justification = "Used for testing purposes")]
        public static void Verify3D<T>(T[,,] source, T[,,] expected)
            where T : unmanaged
        {
            Assert.Equal(source.Length, expected.Length);
            for (int i = 0; i < expected.GetLength(0); ++i)
                for (int j = 0; j < expected.GetLength(1); ++j)
                    for (int k = 0; k < expected.GetLength(2); ++k)
                        Assert.Equal(expected[i, j, k], source[i, j, k]);
        }

        /// <summary>
        /// Initializes a memory buffer with a constant.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The target buffer.</param>
        /// <param name="value">The value.</param>
        public void Initialize<T>(ArrayView<T> view, T value)
            where T : unmanaged
        {
            var data = new T[view.Length];
            for (int i = 0, e = data.Length; i < e; ++i)
                data[i] = value;
            view.CopyFromCPU(Accelerator.DefaultStream, data);
        }

        /// <summary>
        /// Initializes a memory buffer with a sequence.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="sequencer">The sequencer function.</param>
        public void Sequence<T>(ArrayView<T> view, Func<int, T> sequencer)
            where T : unmanaged
        {
            var data = new T[view.Length];
            for (int i = 0, e = data.Length; i < e; ++i)
                data[i] = sequencer(i);
            view.CopyFromCPU(Accelerator.DefaultStream, data);
        }

        #endregion

        #region IDisposable

        /// <summary cref="IDisposable.Dispose"/>
        protected override void Dispose(bool disposing)
        {
            if (CleanTests)
                TestContext.ClearCaches();
            base.Dispose(disposing);
        }

        #endregion
    }
}
