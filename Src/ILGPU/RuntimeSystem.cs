// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RuntimeSystem.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Util;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;

namespace ILGPU
{
    /// <summary>
    /// Represents the dynamic ILGPU assembly runtime system.
    /// </summary>
    public sealed class RuntimeSystem : ICache
    {
        #region Constants

        /// <summary>
        /// The name of the dynamic runtime assembly.
        /// </summary>
        public const string AssemblyName = "ILGPURuntime";

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

        #endregion

        #region Static Instance

        /// <summary>
        /// Returns the static runtime-system instance.
        /// </summary>
        public static RuntimeSystem Instance { get; } = new RuntimeSystem();

        #endregion

        #region Instance

        private readonly object assemblyLock = new object();
        private int assemblyVersion = 0;
        private AssemblyBuilder assemblyBuilder;
        private ModuleBuilder moduleBuilder;
        private volatile int typeBuilderIdx = 0;

        /// <summary>
        /// Constructs a new runtime system.
        /// </summary>
        private RuntimeSystem()
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
            lock (assemblyLock)
            {
                var assemblyName = new AssemblyName(AssemblyName)
                {
                    Version = new Version(1, assemblyVersion++),
                };
                assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    assemblyName,
                    AssemblyBuilderAccess.RunAndCollect);
                moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
            }
        }

        /// <summary>
        /// Defines a new runtime type.
        /// </summary>
        /// <param name="attributes">The custom type attributes.</param>
        /// <param name="baseClass">The base class.</param>
        /// <returns>A new runtime type builder.</returns>
        private TypeBuilder DefineRuntimeType(TypeAttributes attributes, Type baseClass)
        {
            lock (assemblyLock)
            {
                return moduleBuilder.DefineType(
                    CustomTypeName + typeBuilderIdx++,
                    attributes,
                    baseClass);
            }
        }

        /// <summary>
        /// Defines a new runtime class.
        /// </summary>
        /// <returns>A new runtime type builder.</returns>
        public TypeBuilder DefineRuntimeClass(Type baseClass) =>
            DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout |
                TypeAttributes.Sealed,
                baseClass ?? typeof(object));

        /// <summary>
        /// Defines a new runtime structure.
        /// </summary>
        /// <returns>A new runtime type builder.</returns>
        public TypeBuilder DefineRuntimeStruct() => DefineRuntimeStruct(false);

        /// <summary>
        /// Defines a new runtime structure.
        /// </summary>
        /// <param name="explicitLayout">
        /// True, if the individual fields have an explicit structure layout.
        /// </param>
        /// <returns>A new runtime type builder.</returns>
        public TypeBuilder DefineRuntimeStruct(bool explicitLayout) =>
            DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.Sealed |
                (explicitLayout
                    ? TypeAttributes.ExplicitLayout
                    : TypeAttributes.SequentialLayout),
                typeof(ValueType));

        /// <summary>
        /// Defines a new runtime method.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        /// <param name="parameterTypes">All parameter types.</param>
        /// <returns>The defined method.</returns>
        public MethodEmitter DefineRuntimeMethod(
            Type returnType,
            Type[] parameterTypes)
        {
            var typeBuilder = DefineRuntimeStruct();
            var type = typeBuilder.CreateType();

            var method = new DynamicMethod(
                LauncherMethodName,
                typeof(void),
                parameterTypes,
                type,
                true);
            return new MethodEmitter(method);
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
            var implTypeBuilder = DefineRuntimeClass(classType);
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
        public void ClearCache(ClearCacheMode mode) =>
            ReloadAssemblyBuilder();

        #endregion
    }

    /// <summary>
    /// An abstract runtime API that can be used in combination with the dynamic DLL
    /// loader functionality of the class <see cref="RuntimeSystem"/>.
    /// </summary>
    public abstract class RuntimeAPI
    {
        /// <summary>
        /// Loads a runtime API that is implemented via compile-time known classes.
        /// </summary>
        /// <typeparam name="T">The abstract class type to implement.</typeparam>
        /// <typeparam name="TWindows">The Windows implementation.</typeparam>
        /// <typeparam name="TLinux">The Linux implementation.</typeparam>
        /// <typeparam name="TMacOS">The MacOS implementation.</typeparam>
        /// <typeparam name="TNotSupported">The not-supported implementation.</typeparam>
        /// <returns>The loaded runtime API.</returns>
        internal static T LoadRuntimeAPI<
            T,
            TWindows,
            TLinux,
            TMacOS,
            TNotSupported>()
            where T : RuntimeAPI
            where TWindows : T, new()
            where TLinux : T, new()
            where TMacOS : T, new()
            where TNotSupported : T, new()
        {
            if (!Backends.Backend.RunningOnNativePlatform)
                return new TNotSupported();
            try
            {
                T instance =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? new TLinux()
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? new TMacOS() as T
                    : new TWindows();
                // Try to initialize the new interface
                if (!instance.Init())
                    instance = new TNotSupported();
                return instance;
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException)
            {
                // In case of a critical initialization exception fall back to the
                // not supported API
                return new TNotSupported();
            }
        }

        /// <summary>
        /// Returns true if the runtime API instance is supported on this platform.
        /// </summary>
        public abstract bool IsSupported { get; }

        /// <summary>
        /// Initializes the runtime API implementation.
        /// </summary>
        /// <returns>
        /// True, if the API instance could be initialized successfully.
        /// </returns>
        public abstract bool Init();
    }

    /// <summary>
    /// Marks dynamic DLL-import functions that are compatible with the
    /// <see cref="RuntimeSystem.CreateDllWrapper{T}(string, string, string, string)"/>
    /// function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DynamicImportAttribute : Attribute
    {
        #region Static

        /// <summary>
        /// Represents the managed attribute <see cref="DllImportAttribute"/>.
        /// </summary>
        private static readonly Type DllImportType = typeof(DllImportAttribute);

        /// <summary>
        /// The default constructor of the class <see cref="DllImportAttribute"/>.
        /// </summary>
        private static readonly ConstructorInfo DllImportConstructor =
            DllImportType.GetConstructor(new Type[] { typeof(string) });

        /// <summary>
        /// All supported fields of the class <see cref="DllImportAttribute"/>.
        /// </summary>
        private static readonly FieldInfo[] DllImportFields =
        {
            DllImportType.GetField(nameof(DllImportAttribute.EntryPoint)),
            DllImportType.GetField(nameof(DllImportAttribute.CharSet)),
            DllImportType.GetField(nameof(DllImportAttribute.CallingConvention)),
            DllImportType.GetField(nameof(DllImportAttribute.BestFitMapping)),
            DllImportType.GetField(nameof(DllImportAttribute.ThrowOnUnmappableChar))
        };

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new dynamic import attribute.
        /// </summary>
        public DynamicImportAttribute()
            : this(null)
        { }

        /// <summary>
        /// Constructs a new dynamic import attribute.
        /// </summary>
        /// <param name="entryPoint">The entry point.</param>
        public DynamicImportAttribute(string entryPoint)
        {
            EntryPoint = entryPoint;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated native entry point.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Defines the associated character set to use.
        /// </summary>
        public CharSet CharSet { get; set; } = CharSet.Auto;

        /// <summary>
        /// Defines the calling convention.
        /// </summary>
        public CallingConvention CallingConvention { get; set; } =
            CallingConvention.Winapi;

        /// <summary>
        /// Enables or disabled best-fit mapping when mapping ANSI characters.
        /// </summary>
        public bool BestFitMapping { get; set; } = false;

        /// <summary>
        /// If true, it throws an exception in the case of an unmappable character.
        /// </summary>
        public bool ThrowOnUnmappableChar { get; set; } = false;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the name of the native entry point to use.
        /// </summary>
        /// <param name="method">The associated method.</param>
        /// <returns>The resolved native entry-point name.</returns>
        public string GetEntryPoint(MethodInfo method) =>
            EntryPoint ?? method.Name;

        /// <summary>
        /// Converts this attribute instance into a custom attribute builder assembling
        /// an instance of type <see cref="DllImportAttribute"/>.
        /// </summary>
        /// <param name="libraryName">The library name.</param>
        /// <param name="method">The associated method.</param>
        /// <returns>The created attribute builder.</returns>
        public CustomAttributeBuilder ToImportAttributeBuilder(
            string libraryName,
            MethodInfo method) =>
            new CustomAttributeBuilder(
                DllImportConstructor,
                new object[] { libraryName },
                DllImportFields,
                new object[]
                {
                    GetEntryPoint(method),
                    CharSet,
                    CallingConvention,
                    BestFitMapping,
                    ThrowOnUnmappableChar
                });

        #endregion
    }
}
