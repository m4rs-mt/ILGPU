// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: InvocationContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using ILGPUC.Util;
using System;
using System.Reflection;
using System.Text;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.Frontend;

/// <summary>
/// Represents an invocation context for compiler-known methods
/// that are supported in the scope of ILGPU programs.
/// </summary>
unsafe ref struct InvocationContext
{
    #region Instance

    /// <summary>
    /// The internal arguments pointer.
    /// </summary>
    private readonly ref ValueList _arguments;

    /// <summary>
    /// Internal argument index counter.
    /// </summary>
    private int _argumentIndex;

    /// <summary>
    /// Constructs a new invocation context.
    /// </summary>
    /// <param name="codeGenerator">The associated code generator.</param>
    /// <param name="location">The current location.</param>
    /// <param name="block">The current block.</param>
    /// <param name="callerMethod">The caller.</param>
    /// <param name="method">The called method.</param>
    /// <param name="arguments">The method arguments.</param>
    internal InvocationContext(
        CodeGenerator codeGenerator,
        Location location,
        Block block,
        MethodBase callerMethod,
        MethodBase method,
        ref ValueList arguments)
    {
        _arguments = ref arguments;

        CodeGenerator = codeGenerator;
        Location = location;
        Block = block;
        CallerMethod = callerMethod;
        Method = method;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated code generator.
    /// </summary>
    public CodeGenerator CodeGenerator { get; }

    /// <summary>
    /// Returns compilation properties.
    /// </summary>
    public readonly CompilationProperties Properties => CodeGenerator.Properties;

    /// <summary>
    /// Returns the current location.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Return the current basic block.
    /// </summary>
    public Block Block { get; }

    /// <summary>
    /// Returns the current IR context.
    /// </summary>
    public readonly IRContext Context => CodeGenerator.Context;

    /// <summary>
    /// Returns the current type context.
    /// </summary>
    public readonly IRTypeContext TypeContext => CodeGenerator.TypeContext;

    /// <summary>
    /// Returns the current IR builder.
    /// </summary>
    public readonly IRBuilder Builder => Block.Builder;

    /// <summary>
    /// Represents the caller method.
    /// </summary>
    public MethodBase CallerMethod { get; }

    /// <summary>
    /// Represents the targeted method.
    /// </summary>
    public MethodBase Method { get; set; }

    /// <summary>
    /// Returns the associated module.
    /// </summary>
    public readonly Module Module => Method.Module;

    /// <summary>
    /// Returns the call arguments.
    /// </summary>
    public readonly ref ValueList Arguments => ref _arguments;

    /// <summary>
    /// Returns the number of arguments.
    /// </summary>
    public readonly int NumArguments => Arguments.Count;

    /// <summary>
    /// Returns the argument with the given index.
    /// </summary>
    /// <param name="index">The argument index.</param>
    /// <returns>The argument with the given index.</returns>
    public readonly ref ValueReference this[int index] => ref Arguments[index];

    #endregion

    #region Methods

    /// <summary>
    /// Formats an error message to include specific exception information.
    /// </summary>
    public readonly string FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);

    /// <summary>
    /// Returns the generic arguments of the used method.
    /// </summary>
    /// <returns>The generic arguments of the used method.</returns>
    public readonly Type[] GetMethodGenericArguments() => Method.GetGenericArguments();

    /// <summary>
    /// Returns the generic arguments of the used method.
    /// </summary>
    /// <returns>The generic arguments of the used method.</returns>
    public readonly Type[] GetTypeGenericArguments() =>
        Method.DeclaringType.AsNotNull().GetGenericArguments();

    /// <summary>
    /// Pulls an argument from this context.
    /// </summary>
    /// <returns>The pulled argument.</returns>
    public Value Pull() => this[_argumentIndex++];

    /// <summary>
    /// Pulls an argument from this context as a loaded instance.
    /// </summary>
    /// <returns>The pulled argument instance.</returns>
    public Value PullInstance()
    {
        var reference = Pull();
        return Builder.CreateLoad(Location, reference);
    }

    /// <summary>
    /// Returns true if the current method has an unsigned return type.
    /// </summary>
    public readonly bool HasUnsignedReturnType =>
        Method.GetReturnType().IsUnsignedBasicType();

    /// <summary>
    /// Returns true if one of the arguments in an unsigned basic type.
    /// </summary>
    public readonly bool HasUnsignedArguments
    {
        get
        {
            var parameters = Method.GetParameters();
            foreach (var parameter in parameters)
            {
                if (parameter.ParameterType.IsUnsignedBasicType())
                    return true;
            }
            return false;
        }
    }

    #endregion

    #region Object

    /// <summary>
    /// Returns the string representation of this invocation context.
    /// </summary>
    /// <returns>The string representation of this invocation context.</returns>
    public override readonly string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Method.Name);
        builder.Append('(');
        if (Arguments.Count > 0)
        {
            for (int i = 0, e = Arguments.Count; i < e; ++i)
            {
                builder.Append(Arguments[i].ToString());
                if (i + 1 < e)
                    builder.Append(", ");
            }
        }
        builder.Append(')');
        return builder.ToString();
    }

    #endregion
}
