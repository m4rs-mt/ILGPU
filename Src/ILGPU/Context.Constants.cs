// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IRContext.Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU
{
    partial class Context
    {
        /// <summary>
        /// The name of the dynamic runtime assembly.
        /// </summary>
        public const string RuntimeAssemblyName = RuntimeSystem.AssemblyName;

        /// <summary>
        /// Represents the general ILGPU assembly name.
        /// </summary>
        public const string AssemblyName = "ILGPU";

        /// <summary>
        /// Represents the general ILGPU assembly module name.
        /// </summary>
        public const string FullAssemblyModuleName = AssemblyName + ".dll";

        /// <summary>
        /// The ILGPU assembly file extension.
        /// </summary>
        public const string IRFileExtension = ".gpuil";

        /// <summary>
        /// Represents the default flags of a new context.
        /// </summary>
        public static readonly ContextFlags DefaultFlags = ContextFlags.None;

        /// <summary>
        /// Represents the default debug flags of a new context.
        /// </summary>
        public static readonly ContextFlags DefaultDebug =
            DefaultFlags |
            ContextFlags.EnableDebugSymbols |
            ContextFlags.EnableAssertions;

        /// <summary>
        /// Represents the default flags of a new context.
        /// </summary>
        public static readonly ContextFlags FastMathFlags =
            DefaultFlags | ContextFlags.FastMath;
    }
}
