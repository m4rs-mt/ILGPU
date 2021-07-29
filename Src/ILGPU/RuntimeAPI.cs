// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RuntimeAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace ILGPU
{
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
        public bool BestFitMapping { get; set; }

        /// <summary>
        /// If true, it throws an exception in the case of an unmappable character.
        /// </summary>
        public bool ThrowOnUnmappableChar { get; set; }

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
                    GetEntryPoint(method), CharSet, CallingConvention, BestFitMapping,
                    ThrowOnUnmappableChar
                });

        #endregion
    }
}
