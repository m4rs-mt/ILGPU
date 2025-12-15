// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenerationContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ILGPUC.Backends;

/// <summary>
/// Represents the context for code generation, maintaining state and helpers
/// during the source code emission process.
/// </summary>
/// <param name="module">The module being compiled.</param>
/// <param name="languageConfig">The language configuration.</param>
/// <param name="writer">The text writer for output.</param>
sealed partial class GenerationContext(
    Module module,
    LanguageConfiguration languageConfig,
    TextWriter writer)
{
    private readonly Stack<int> _indentStack = new();
    private int _currentIndent;
    private readonly GlobalValueMap<string> _valueNames =
        module.CreateGlobalMap<string>();
    private int _nextTempId;
    private readonly GlobalValueSet _variableAllocations = module.CreateGlobalSet();

    /// <summary>
    /// Returns the module being compiled.
    /// </summary>
    public Module Module { get; } = module;

    /// <summary>
    /// Returns the language-specific configuration.
    /// </summary>
    public LanguageConfiguration LanguageConfig { get; } = languageConfig;

    /// <summary>
    /// Gets the intrinsic emitter for generating language-specific intrinsic calls.
    /// </summary>
    public IntrinsicEmitter IntrinsicEmitter { get; } =
        languageConfig.CreateIntrinsicEmitter();

    #region Writing

    /// <summary>
    /// Writes a line of text with the current indentation.
    /// </summary>
    public void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            WriteIndent();
            writer.WriteLine(text);
        }
        else
        {
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Writes text without a newline.
    /// </summary>
    public void Write(string text) => writer.Write(text);

    /// <summary>
    /// Writes the current indentation.
    /// </summary>
    private void WriteIndent()
    {
        for (int i = 0; i < _currentIndent; i++)
            writer.Write("  ");
    }

    /// <summary>
    /// Increases the indentation level.
    /// </summary>
    public void PushIndent()
    {
        _indentStack.Push(_currentIndent);
        _currentIndent++;
    }

    /// <summary>
    /// Decreases the indentation level.
    /// </summary>
    public void PopIndent()
    {
        if (_indentStack.Count > 0)
            _currentIndent = _indentStack.Pop();
        else
            _currentIndent = Math.Max(0, _currentIndent - 1);
    }

    #endregion

    #region Naming

    /// <summary>
    /// Gets or creates a name for a value.
    /// </summary>
    public string GetValueName(Value value)
    {
        if (_valueNames.TryGetValue(value, out var name))
            return name.AsNotNull();

        // Generate a new name
        name = GenerateValueName(value);
        _valueNames[value] = name;
        return name;
    }

    /// <summary>
    /// Assigns a specific name to a value.
    /// </summary>
    public void SetValueName(Value value, string name) =>
        _valueNames[value] = name;

    /// <summary>
    /// Generates a name for a value based on its type.
    /// </summary>
    public string GenerateValueName(Value value) =>
        value switch
        {
            Method method => GetMethodName(method),
            Global global => $"global_{global.Id}",
            Parameter param => GetIdentifier($"param_{param.Index}_{param.Name}"),
            BasicBlock block => $"block_{block.Id}",
            _ => $"tmp_{_nextTempId++}"
        };

    /// <summary>
    /// Generates a name for a value based on its type.
    /// </summary>
    public static string GetMethodName(Method method) =>
        GetIdentifier($"method_{method.Name}_{method.Id}");

    /// <summary>
    /// Generates a class name from a method (without the "method_" prefix).
    /// </summary>
    public static string GetClassName(Method method) =>
        GetIdentifier($"{method.Name}_{method.Id}");

    /// <summary>
    /// Sanitizes an identifier to be valid in the target language.
    /// </summary>
    private static string GetIdentifier(string identifier)
    {
        // Remove invalid characters and replace with underscores
        var result = SanitizeIdentifierRegex().Replace(identifier, "_");

        // Ensure it doesn't start with a digit
        if (char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }

    /// <summary>
    /// Generates a unique temporary variable name.
    /// </summary>
    public string GenerateTempName() => $"tmp_{_nextTempId++}";

    #endregion

    #region Variable Tracking

    /// <summary>
    /// Marks a value as needing a variable allocation.
    /// </summary>
    public void MarkNeedsVariable(Value value) =>
        _variableAllocations.Add(value);

    /// <summary>
    /// Checks if a value has been allocated a variable.
    /// </summary>
    public bool NeedsVariable(Value value) =>
        _variableAllocations.Contains(value);

    #endregion

    #region Scopes

    /// <summary>
    /// Opens a new scope with curly braces.
    /// </summary>
    public void OpenScope()
    {
        WriteLine("{");
        PushIndent();
    }

    /// <summary>
    /// Closes the current scope.
    /// </summary>
    public void CloseScope()
    {
        PopIndent();
        WriteLine("}");
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex SanitizeIdentifierRegex();

    #endregion
}
