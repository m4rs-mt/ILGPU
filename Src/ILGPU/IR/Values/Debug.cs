// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a debug assert operation.
    /// </summary>
    [ValueKind(ValueKind.DebugAssert)]
    public sealed class DebugAssertOperation : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The assert condition.</param>
        /// <param name="message">The debug message.</param>
        internal DebugAssertOperation(
            in ValueInitializer initializer,
            ValueReference condition,
            ValueReference message)
            : base(initializer)
        {
            Seal(condition, message);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.DebugAssert;

        /// <summary>
        /// The debug condition.
        /// </summary>
        public ValueReference Condition => this[0];

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[1];

        #endregion

        #region Methods

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

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateDebugAssert(
                Location,
                rebuilder.Rebuild(Condition),
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "debug.assert";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Condition}, {Message}";

        #endregion
    }
}
