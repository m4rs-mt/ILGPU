// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GCInit.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Marks the exact block where a managed (GC-allocated) object is initialized.
/// This is the reference-type parallel to <see cref="Alloca"/> (which handles
/// value-type stack slots): <see cref="GCInit"/> represents a class/reference-type
/// allocation backed by a pre-allocated <see cref="Global"/> in Local memory.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GCInit"/> is a first-class IR node that lives inside the basic block
/// where the managed object is created.  It moves naturally with the IR through all
/// restructuring passes (SSA, inlining, block splitting) because it is stored
/// <em>inside</em> the block rather than as an out-of-band annotation on the
/// module-level <see cref="Global"/>.
/// </para>
/// <para>
/// The node is eliminated by <c>LowerGCInit</c>, the first pass in the accelerator
/// specializer pipeline. That pass simultaneously validates that no
/// <see cref="GCInit"/> node is inside a loop (which would imply per-iteration
/// allocations) and replaces every use of the node with its underlying
/// <see cref="Global"/>. Backends never see <see cref="GCInit"/> nodes.
/// </para>
/// </remarks>
sealed partial class GCInit : BasicBlockValue
{
    /// <summary>
    /// Constructs a new GCInit node.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="global">
    /// The module-level Global that holds the per-thread Local memory for this object.
    /// </param>
    internal GCInit(in BasicBlockValueInitializer initializer, Global global)
        : base(initializer, global.Type)
    {
        Seal(global);
    }

    /// <summary>
    /// Returns the underlying <see cref="Global"/> that backs this GC-initialized object.
    /// </summary>
    public Global Global => GetValue<Global>(0);

    /// <inheritdoc cref="IBasicBlockValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGCInit(Location, rewriter.Rewrite(Global) as Global);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "gc_init";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Global.ToReferenceString();
}
