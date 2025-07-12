// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sequencer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Initialization;

/// <summary>
/// Represents sequence initialization helpers for views.
/// </summary>
public static class Sequencer
{
    /// <summary>
    /// Computes a sequence of values. The output depends on settings for
    /// <paramref name="sequenceLength"/> and <paramref name="sequenceBatchLength"/>
    /// see below:
    /// 1) If <paramref name="sequenceBatchLength"/> is specified, this function computes
    /// a new sequence of batched values of length sequenceBatchLength, and writes the
    /// computed values to the given view. Afterwards, the target view will contain the
    /// following values:
    /// - [0, sequenceBatchLength - 1] = 0,,
    /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
    /// - ...
    /// 2) If <paramref name="sequenceBatchLength"/> and <paramref name="sequenceLength"/>
    /// are specified, this function computes a new repeated sequence (of length
    /// <paramref name="sequenceLength"/>) of batched values (of length
    /// <paramref name="sequenceBatchLength"/>, and writes the computed values to the
    /// given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] =
    ///       - [0, sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - [sequenceLength, sequenceLength * 2 - 1]
    ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceLength + sequenceBatchLength,
    ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="sequencer">The basic sequencer to use.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Sequence<T>(
        this AcceleratorStream stream,
        ArrayView<T> target,
        Func<long, T> sequencer,
        long sequenceLength = long.MaxValue,
        long sequenceBatchLength = 1)
        where T : unmanaged
    {
        if (sequenceBatchLength < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceBatchLength));
        if (sequenceLength < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceLength));

        stream.SequenceInternal(
            target.AsDense(),
            sequencer,
            sequenceLength,
            sequenceBatchLength);
    }

    /// <summary>
    /// Computes a sequence of values. The output depends on settings for
    /// <paramref name="sequenceLength"/> and <paramref name="sequenceBatchLength"/>
    /// see below:
    /// 1) If <paramref name="sequenceBatchLength"/> is specified, this function computes
    /// a new sequence of batched values of length sequenceBatchLength, and writes the
    /// computed values to the given view. Afterwards, the target view will contain the
    /// following values:
    /// - [0, sequenceBatchLength - 1] = 0,,
    /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
    /// - ...
    /// 2) If <paramref name="sequenceBatchLength"/> and <paramref name="sequenceLength"/>
    /// are specified, this function computes a new repeated sequence (of length
    /// <paramref name="sequenceLength"/>) of batched values (of length
    /// <paramref name="sequenceBatchLength"/>, and writes the computed values to the
    /// given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] =
    ///       - [0, sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - [sequenceLength, sequenceLength * 2 - 1]
    ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceLength + sequenceBatchLength,
    ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="sequencer">The basic sequencer to use.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void Sequence<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> target,
        Func<long, T> sequencer,
        long sequenceLength = long.MaxValue,
        long sequenceBatchLength = 1)
        where T : unmanaged
        where TStride : struct, IStride1D
    {
        if (sequenceBatchLength < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceBatchLength));
        if (sequenceLength < 1)
            throw new ArgumentOutOfRangeException(nameof(sequenceLength));

        stream.SequenceInternal(target, sequencer, sequenceLength, sequenceBatchLength);
    }

    /// <summary>
    /// Computes a sequence of values. The output depends on settings for
    /// <paramref name="sequenceLength"/> and <paramref name="sequenceBatchLength"/>
    /// see below:
    /// 1) If <paramref name="sequenceBatchLength"/> is specified, this function computes
    /// a new sequence of batched values of length sequenceBatchLength, and writes the
    /// computed values to the given view. Afterwards, the target view will contain the
    /// following values:
    /// - [0, sequenceBatchLength - 1] = 0,,
    /// - [sequenceBatchLength, sequenceBatchLength * 2 -1] = 1,
    /// - ...
    /// 2) If <paramref name="sequenceBatchLength"/> and <paramref name="sequenceLength"/>
    /// are specified, this function computes a new repeated sequence (of length
    /// <paramref name="sequenceLength"/>) of batched values (of length
    /// <paramref name="sequenceBatchLength"/>, and writes the computed values to the
    /// given view. Afterwards, the target view will contain the following values:
    /// - [0, sequenceLength - 1] =
    ///       - [0, sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceBatchLength, sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - [sequenceLength, sequenceLength * 2 - 1]
    ///       - [sequenceLength, sequenceLength + sequenceBatchLength - 1] = sequencer(0),
    ///       - [sequenceLength + sequenceBatchLength,
    ///          sequenceLength + sequenceBatchLength * 2 - 1] = sequencer(1),
    ///       - ...
    /// - ...
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="target">The target view to initialize.</param>
    /// <param name="sequencer">The basic sequencer to use.</param>
    /// <param name="sequenceBatchLength">The length of a single batch.</param>
    /// <param name="sequenceLength">The length of a single sequence.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    internal static void SequenceInternal<T, TStride>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> target,
        Func<long, T> sequencer,
        long sequenceLength = long.MaxValue,
        long sequenceBatchLength = 1)
        where T : unmanaged
        where TStride : struct, IStride1D =>
        stream.Launch(target.Extent, index =>
        {
            long sequenceIndex = index / sequenceBatchLength % sequenceLength;
            target[index] = sequencer(sequenceIndex);
        });
}
