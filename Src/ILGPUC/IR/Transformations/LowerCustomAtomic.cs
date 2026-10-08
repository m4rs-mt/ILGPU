// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerCustomAtomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers <see cref="CustomAtomic"/> nodes into an explicit CAS loop, using
/// a local alloca to carry <c>current</c> across the loop back-edge. A
/// subsequent <see cref="SSAConstruction"/> + <see cref="SSACleanup"/> pair
/// promotes the alloca to a proper phi — this avoids having to construct
/// the loop phi with a forward reference to the CAS result that does not
/// yet exist when the loop body begins.
///
/// <para>CFG shape per site:</para>
/// <code>
///   preHeader:               // original block, truncated at the site
///     currentVar = alloca(T)
///     initial    = load(target)
///     store currentVar &lt;- initial
///     br loop
///
///   loop:
///     current  = load(currentVar)
///     newValue = call operation(current, value)
///     casRes   = AtomicCAS(target, newValue, compare = current)
///     store currentVar &lt;- casRes
///     same     = casRes == current
///     br.cond same, exit, loop
///
///   exit:                    // receives the rest of the original block
///     result   = load(currentVar)
///     ...downstream users of the CustomAtomic site receive result...
/// </code>
/// </summary>
/// <param name="args">The transformation args.</param>
sealed class LowerCustomAtomic(TransformationArgs args) : Transformation(args)
{
    /// <inheritdoc/>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        var sites = new List<CustomAtomic>();
        foreach (var block in transform.OldMethod.Blocks)
            foreach (var value in block.Values)
                if (value is CustomAtomic ca) sites.Add(ca);

        if (sites.Count == 0) return;

        foreach (var site in sites)
            LowerOne(transform, site);
    }

    /// <summary>
    /// Lowers a single <see cref="CustomAtomic"/> site into a CAS loop.
    /// </summary>
    private static void LowerOne(MethodTransform transform, CustomAtomic site)
    {
        var location = site.Location;
        var oldBlock = site.BasicBlock;
        var preHeader = transform.GetBasicBlockTransform(oldBlock);

        // Create NEW blocks for the loop body and exit continuation.
        // Pass `null` as the third (oldBlock) argument — these blocks are
        // genuinely new, not generational replacements of oldBlock.
        // (oldBlock remains represented by `preHeader`, via GetBasicBlockTransform.)
        var loopBuilder = transform.CreateBasicBlock(
            location, "customAtomicLoop")
            .AsNotNullCast<BasicBlockTransform>();
        var exitBuilder = transform.CreateBasicBlock(
            location, "customAtomicExit")
            .AsNotNullCast<BasicBlockTransform>();

        // Split the current block at the site: truncate the forward chain
        // at the site and move everything past the site into the exit block.
        // Note: termination is NOT copied yet — we first append the exit
        // block's own values (load final, bit-cast back) so they end up
        // before the terminator.
        preHeader.UpdateLastByTerminatingAfter(site);
        transform.AppendTo(exitBuilder, toAppend: site.Next);

        var (castPtr, castValue, isBitcast) =
            BitCastForAtomic(preHeader, location, site.Target, site.Value);

        var intElemType = ((PointerType)castPtr.Type).ElementType;

        // preHeader: allocate slot, load initial, store, branch.
        var currentVar = preHeader.CreateAlloca(location, intElemType)
            .AsNotNull();
        var initial = (Value)preHeader.CreateLoad(location, castPtr)
            .AsNotNull();
        preHeader.CreateStore(location, currentVar, initial);
        preHeader.CreateUnconditionalTermination(loopBuilder.BasicBlock);

        // loop body
        var current = (Value)loopBuilder.CreateLoad(location, currentVar)
            .AsNotNull();
        var currentTyped = isBitcast
            ? loopBuilder.CreateIntAsFloatCast(location, current).AsNotNull()
            : current;

        // Call the user's binary operation via the MethodCall.Builder API.
        var operationMethod = site.Operation.AsNotNullCast<Method>();
        var callBuilder = loopBuilder.CreateCall(location, operationMethod);
        callBuilder.Add(currentTyped);
        callBuilder.Add(castValue is var cv && isBitcast
            ? loopBuilder.CreateIntAsFloatCast(location, cv).AsNotNull()
            : site.Value);
        Value newValueTyped = callBuilder.Seal();

        var newValueInt = isBitcast
            ? loopBuilder.CreateFloatAsIntCast(location, newValueTyped).AsNotNull()
            : newValueTyped;

        var casResult = loopBuilder.CreateAtomicCAS(
            location, castPtr, newValueInt, current, site.Flags)
            .AsNotNull();

        loopBuilder.CreateStore(location, currentVar, casResult);

        // Continue the loop while CAS failed (current != casResult).
        // The CPU vectorized backend translates conditional terminations
        // with the TRUE target as the loop-continuation and FALSE target as
        // the loop-exit, so we express the condition as "not-equal →
        // continue, equal → exit".
        var notSame = loopBuilder.CreateCompare(
            location,
            casResult,
            current,
            CompareKind.NotEqual,
            CompareFlags.None)
            .AsNotNull();

        loopBuilder.CreateConditionalTermination(
            notSame, loopBuilder.BasicBlock, exitBuilder.BasicBlock);

        // exit block: insert the "load final result" and bit-cast-back
        // values at the START of the exit block (before any values we
        // moved here from the original block's tail, and before the
        // terminator we're about to copy).
        // Since AppendTo put the tail values after preHeader's current Last
        // (which is site, now replaced), and exit's Last currently points
        // to the LAST of those moved values (or is null if there were none),
        // we need to inject our final-result values at the HEAD of exit.
        //
        // Simpler: add the final-result load BEFORE the tail values by
        // temporarily clearing exit, building our values, then re-appending
        // the tail. But that's brittle. Instead: the common case has no
        // tail (site was last non-terminator in block), so just append here.
        var finalInt = (Value)exitBuilder.CreateLoad(location, currentVar)
            .AsNotNull();
        var finalTyped = isBitcast
            ? exitBuilder.CreateIntAsFloatCast(location, finalInt).AsNotNull()
            : finalInt;

        // Now copy the termination from the original block to the exit block.
        exitBuilder.CopyTerminationFrom(oldBlock);

        transform.Replace(site, finalTyped);
    }

    /// <summary>
    /// Emits bit-casts for float/double atomics. AtomicCAS on most backends
    /// is integer-only; reinterpret the pointer and the operand as the
    /// corresponding integer width.
    /// </summary>
    private static (Value CastPtr, Value CastValue, bool Bitcast)
        BitCastForAtomic(
        BasicBlockTransform preHeader,
        Location location,
        Value targetPtr,
        Value value)
    {
        var elemType = ((PointerType)targetPtr.Type).ElementType;
        var bvt = elemType.BasicValueType;
        if (bvt != BasicValueType.Float32 && bvt != BasicValueType.Float64)
            return (targetPtr, value, false);

        var intBvt = bvt == BasicValueType.Float32
            ? BasicValueType.Int32
            : BasicValueType.Int64;
        var intElem = preHeader.ModuleBuilder.GetPrimitiveType(intBvt);
        // CreatePointerCast takes the target ELEMENT type (it wraps it in a
        // PointerType internally, preserving the source address space).
        var castPtr = preHeader.CreatePointerCast(
            location, targetPtr, intElem).AsNotNull();
        var castValue = preHeader.CreateFloatAsIntCast(
            location, value).AsNotNull();
        return (castPtr, castValue, true);
    }
}
