// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeSystem.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
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
            internal DynamicMethod Method { get; }

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

        #endregion

        #region Static

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
        [MemberNotNull(nameof(assemblyBuilder))]
        [MemberNotNull(nameof(moduleBuilder))]
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
            moduleBuilder =
                assemblyBuilder.DefineDynamicModule(assemblyName.Name.AsNotNull());
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

        #endregion

        #region ICache

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">
        /// Passing <see cref="ClearCacheMode.Everything"/>, causes a reload of the
        /// CLR assembly builder, which is used internally.
        /// </param>
        public void ClearCache(ClearCacheMode mode)
        {
            if (mode == ClearCacheMode.Everything)
                ReloadAssemblyBuilder();
        }

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
