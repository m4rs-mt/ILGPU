using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
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
        /// <returns>The resolved kernel method.</returns>
        internal static MethodInfo GetKernelMethod(Type type, string name)
        {
            var method = type.GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
                throw new InvalidOperationException($"Could not find kernel method '{name}' of type '{type}'");
            return method;
        }

        #endregion

        /// <summary>
        /// Constructs a new test base.
        /// </summary>
        /// <param name="output">The associated output module.</param>
        /// <param name="contextProvider">The context provider to use.</param>
        protected TestBase(ITestOutputHelper output, ContextProvider contextProvider)
        {
            Output = output;
            ContextProvider = contextProvider;

            Context = contextProvider.CreateContext();
            Assert.True(Context != null, "Invalid context");

            Context.EnableAlgorithms();

            Accelerator = contextProvider.CreateAccelerator(Context);
            Assert.True(Accelerator != null, "Accelerator not supported");

            TestType = GetType();
        }

        #region Properties

        /// <summary>
        /// Returns the associated context provider.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0052:Remove unread private members", Justification = "Used in T4 templates")]
        private ContextProvider ContextProvider { get; }

        /// <summary>
        /// Returns the output helper.
        /// </summary>
        public ITestOutputHelper Output { get; }

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

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
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex>(string kernelName, TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
        {
            Execute(GetKernelMethod(TestType, kernelName), dimension, arguments);
        }

        /// <summary>
        /// Executes the specified kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="kernel">The kernel method.</param>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex>(MethodInfo kernel, TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
        {
            Accelerator.DefaultStream.Synchronize();
            Accelerator.Synchronize();

            // Compile kernel manually and load the compiled kernel into the accelerator
            var backend = Accelerator.Backend;
            Output.WriteLine($"Compiling '{kernel.Name}'");
            var compiled = backend.Compile(kernel, new KernelSpecialization());

            // Load the compiled kernel
            Output.WriteLine($"Loading '{kernel.Name}'");
            using var acceleratorKernel = Accelerator.LoadKernel(compiled);

            // Launch the kernel
            Output.WriteLine($"Launching '{kernel.Name}'");
            acceleratorKernel.Launch(Accelerator.DefaultStream, dimension, arguments);

            Accelerator.DefaultStream.Synchronize();
            Accelerator.Synchronize();
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute(int dimension, params object[] arguments)
        {
            Execute(new Index(dimension), arguments);
        }

        /// <summary>
        /// Executes an implicitly linked kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The dimension.</param>
        /// <param name="arguments">The arguments.</param>
        public void Execute<TIndex>(TIndex dimension, params object[] arguments)
            where TIndex : struct, IIndex
        {
            var kernelMethod = KernelMethodAttribute.GetKernelMethod();
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
            where T : struct
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
            where T : struct
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
            where T : struct
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
            if (disposing)
            {
                Accelerator.Dispose();
                Context.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
