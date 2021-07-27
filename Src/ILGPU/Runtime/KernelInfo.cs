// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: KernelInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.IR.Analyses;
using System.Collections.Immutable;
using System.IO;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Provides detailed information about compiled kernels.
    /// </summary>
    public sealed class KernelInfo : CompiledKernel.KernelInfo
    {
        #region Static

        /// <summary>
        /// Creates a new kernel information object.
        /// </summary>
        /// <param name="info">The underlying kernel info (if any).</param>
        /// <param name="minGroupSize">The minimum group size (if known).</param>
        /// <param name="minGridSize">The minimum grid size (if known).</param>
        /// <returns>The created kernel information object.</returns>
        public static KernelInfo CreateFrom(
            CompiledKernel.KernelInfo info,
            int? minGroupSize,
            int? minGridSize) =>
            info is null
                ? new KernelInfo(minGroupSize, minGridSize)
                : new KernelInfo(minGroupSize, minGridSize,
                    info.SharedAllocations, info.Functions);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel information object.
        /// </summary>
        /// <param name="minGroupSize">The minimum group size (if known).</param>
        /// <param name="minGridSize">The minimum grid size (if known).</param>
        public KernelInfo(int? minGroupSize, int? minGridSize)
            : this(
                minGroupSize,
                minGridSize,
                new AllocaKindInformation(
                    ImmutableArray<AllocaInformation>.Empty,
                    0),
                ImmutableArray<CompiledKernel.FunctionInfo>.Empty)
        { }

        /// <summary>
        /// Constructs a new kernel information object.
        /// </summary>
        /// <param name="minGroupSize">The minimum group size (if known).</param>
        /// <param name="minGridSize">The minimum grid size (if known).</param>
        /// <param name="sharedAllocations">All shared allocations.</param>
        /// <param name="functions">
        /// An array containing detailed function information.
        /// </param>
        public KernelInfo(
            int? minGroupSize,
            int? minGridSize,
            in AllocaKindInformation sharedAllocations,
            ImmutableArray<CompiledKernel.FunctionInfo> functions)
            : base(sharedAllocations, functions)
        {
            MinGroupSize = minGroupSize;
            MinGridSize = minGridSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the estimated group size to gain maximum occupancy on this device.
        /// </summary>
        public int? MinGroupSize { get; }

        /// <summary>
        /// Returns the minimum grid size to gain maximum occupancy on this device.
        /// </summary>
        public int? MinGridSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Dumps kernel information to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public override void Dump(TextWriter textWriter)
        {
            base.Dump(textWriter);

            // Group and grid dimensions
            if (MinGroupSize.HasValue)
            {
                textWriter.Write(nameof(MinGroupSize));
                textWriter.Write(' ');
                textWriter.WriteLine(MinGroupSize);
            }

            if (MinGridSize.HasValue)
            {
                textWriter.Write(nameof(MinGridSize));
                textWriter.Write(' ');
                textWriter.WriteLine(MinGridSize);
            }
        }

        #endregion
    }
}
