// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.CompilationPropertiesOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System.CommandLine;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Registers compilation properties command-line switches.
    /// </summary>
    sealed class CompilationPropertiesOptions
    {
        /// <summary>Debug symbols mode to embed in generated code.</summary>
        readonly Option<DebugSymbolsMode> _debugSymbolsOption = new("--debug-symbols")
        {
            Description = "Debug symbols mode to use",
            DefaultValueFactory = static _ => DebugSymbolsMode.None
        };

        /// <summary>Optimization level applied during kernel compilation.</summary>
        readonly Option<OptimizationLevel> _optLevelOption = new(
            "-opt",
            "--optimization-level")
        {
            Description = "Optimization level to use (default O1)",
            DefaultValueFactory = static _ => OptimizationLevel.O1
        };

        /// <summary>Enables or disables runtime assertions in the generated kernel.</summary>
        readonly Option<bool> _assertionsOption = new("-assert", "--assertions")
        {
            Description = "Enables or disables assertions",
            DefaultValueFactory = static _ => false
        };

        /// <summary>Enables or disables I/O operations (e.g., printf) in the generated kernel.</summary>
        readonly Option<bool> _ioOperationsOption = new("-io", "--io-operations")
        {
            Description = "Enables or disables assertions",
            DefaultValueFactory = static _ => false
        };

        /// <summary>Controls function-call inlining aggressiveness during optimization.</summary>
        readonly Option<InliningMode> _inliningModeOption = new("--inline")
        {
            Description = "Specifies inlining mode to use",
            DefaultValueFactory = static _ => InliningMode.Aggressive
        };

        /// <summary>Selects the math-precision mode for floating-point operations.</summary>
        readonly Option<MathMode> _mathModeOption = new("--math")
        {
            Description = "Specifies math operations mode to use",
            DefaultValueFactory = static _ => MathMode.Default
        };

        /// <summary>Forces floating-point math to flush denormal values to zero.</summary>
        readonly Option<bool> _mathFlushToZeroOption = new("--ftz")
        {
            Description = "Forces math operations to use flush-to-zero for performance",
            DefaultValueFactory = static _ => false
        };

        /// <summary>Governs how static fields in kernel code are handled.</summary>
        readonly Option<StaticFieldMode> _staticFieldModeOption = new("--static-fields")
        {
            Description = "Specifies static field mode to use",
            DefaultValueFactory = static _ => StaticFieldMode.Default
        };

        /// <summary>Governs how managed arrays in kernel code are handled.</summary>
        readonly Option<ArrayMode> _arrayModeOption = new("--arrays")
        {
            Description = "Specifies array mode to use",
            DefaultValueFactory = static _ => ArrayMode.Default
        };

        /// <summary>
        /// Convenience flag that sets all debug-related options
        /// (symbols, assertions, I/O, O0).
        /// </summary>
        readonly Option<bool> _debugOption = new("-d", "--debug")
        {
            Description = "Enables all debug-related settings (like assertions and IO)",
            DefaultValueFactory = static _ => false
        };

        /// <summary>
        /// Convenience flag that sets all release-related options
        /// (no symbols, no assertions, O2).
        /// </summary>
        readonly Option<bool> _releaseOption = new("-r", "--release")
        {
            Description = "Enables all release-related settings",
            DefaultValueFactory = static _ => false
        };

        /// <summary>
        /// Registers all options to control compilation properties with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public CompilationPropertiesOptions(Command command)
        {
            command.Add(_debugSymbolsOption);
            command.Add(_optLevelOption);
            command.Add(_assertionsOption);
            command.Add(_ioOperationsOption);

            command.Add(_inliningModeOption);
            command.Add(_mathModeOption);
            command.Add(_mathFlushToZeroOption);
            command.Add(_staticFieldModeOption);
            command.Add(_arrayModeOption);

            command.Add(_debugOption);
            command.Add(_releaseOption);
        }

        /// <summary>
        /// Gets compilation properties from the given parse result.
        /// </summary>
        /// <param name="result">The parse result as input.</param>
        /// <returns>The determined compilation properties.</returns>
        public CompilationProperties GetProperties(ParseResult result)
        {
            var properties = new CompilationProperties
            {
                DebugSymbolsMode = result.GetValue(_debugSymbolsOption),
                OptimizationLevel = result.GetValue(_optLevelOption),
                EnableAssertions = result.GetValue(_assertionsOption),
                EnableIOOperations = result.GetValue(_ioOperationsOption),

                InliningMode = result.GetValue(_inliningModeOption),
                MathMode = result.GetValue(_mathModeOption),
                EnableMathFlushToZero = result.GetValue(_mathFlushToZeroOption),
                StaticFieldMode = result.GetValue(_staticFieldModeOption),
                ArrayMode = result.GetValue(_arrayModeOption),
            };

            if (result.GetValue(_debugOption))
            {
                properties = properties with
                {
                    DebugSymbolsMode = DebugSymbolsMode.Default,
                    OptimizationLevel = OptimizationLevel.O0,
                    EnableAssertions = true,
                    EnableIOOperations = true,
                };
            }
            if (result.GetValue(_releaseOption))
            {
                properties = properties with
                {
                    DebugSymbolsMode = DebugSymbolsMode.None,
                    OptimizationLevel = OptimizationLevel.O2,
                    EnableAssertions = false,
                    EnableIOOperations = false,
                };
            }

            return properties;
        }
    }
}
