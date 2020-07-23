using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
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
        /// Resolves a kernel method.
        /// </summary>
        /// <param name="type">The parent type.</param>
        /// <param name="name">The kernel name to look for.</param>
        /// <param name="typeArguments">The type arguments.</param>
        /// <returns>The resolved kernel method.</returns>
        internal static MethodInfo GetKernelMethod(
            Type type,
            string name,
            Type[] typeArguments)
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

        private TestContext TestContext { get; }

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
            using var stream = Accelerator.CreateStream();

            // Compile kernel manually and load the compiled kernel into the accelerator
            var backend = Accelerator.Backend;
            Output.WriteLine($"Compiling '{kernel.Name}'");
            var entryPoint = typeof(TIndex) == typeof(KernelConfig)
                ? EntryPointDescription.FromExplicitlyGroupedKernel(kernel)
                : EntryPointDescription.FromImplicitlyGroupedKernel(kernel);
            var compiled = backend.Compile(entryPoint, new KernelSpecialization());

            // Load the compiled kernel
            Output.WriteLine($"Loading '{kernel.Name}'");
            using var acceleratorKernel = Accelerator.LoadKernel(compiled);

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
            Execute(new Index1(dimension), arguments);

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute(long dimension, params object[] arguments) =>
            Execute(new LongIndex1(dimension).ToIntIndex(), arguments);

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
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="offset">The custom data offset to use (if any).</param>
        /// <param name="length">The custom data length to use (if any).</param>
        public void Verify<T>(
            MemoryBuffer<T> buffer,
            T[] expected,
            int? offset = null,
            int? length = null)
            where T : unmanaged =>
            Verify(
                buffer.Buffer,
                expected,
                offset,
                length);

        /// <summary>
        /// Verifies the contents of the given memory buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="expected">The expected values.</param>
        /// <param name="offset">The custom data offset to use (if any).</param>
        /// <param name="length">The custom data length to use (if any).</param>
        public void Verify<T, TIndex>(
            MemoryBuffer<T, TIndex> buffer,
            T[] expected,
            int? offset = null,
            int? length = null)
            where T : unmanaged
            where TIndex : unmanaged, IGenericIndex<TIndex>
        {
            var data = buffer.GetAsArray(Accelerator.DefaultStream);
            Assert.Equal(data.Length, expected.Length);
            for (int i = offset ?? 0, e = length ?? data.Length; i < e; ++i)
                Assert.Equal(expected[i], data[i]);
        }

        /// <summary>
        /// Initializes a memory buffer with a constant.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="value">The value.</param>
        public void Initialize<T>(MemoryBuffer<T> buffer, T value)
            where T : unmanaged
        {
            var data = new T[buffer.Length];
            for (int i = 0, e = data.Length; i < e; ++i)
                data[i] = value;
            buffer.CopyFrom(Accelerator.DefaultStream, data, 0, 0, data.Length);
        }

        /// <summary>
        /// Initializes a memory buffer with a sequence.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="sequencer">The sequencer function.</param>
        public void Sequence<T>(MemoryBuffer<T> buffer, Func<int, T> sequencer)
            where T : unmanaged
        {
            var data = new T[buffer.Length];
            for (int i = 0, e = data.Length; i < e; ++i)
                data[i] = sequencer(i);
            buffer.CopyFrom(Accelerator.DefaultStream, data, 0, 0, data.Length);
        }

        #endregion

        #region IDisposable

        /// <summary cref="IDisposable.Dispose"/>
        protected override void Dispose(bool disposing)
        {
            TestContext.ClearCaches();
            base.Dispose(disposing);
        }

        #endregion
    }
}
