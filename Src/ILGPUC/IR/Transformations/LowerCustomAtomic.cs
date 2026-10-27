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

using ILGPU.Runtime;
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
///     ...downstream users of the CustomAtomic site receive casRes...
/// </code>
/// <para>
/// <see cref="GenericAtomic"/> operations that the target has no native
/// instruction for (e.g. float min/max) are lowered the same way, computing
/// the new value inline instead of calling an operation method.
/// </para>
/// </summary>
/// <param name="args">The transformation args.</param>
/// <param name="specification">The target architecture specification.</param>
sealed class LowerCustomAtomic(
    TransformationArgs args,
    ArchitectureSpecification specification) : Transformation(args)
{
    /// <summary>
    /// Safety cap for the number of lowering rounds.
    /// </summary>
    private const int MaxIterations = 256;

    /// <summary>
    /// Lowers one atomic site per block and round until no site remains:
    /// splitting a block at a site moves the remaining values of the block
    /// into a new exit block, which is lowered in the next round.
    /// </summary>
    protected override Module TransformInternal(Module module)
    {
        var current = module;
        for (int i = 0; i < MaxIterations && HasSites(current); ++i)
        {
            current = Transformer.Create(
                a => new LowerAtomicSites(a, specification))
                .Apply(Properties, TypeInformationManager, current);
        }
        return current;
    }

    /// <summary>
    /// Returns true if the module contains an atomic site to lower.
    /// </summary>
    private bool HasSites(Module module)
    {
        foreach (var method in module.Methods)
        {
            foreach (var block in method.Blocks)
            {
                foreach (var value in block.Values)
                {
                    if (IsSite(value, specification))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the given value is an atomic that must be lowered to a
    /// CAS loop on the given target.
    /// </summary>
    internal static bool IsSite(Value value, ArchitectureSpecification specification) =>
        value is CustomAtomic ||
        value is GenericAtomic genericAtomic &&
        !IsNativeOrUnlowerable(
            specification.AcceleratorType,
            genericAtomic.Kind,
            genericAtomic.ArithmeticBasicValueType);

    #region Static

    /// <summary>
    /// Returns true if the given accelerator implements the generic atomic
    /// natively, or if the operation cannot be lowered to a CAS loop on it
    /// (the backend then reports a meaningful error).
    /// </summary>
    internal static bool IsNativeOrUnlowerable(
        AcceleratorType acceleratorType,
        GenericAtomicKind kind,
        ArithmeticBasicValueType type) =>
        acceleratorType switch
        {
            AcceleratorType.CPU => true,
            _ => type switch
            {
                ArithmeticBasicValueType.Float32 => acceleratorType switch
                {
                    AcceleratorType.OpenCL => kind == GenericAtomicKind.Exchange,
                    _ => kind is GenericAtomicKind.Add or GenericAtomicKind.Exchange,
                },
                ArithmeticBasicValueType.Float64 => acceleratorType switch
                {
                    // Metal has no 64-bit compare-and-swap to loop on
                    AcceleratorType.Metal => true,
                    AcceleratorType.OpenCL => false,
                    _ => kind == GenericAtomicKind.Add,
                },
                // All integer atomics are native (or rejected by the backend)
                _ => true,
            },
        };

    #endregion
}

/// <summary>
/// Lowers the first atomic site of every block into a CAS loop (see
/// <see cref="LowerCustomAtomic"/>).
/// </summary>
/// <param name="args">The transformation args.</param>
/// <param name="specification">The target architecture specification.</param>
sealed class LowerAtomicSites(
    TransformationArgs args,
    ArchitectureSpecification specification) : Transformation(args)
{
    /// <inheritdoc/>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        var sites = new List<AtomicValue>();
        foreach (var block in transform.OldMethod.Blocks)
        {
            foreach (var value in block.Values)
            {
                if (LowerCustomAtomic.IsSite(value, specification))
                {
                    sites.Add((AtomicValue)value);
                    break;
                }
            }
        }

        foreach (var site in sites)
            LowerOne(transform, site);
    }

    /// <summary>
    /// Computes the new value of a lowered atomic operation inside the loop.
    /// </summary>
    private static Value ComputeNewValue(
        BasicBlockTransform loopBuilder,
        Location location,
        AtomicValue site,
        Value current,
        Value value)
    {
        if (site is CustomAtomic customAtomic)
        {
            // Call the user's binary operation via the MethodCall.Builder API.
            var operationMethod =
                loopBuilder.Rewrite(customAtomic.Operation) as Method
                ?? customAtomic.Operation.AsNotNullCast<Method>();
            var callBuilder = loopBuilder.CreateCall(location, operationMethod);
            callBuilder.Add(current);
            callBuilder.Add(value);
            return callBuilder.Seal();
        }

        var genericAtomic = site.AsNotNullCast<GenericAtomic>();
        var flags = genericAtomic.IsUnsigned
            ? ArithmeticFlags.Unsigned
            : ArithmeticFlags.None;
        var kind = genericAtomic.Kind switch
        {
            GenericAtomicKind.Exchange => (BinaryArithmeticKind?)null,
            GenericAtomicKind.Add => BinaryArithmeticKind.Add,
            GenericAtomicKind.And => BinaryArithmeticKind.And,
            GenericAtomicKind.Or => BinaryArithmeticKind.Or,
            GenericAtomicKind.Xor => BinaryArithmeticKind.Xor,
            GenericAtomicKind.Max => BinaryArithmeticKind.Max,
            _ => BinaryArithmeticKind.Min,
        };
        return kind.HasValue
            ? loopBuilder.CreateArithmetic(
                location,
                current,
                value,
                kind.Value,
                flags).AsNotNull()
            : value;
    }

    /// <summary>
    /// Lowers a single atomic site into a CAS loop.
    /// </summary>
    private static void LowerOne(MethodTransform transform, AtomicValue site)
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

        // The loop is built by hand, so every operand it embeds must be resolved
        // into the new generation first. BitCastForAtomic returns its inputs
        // unchanged on the integer path, so resolving here covers both paths.
        var newTarget = transform.Rewrite(site.Target).AsNotNull();
        var newValue = transform.Rewrite(site.Value).AsNotNull();

        var (castPtr, castValue, isBitcast) =
            BitCastForAtomic(preHeader, location, newTarget, newValue);

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

        Value newValueTyped = ComputeNewValue(
            loopBuilder,
            location,
            site,
            currentTyped,
            isBitcast
                ? loopBuilder.CreateIntAsFloatCast(location, castValue).AsNotNull()
                : newValue);

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

        // The loop exits once the CAS succeeded, so the CAS result of the
        // final iteration is the previous value at the target. The loop block
        // dominates the exit block, so the tail moved to the exit block can
        // use it directly. (Loading the result in the exit block would place
        // the load after the moved tail values that consume it.)
        var finalTyped = isBitcast
            ? loopBuilder.CreateIntAsFloatCast(location, casResult).AsNotNull()
            : casResult;

        // Now copy the termination from the original block to the exit block,
        // which becomes the predecessor of its successors' phi values.
        transform.CompleteBlockSplit(preHeader, exitBuilder, oldBlock);

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
