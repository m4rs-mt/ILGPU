// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents a debug assert operation.
/// </summary>
sealed partial class DebugAssertOperation : MemoryValue
{
    /// <summary>
    /// Constructs a new debug operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="condition">The assert condition.</param>
    /// <param name="message">The debug message.</param>
    public DebugAssertOperation(
        in BasicBlockValueInitializer initializer,
        Value condition,
        Value message)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
        Seal(condition, message);
    }

    /// <summary>
    /// The debug condition.
    /// </summary>
    public Value Condition => GetValue<Value>(0);

    /// <summary>
    /// Returns the message.
    /// </summary>
    public Value Message => GetValue<Value>(1);

    /// <summary>
    /// Determines the current location information.
    /// </summary>
    /// <returns>The location information.</returns>
    public (string FileName, int Line, string Method) GetLocationInfo()
    {
        const string KernelName = "Kernel";

        // Return information based on the current file location
        static (string FileName, int Line, string Method) MakeLocation(
            FileLocation fileLocation) => (
                string.IsNullOrWhiteSpace(fileLocation.FileName)
                ? KernelName
                : fileLocation.FileName,
                fileLocation.StartLine,
                string.Empty);

        if (Location.IsKnown && Location is FileLocation fileLocation)
        {
            return MakeLocation(fileLocation);
        }
        else if (Location.IsKnown &&
            Location is CompilationStackLocation compilationStackLocation &&
            compilationStackLocation.TryGetLocation(
                out FileLocation? innerFileLocation))
        {
            return MakeLocation(innerFileLocation);
        }
        else
        {
            // Return dummy location information
            return (KernelName, 0, KernelName);
        }
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateDebugAssert(
            Location,
            rewriter.Rewrite(Condition),
            rewriter.Rewrite(Message));

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "debug.assert";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Condition}, {Message}";
}
