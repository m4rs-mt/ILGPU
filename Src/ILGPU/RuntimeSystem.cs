// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeSystem.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace ILGPU
{
    /// <summary>
    /// Represents the dynamic ILGPU assembly runtime system.
    /// </summary>
    public sealed class RuntimeSystem : DisposeBase, ICache
    {
        #region Constants

        /// <summary>
        /// The name of the dynamic runtime assembly.
        /// </summary>
        internal const string AssemblyName = "ILGPURuntime";

        /// <summary>
        /// A custom runtime type name.
        /// </summary>
        private const string CustomTypeName = "ILGPURuntimeType";

        /// <summary>
        /// A default launcher name.
        /// </summary>
        private const string LauncherMethodName = "ILGPULauncher";

        #endregion

        #region Nested Types

        /// <summary>
        /// A scoped lock that can be used in combination with a
        /// <see cref="RuntimeSystem"/> instance.
        /// </summary>
        public readonly struct ScopedLock : IDisposable
        {
            private readonly WriteScopedLock writeScope;

            internal ScopedLock(RuntimeSystem parent)
            {
                writeScope = parent.assemblyLock.EnterWriteScope();

                Parent = parent;
                AssemblyVersion = parent.assemblyVersion;
            }

            /// <summary>
            /// Returns the parent runtime system instance.
            /// </summary>
            public RuntimeSystem Parent { get; }

            /// <summary>
            /// Returns the original assembly version this lock has been created from.
            /// </summary>
            /// <remarks>
            /// This information is used to detect internal assembly corruption.
            /// </remarks>
            public int AssemblyVersion { get; }

            /// <summary>
            /// Releases the lock.
            /// </summary>
            public readonly void Dispose()
            {
                // Verify the assembly version
                Trace.Assert(
                    AssemblyVersion == Parent.assemblyVersion,
                    RuntimeErrorMessages.InvalidConcurrentModification);
                writeScope.Dispose();
            }
        }

        /// <summary>
        /// Represents a method builder in the .Net world.
        /// </summary>
        public readonly struct MethodEmitter
        {
            /// <summary>
            /// Constructs a new method emitter.
            /// </summary>
            /// <param name="method">The desired internal method.</param>
            public MethodEmitter(
                DynamicMethod method)
            {
                Method = method;
                ILGenerator = method.GetILGenerator();
            }

            /// <summary>
            /// Returns the associated method builder.
            /// </summary>
            private DynamicMethod Method { get; }

            /// <summary>
            /// Returns the internal IL generator.
            /// </summary>
            public ILGenerator ILGenerator { get; }

            /// <summary>
            /// Finishes the building process.
            /// </summary>
            /// <returns>The emitted method.</returns>
            public MethodInfo Finish() => Method;
        }

        /// <summary>
        /// Represents a dynamically imported runtime method.
        /// </summary>
        private readonly struct ImportMethod
        {
            /// <summary>
            /// Constructs a new import method.
            /// </summary>
            /// <param name="method">The source method to implement.</param>
            /// <param name="attribute">The defined dynamic import attribute.</param>
            public ImportMethod(MethodInfo method, DynamicImportAttribute attribute)
            {
                Method = method;
                Attribute = attribute;
            }

            /// <summary>
            /// Returns the abstract source method to implement.
            /// </summary>
            public MethodInfo Method { get; }

            /// <summary>
            /// Returns the associated dynamic import attribute.
            /// </summary>
            public DynamicImportAttribute Attribute { get; }

            /// <summary>
            /// Returns the return type of the source method.
            /// </summary>
            public Type ReturnType => Method.GetReturnType();

            /// <summary>
            /// Returns all parameters of the source method.
            /// </summary>
            public ParameterInfo[] GetParameters() => Method.GetParameters();

            /// <summary>
            /// Returns all parameter types of the source method.
            /// </summary>
            public Type[] GetParameterTypes() =>
                GetParameters().Select(t => t.ParameterType).ToArray();

            /// <summary>
            /// Defines a new runtime implementation using the given type builder.
            /// </summary>
            /// <param name="typeBuilder">The type builder to use.</param>
            /// <returns>The created method builder.</returns>
            public MethodBuilder DefineImplementationMethod(TypeBuilder typeBuilder) =>
                typeBuilder.DefineMethod(
                    Method.Name,
                    Method.Attributes & ~ImplAttributesToClear,
                    Method.CallingConvention,
                    ReturnType,
                    GetParameterTypes());
        }

        #endregion

        #region Static

        /// <summary>
        /// All method attributes to clear when implementing a wrapper method.
        /// </summary>
        private const MethodAttributes ImplAttributesToClear =
             MethodAttributes.Abstract | MethodAttributes.NewSlot;

        /// <summary>
        /// The constructor of the class <see cref="NotSupportedException"/>.
        /// </summary>
        private static readonly ConstructorInfo NotSupportedExceptionConstructor =
            typeof(NotSupportedException)
            .GetConstructor(new Type[] { typeof(string) });

        /// <summary>
        /// The constructor of the class
        /// <see cref="SuppressUnmanagedCodeSecurityAttribute"/>.
        /// </summary>
        private static readonly ConstructorInfo SuppressCodeSecurityConstructor =
            typeof(SuppressUnmanagedCodeSecurityAttribute)
            .GetConstructor(Array.Empty<Type>());

        /// <summary>
        /// Implements all given abstract methods by throwing
        /// <see cref="NotSupportedException"/>s.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="methods">The methods to implement.</param>
        /// <param name="errorMessage">
        /// The error message to use for all exceptions.
        /// </param>
        private static void ImplementNotSupported(
            TypeBuilder typeBuilder,
            ImportMethod[] methods,
            string errorMessage)
        {
            foreach (var importMethod in methods)
            {
                // Define the implementation method
                var notImplementedMethod =
                    importMethod.DefineImplementationMethod(typeBuilder);
                DefineWrapperParameters(
                    notImplementedMethod,
                    importMethod.GetParameters());

                // Define wrapper method body
                var generator = new ILEmitter(notImplementedMethod.GetILGenerator());
                generator.EmitConstant(errorMessage);
                generator.EmitNewObject(NotSupportedExceptionConstructor);
                generator.Emit(OpCodes.Throw);
                generator.Emit(OpCodes.Ret);

                // Define method override
                typeBuilder.DefineMethodOverride(
                    notImplementedMethod,
                    importMethod.Method);
            }
        }

        /// <summary>
        /// Implements all given abstract methods by using their p-invoke targets.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="libraryName">The native library name.</param>
        /// <param name="methods">The methods to implement.</param>
        private static void ImplementImports(
            TypeBuilder typeBuilder,
            string libraryName,
            ImportMethod[] methods)
        {
            foreach (var importMethod in methods)
            {
                var method = importMethod.Method;
                var parameters = importMethod.GetParameters();
                var parameterTypes = importMethod.GetParameterTypes();

                // Define a new p-invoke compatible entry point method
                var pInvokeMethod = typeBuilder.DefineMethod(
                    importMethod.Attribute.GetEntryPoint(method),
                    MethodAttributes.Private |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig |
                    MethodAttributes.PinvokeImpl,
                    CallingConventions.Standard,
                    importMethod.ReturnType,
                    parameterTypes);
                pInvokeMethod.SetCustomAttribute(
                    SuppressCodeSecurityConstructor,
                    Array.Empty<byte>());
                pInvokeMethod.SetCustomAttribute(
                    importMethod.Attribute.ToImportAttributeBuilder(
                        libraryName,
                        method));
                DefineMarshalParameters(pInvokeMethod, parameters);

                // Define a new wrapper implementation that invokes the p-invoke target
                var wrapperMethod = importMethod.DefineImplementationMethod(typeBuilder);
                DefineWrapperParameters(wrapperMethod, parameters);

                // Define wrapper method body
                var generator = new ILEmitter(wrapperMethod.GetILGenerator());
                for (int i = 0, e = parameters.Length; i < e; ++i)
                    generator.Emit(ArgumentOperation.Load, i + 1);
                generator.EmitCall(pInvokeMethod);
                generator.Emit(OpCodes.Ret);

                // Define method override
                typeBuilder.DefineMethodOverride(wrapperMethod, method);
            }
        }

        /// <summary>
        /// Implements the <see cref="RuntimeAPI.IsSupported"/> property.
        /// </summary>
        private static void ImplementIsSupported<T>(
            TypeBuilder typeBuilder,
            bool isSupported)
            where T : RuntimeAPI
        {
            var abstractMethod = typeof(T)
                .GetProperty(nameof(RuntimeAPI.IsSupported))
                .GetGetMethod();
            var wrapperMethod = typeBuilder.DefineMethod(
                abstractMethod.Name,
                abstractMethod.Attributes & ~ImplAttributesToClear,
                abstractMethod.GetReturnType(),
                Array.Empty<Type>());
            var generator = new ILEmitter(wrapperMethod.GetILGenerator());
            generator.EmitConstant(Convert.ToInt32(isSupported));
            generator.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(wrapperMethod, abstractMethod);
        }

        /// <summary>
        /// Defines parameters of managed wrapper methods.
        /// </summary>
        /// <param name="methodBuilder">The parent method builder.</param>
        /// <param name="parameters">The source parameters.</param>
        private static void DefineWrapperParameters(
            MethodBuilder methodBuilder,
            ParameterInfo[] parameters)
        {
            // Bind parameter attributes
            foreach (var param in parameters)
            {
                methodBuilder.DefineParameter(
                    param.Position,
                    param.IsOut ? ParameterAttributes.Out : ParameterAttributes.None,
                    param.Name);
            }
        }

        /// <summary>
        /// Defines parameters of p-invoke entry-point methods.
        /// </summary>
        /// <param name="methodBuilder">The parent method builder.</param>
        /// <param name="parameters">The source parameters.</param>
        private static void DefineMarshalParameters(
            MethodBuilder methodBuilder,
            ParameterInfo[] parameters)
        {
            // Bind parameter attributes
            foreach (var param in parameters)
            {
                var newParam = methodBuilder.DefineParameter(
                    param.Position,
                    param.Attributes,
                    param.Name);

                foreach (var attributeData in param.GetCustomAttributesData())
                {
                    newParam.SetCustomAttribute(new CustomAttributeBuilder(
                        attributeData.Constructor,
                        attributeData.ConstructorArguments
                            .Select(t => t.Value).ToArray(),
                        attributeData.NamedArguments
                            .Select(t => (FieldInfo)t.MemberInfo).ToArray(),
                        attributeData.NamedArguments
                            .Select(t => t.TypedValue.Value).ToArray()));
                }
            }
        }

        /// <summary>
        /// The globally unique assembly version.
        /// </summary>
        private static volatile int globalAssemblyVersion;

        /// <summary>
        /// Determines the next global assembly version.
        /// </summary>
        private static int GetNextAssemblyVersion() =>
            Interlocked.Add(ref globalAssemblyVersion, 1);

        #endregion

        #region Instance

        private readonly ReaderWriterLockSlim assemblyLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);

        private AssemblyBuilder assemblyBuilder;
        private ModuleBuilder moduleBuilder;

        private volatile int assemblyVersion;
        private volatile int typeBuilderIdx;

        /// <summary>
        /// Constructs a new runtime system.
        /// </summary>
        public RuntimeSystem()
        {
            // Initialize assembly builder and context data
            ReloadAssemblyBuilder();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reloads the assembly builder.
        /// </summary>
        private void ReloadAssemblyBuilder()
        {
            using var writerLock = assemblyLock.EnterWriteScope();

            assemblyVersion = GetNextAssemblyVersion();
            var assemblyName = new AssemblyName(AssemblyName)
            {
                Version = new Version(1, assemblyVersion),
            };
            assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.RunAndCollect);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        }

        /// <summary>
        /// Defines a new runtime type.
        /// </summary>
        /// <param name="attributes">The custom type attributes.</param>
        /// <param name="baseClass">The base class.</param>
        /// <param name="typeBuilder">The type builder.</param>
        /// <returns>The acquired scoped lock.</returns>
        private ScopedLock DefineRuntimeType(
            TypeAttributes attributes,
            Type baseClass,
            out TypeBuilder typeBuilder)
        {
            var scopedLock = new ScopedLock(this);

            typeBuilder = moduleBuilder.DefineType(
                CustomTypeName + typeBuilderIdx++,
                attributes,
                baseClass);

            return scopedLock;
        }

        /// <summary>
        /// Defines a new runtime class.
        /// </summary>
        /// <param name="baseClass">The base class.</param>
        /// <param name="typeBuilder">The type builder.</param>
        /// <returns>The acquired scoped lock.</returns>
        public ScopedLock DefineRuntimeClass(
            Type baseClass,
            out TypeBuilder typeBuilder) =>
            DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout |
                TypeAttributes.Sealed,
                baseClass ?? typeof(object),
                out typeBuilder);

        /// <summary>
        /// Defines a new runtime structure.
        /// </summary>
        /// <param name="typeBuilder">The type builder.</param>
        /// <returns>The acquired scoped lock.</returns>
        public ScopedLock DefineRuntimeStruct(out TypeBuilder typeBuilder) =>
            DefineRuntimeStruct(false, out typeBuilder);

        /// <summary>
        /// Defines a new runtime structure.
        /// </summary>
        /// <param name="explicitLayout">
        /// True, if the individual fields have an explicit structure layout.
        /// </param>
        /// <param name="typeBuilder">The type builder.</param>
        /// <returns>The acquired scoped lock.</returns>
        public ScopedLock DefineRuntimeStruct(
            bool explicitLayout,
            out TypeBuilder typeBuilder) =>
            DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.Sealed |
                (explicitLayout
                    ? TypeAttributes.ExplicitLayout
                    : TypeAttributes.SequentialLayout),
                typeof(ValueType),
                out typeBuilder);

        /// <summary>
        /// Defines a new runtime method.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        /// <param name="parameterTypes">All parameter types.</param>
        /// <param name="methodEmitter">The method emitter.</param>
        /// <returns>The acquired scoped lock.</returns>
        public ScopedLock DefineRuntimeMethod(
            Type returnType,
            Type[] parameterTypes,
            out MethodEmitter methodEmitter)
        {
            var scopedLock = new ScopedLock(this);

            methodEmitter = new MethodEmitter(
                new DynamicMethod(
                    LauncherMethodName,
                    returnType,
                    parameterTypes,
                    moduleBuilder,
                    true));

            return scopedLock;
        }

        /// <summary>
        /// Creates a new DLL-interop proxy type instance.
        /// </summary>
        /// <typeparam name="T">The abstract API type.</typeparam>
        /// <param name="callback">
        /// The custom callback instance that constructs the internals of the wrapper
        /// type implementation.
        /// </param>
        /// <returns>The created proxy-type instance.</returns>
        private T CreateDllWrapper<T>(
            Action<TypeBuilder, ImportMethod[]> callback)
            where T : class
        {
            // Verify whether the class is an abstract class
            var classType = typeof(T);
            if (!classType.IsAbstract)
                throw new InvalidOperationException();

            // Get all methods that need to be implemented by the wrapper type
            var methods = classType.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance)
                .Where(t => t.IsAbstract)
                .Select(t => new ImportMethod
                (
                    t,
                    t.GetCustomAttribute<DynamicImportAttribute>()
                ))
                .Where(t => t.Attribute != null).ToArray();

            if (methods.Length < 1)
                throw new InvalidOperationException();

            // Define the runtime class an implement all functions
            using var writeScope = DefineRuntimeClass(
                classType,
                out var implTypeBuilder);
            callback(implTypeBuilder, methods);

            // Create the actual implementation instance
            var implType = implTypeBuilder.CreateType();
            return Activator.CreateInstance(implType) as T;
        }

        /// <summary>
        /// Creates a new DLL-interop proxy type instance.
        /// </summary>
        /// <typeparam name="T">The abstract API type.</typeparam>
        /// <returns>The created proxy-type instance.</returns>
        internal T CreateDllWrapper<T>(string libraryName)
            where T : RuntimeAPI =>
            CreateDllWrapper<T>((builder, methods) =>
            {
                ImplementImports(builder, libraryName, methods);
                ImplementIsSupported<T>(builder, true);
            });

        /// <summary>
        /// Creates a new DLL-interop proxy type instance that throw
        /// <see cref="NotSupportedException"/> exceptions.
        /// </summary>
        /// <typeparam name="T">The abstract API type.</typeparam>
        /// <param name="errorMessage"></param>
        /// <returns>The created proxy-type instance.</returns>
        internal T CreateNotSupportedDllWrapper<T>(string errorMessage)
            where T : RuntimeAPI =>
            CreateDllWrapper<T>((builder, methods) =>
            {
                ImplementNotSupported(builder, methods, errorMessage);
                ImplementIsSupported<T>(builder, false);
            });

        /// <summary>
        /// Creates a platform-compatible DLL-interop wrapper type.
        /// </summary>
        /// <typeparam name="T">The abstract API type.</typeparam>
        /// <param name="windows">The native library name on Windows.</param>
        /// <param name="linux">The native library name on Linux.</param>
        /// <param name="macos">The native library name on MacOS.</param>
        /// <param name="errorMessage">
        /// The custom error message for not-supported platforms.
        /// </param>
        /// <returns>The created wrapper type.</returns>
        /// <remarks>
        /// If the current platform is not compatible with the native OS platform, the
        /// associated native library could not be loaded or the interop wrapper could
        /// not be initialized, this function returns a "not-supported wrapper"
        /// implementation. This instance implements all entry points by throwing
        /// instances of type <see cref="NotSupportedException"/>
        /// </remarks>
        public T CreateDllWrapper<T>(
            string windows,
            string linux,
            string macos,
            string errorMessage)
            where T : RuntimeAPI
        {
            if (!Backends.Backend.RunningOnNativePlatform)
                return CreateNotSupportedDllWrapper<T>(errorMessage);
            try
            {
                T instance =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? CreateDllWrapper<T>(linux)
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? CreateDllWrapper<T>(macos)
                    : CreateDllWrapper<T>(windows);
                // Try to initialize the new interface
                if (!instance.Init())
                    instance = CreateNotSupportedDllWrapper<T>(errorMessage);
                return instance;
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException)
            {
                // In case of a critical initialization exception
                // fall back to the not supported API
                return CreateNotSupportedDllWrapper<T>(errorMessage);
            }
        }

        #endregion

        #region ICache

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">Not used.</param>
        public void ClearCache(ClearCacheMode mode) => ReloadAssemblyBuilder();

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the internal assembly lock.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                assemblyLock.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
