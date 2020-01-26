// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: IfConversion.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using System;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Converts simple ifs to predicates.
    /// </summary>
    public sealed class IfConversion : UnorderedTransformation
    {
        /// <summary>
        /// The default maximum number of instructions per block.
        /// </summary>
        public const int DefaultMaxBlockSize = 2;

        /// <summary>
        /// The default The maximum size difference of the if and the else block.
        /// </summary>
        public const int DefaultMaxSizeDifference = 1;

        /// <summary>
        /// Constructs a new if-conversion transformation.
        /// </summary>
        public IfConversion()
            : this(DefaultMaxBlockSize, DefaultMaxSizeDifference)
        { }

        /// <summary>
        /// Constructs a new if-conversion transformation.
        /// </summary>
        /// <param name="maxBlockSize">The maximum number of instructions per block.</param>
        /// <param name="maxSizeDifference">The maximum size difference of the if and the else block.</param>
        public IfConversion(int maxBlockSize, int maxSizeDifference)
        {
            if (maxBlockSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBlockSize));
            if (maxSizeDifference < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBlockSize));

            MaxBlockSize = maxBlockSize;
            MaxSizeDifference = maxSizeDifference;
        }

        /// <summary>
        /// Resolves the maximum number of instructions per block.
        /// </summary>
        public int MaxBlockSize { get; }

        /// <summary>
        /// Resolves the maximum size difference of the if and the else block.
        /// </summary>
        public int MaxSizeDifference { get; }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();
            var cfg = scope.CreateCFG();
            var ifInfos = IfInfos.Create(cfg);

            bool converted = false;
            foreach (IfInfo ifInfo in ifInfos)
            {
                // Check for an extremely simple if block to convert
                if (!ifInfo.IsSimpleIf())
                    continue;

                // Check size constraints
                int ifBlockSize = ifInfo.IfBlock.Count;
                int elseBlockSize = ifInfo.ElseBlock.Count;
                int blockSizeDiff = IntrinsicMath.Abs(ifBlockSize - elseBlockSize);

                if (ifBlockSize > MaxBlockSize ||
                    elseBlockSize > MaxBlockSize ||
                    blockSizeDiff > DefaultMaxSizeDifference)
                    continue;

                // Check for side effects
                if (ifInfo.HasSideEffects())
                    continue;

                // Convert the current if block
                var variableInfo = ifInfo.ResolveVariableInfo();

                var blockBuilder = builder[ifInfo.EntryBlock];
                blockBuilder.MergeBlock(ifInfo.IfBlock);
                blockBuilder.MergeBlock(ifInfo.ElseBlock);
                blockBuilder.MergeBlock(ifInfo.ExitBlock);

                // Convert all phi values
                var condition = ifInfo.Condition;
                foreach (var variableEntry in variableInfo)
                {
                    var variable = variableEntry.Value;

                    blockBuilder.SetupInsertPosition(variableEntry.Key);
                    var predicate = blockBuilder.CreatePredicate(
                        condition,
                        variable.TrueValue,
                        variable.FalseValue);

                    // Replace the phi node
                    variableEntry.Key.Replace(predicate);
                    blockBuilder.Remove(variableEntry.Key);
                }

                converted = true;
            }

            return converted;
        }
    }
}
