// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: BasicBlock.Builder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR
{
    [SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "Handled in Method.Builder.Dispose(bool)")]
    partial class BasicBlock
    {
        /// <summary>
        /// Represents a basic block builder.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Naming",
            "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is the correct name of the current entity")]
        public sealed class Builder :
            IRBuilder,
            IEnumerable<ValueEntry>,
            IDumpable
        {
            #region Instance

            /// <summary>
            /// A local cache of the value list.
            /// </summary>
            private List<ValueReference> values;

            /// <summary>
            /// A collection of values to remove
            /// </summary>
            private readonly HashSet<Value> toRemove = new HashSet<Value>();

            /// <summary>
            /// The current insert position for new instructions.
            /// </summary>
            private int insertPosition;

            /// <summary>
            /// Constructs a new builder.
            /// </summary>
            /// <param name="methodBuilder">The parent method builder.</param>
            /// <param name="block">The parent block.</param>
            internal Builder(Method.Builder methodBuilder, BasicBlock block)
                : base(block)
            {
                MethodBuilder = methodBuilder;

                values = block.values;
                insertPosition = Count;
                block.CompactTerminator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent function builder.
            /// </summary>
            public Method.Builder MethodBuilder { get; }

            /// <summary>
            /// Gets or sets the current terminator.
            /// </summary>
            public TerminatorValue Terminator
            {
                get => BasicBlock.Terminator;
                set
                {
                    if (value != null)
                        value.BasicBlock = BasicBlock;
                    MethodBuilder.ScheduleControlFlowUpdate(Terminator, value);
                    BasicBlock.Terminator = value;
                }
            }

            /// <summary>
            /// Returns the number of attached values.
            /// </summary>
            public int Count => Values.Count;

            /// <summary>
            /// Gets or sets the current value list.
            /// </summary>
            private List<ValueReference> Values
            {
                get => values;
                set => BasicBlock.values = values = value;
            }

            /// <summary>
            /// Gets or sets the current insert position for new instructions.
            /// </summary>
            public int InsertPosition
            {
                get => insertPosition;
                set
                {
                    this.Assert(value >= 0 && value <= Count);
                    insertPosition = value;
                }
            }

            #endregion

            #region Methods

            /// <summary>
            /// Sets the insert position to the index stored in the given value entry.
            /// </summary>
            /// <param name="valueEntry">The value entry.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetupInsertPosition(ValueEntry valueEntry) =>
                InsertPosition = valueEntry.Index + 1;

            /// <summary>
            /// Sets the insert position to the end of the current value list.
            /// </summary>
            public void SetupInsertPositionToEnd() =>
                InsertPosition = Values.Count;

            /// <summary>
            /// Sets the insert position to the index stored in the given value entry.
            /// </summary>
            /// <param name="value">The value entry.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetupInsertPosition(Value value)
            {
                this.Assert(value.BasicBlock == BasicBlock);
                InsertPosition = Values.IndexOf(value);
            }

            /// <summary>
            /// Inserts the given value at the beginning of this block.
            /// </summary>
            /// <param name="value">The value to add.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InsertAtBeginning(Value value)
            {
                this.AssertNotNull(value);
                this.Assert(value.BasicBlock == BasicBlock);

                Values.Insert(0, value);
                ++InsertPosition;
            }

            /// <summary>
            /// Adds the given value to this block.
            /// </summary>
            /// <param name="value">The value to add.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(Value value)
            {
                this.AssertNotNull(value);
                this.Assert(value.BasicBlock == BasicBlock);

                if (insertPosition < Count)
                    Values.Insert(insertPosition, value);
                else
                    Values.Add(value);
                ++insertPosition;
            }

            /// <summary>
            /// Removes all operations of this block (including the terminator).
            /// </summary>
            public void Clear()
            {
                foreach (var reference in Values)
                    toRemove.Add(reference.DirectTarget);
                toRemove.Add(Terminator);
            }

            /// <summary>
            /// Clears all attached values (except the terminator).
            /// </summary>
            private void ClearLists()
            {
                Values.Clear();
                toRemove.Clear();
            }

            /// <summary>
            /// Schedules the given value for removal.
            /// </summary>
            /// <param name="value">The value to remove.</param>
            public void Remove(Value value)
            {
                this.AssertNotNull(value);
                this.Assert(value.BasicBlock == BasicBlock);

                toRemove.Add(value);
            }

            /// <summary>
            /// Applies all scheduled removal operations.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void PerformRemoval()
            {
                if (toRemove.Count < 1)
                    return;
                var newValues = new List<ValueReference>(
                    IntrinsicMath.Max(Count - toRemove.Count, 0));
                PerformRemoval(newValues);
                Values = newValues;
            }

            /// <summary>
            /// Applies all scheduled removal operations by adding them to
            /// the given <paramref name="targetCollection"/>.
            /// </summary>
            /// <param name="targetCollection">
            /// The target collection to which all elements will be appended.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PerformRemoval<TCollection>(TCollection targetCollection)
                where TCollection : ICollection<ValueReference>
            {
                for (int i = 0, e = Count; i < e; ++i)
                {
                    var valueRef = values[i];
                    var directTarget = valueRef.DirectTarget;
                    if (toRemove.Contains(directTarget) ||
                        directTarget.IsReplaced &&
                        toRemove.Contains(valueRef.Resolve()))
                    {
                        // Replace if possible
                        if (!directTarget.IsReplaced)
                            directTarget.Replace(CreateUndefined());
                        continue;
                    }
                    targetCollection.Add(valueRef);
                }

                toRemove.Clear();
            }

            /// <summary>
            /// Updates phi values in the current block to point to the new blocks
            /// instead.
            /// </summary>
            /// <typeparam name="TArgumentRemapper">The argument mapper type.</typeparam>
            /// <param name="remapper">The remapper to use.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemapPhiArguments<TArgumentRemapper>(TArgumentRemapper remapper)
                where TArgumentRemapper : PhiValue.IArgumentRemapper
            {
                foreach (ValueEntry value in this)
                {
                    if (!(value.Value is PhiValue phiValue))
                        continue;
                    SetupInsertPosition(value);
                    phiValue.RemapArguments(this, remapper);
                }
            }

            /// <summary>
            /// Updates phi values in the supplied blocks to point to the new blocks
            /// instead.
            /// </summary>
            /// <typeparam name="TArgumentRemapper">The argument mapper type.</typeparam>
            /// <param name="blocks">
            /// The blocks containing phi values to be updated.
            /// </param>
            /// <param name="remapper">The remapper to use.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemapPhiArguments<TArgumentRemapper>(
                ReadOnlySpan<BasicBlock> blocks,
                TArgumentRemapper remapper)
                where TArgumentRemapper : PhiValue.IArgumentRemapper
            {
                foreach (var block in blocks)
                {
                    var blockBuilder = MethodBuilder[block];
                    blockBuilder.RemapPhiArguments(remapper);
                }
            }

            /// <summary>
            /// Updates the type of the given phi value.
            /// </summary>
            /// <typeparam name="TTypeConverter">The type converter.</typeparam>
            /// <param name="phiValue">The phi value.</param>
            /// <param name="typeConverter">The type converter instance.</param>
            public void UpdatePhiType<TTypeConverter>(
                PhiValue phiValue,
                TTypeConverter typeConverter)
                where TTypeConverter : ITypeConverter<TypeNode> =>
                phiValue.UpdateType(Context, typeConverter);

            /// <summary>
            /// Specializes a function call.
            /// </summary>
            /// <param name="call">The call to specialize.</param>
            /// <returns>The created target block.</returns>
            public Builder SpecializeCall(MethodCall call)
            {
                // Perform local rebuilding step
                var callTarget = call.Target;
                var tempBlock = SplitBlock(call, false);
                var mapping = callTarget.CreateParameterMapping(call.Nodes);
                var rebuilder = MethodBuilder.CreateRebuilder<IRRebuilder.InlineMode>(
                    mapping,
                    callTarget.Blocks);
                var (exitBlock, exitValue) = rebuilder.Rebuild();

                // Wire current block with new entry block
                CreateBranch(
                    call.Location,
                    rebuilder.EntryBlock.BasicBlock);

                // Replace call with the appropriate return value
                call.Replace(exitValue);

                // Unlink call node from the current block
                Remove(call);

                // Wire exit block with temp block
                exitBlock.CreateBranch(call.Location, tempBlock.BasicBlock);

                return tempBlock;
            }

            /// <summary>
            /// Splits the current block at the given value.
            /// </summary>
            /// <param name="splitPoint">The split point.</param>
            /// <param name="keepSplitPoint">
            /// True, if you want to keep the split point.
            /// </param>
            /// <returns>The created temporary block.</returns>
            public Builder SplitBlock(Value splitPoint, bool keepSplitPoint)
            {
                PerformRemoval();
                this.AssertNotNull(splitPoint);

                // Create a new basic block to jump to
                var valueIndex = Values.IndexOf(splitPoint);
                var splitPointOffset = keepSplitPoint ? 0 : 1;
                this.Assert(valueIndex >= 0);

                // Create temp block and move instructions
                var tempBlock = MethodBuilder.CreateBasicBlock(
                    splitPoint.Location,
                    BasicBlock.Name + "'");
                for (int i = valueIndex + splitPointOffset, e = Count; i < e; ++i)
                {
                    Value valueToMove = Values[i];
                    valueToMove.BasicBlock = tempBlock.BasicBlock;
                    tempBlock.Add(valueToMove);
                }
                while (Count > valueIndex)
                    Values.RemoveAt(Count - 1);

                // Adjust terminators
                tempBlock.Terminator = Terminator;
                Terminator = null;
                CreateBranch(
                    splitPoint.Location,
                    tempBlock.BasicBlock);

                // Update phi blocks
                RemapPhiArguments(
                    tempBlock.BasicBlock.CurrentSuccessors,
                    new PhiValue.BlockRemapper(BasicBlock, tempBlock.BasicBlock));

                return tempBlock;
            }

            /// <summary>
            /// Merges the given block into the current one.
            /// </summary>
            /// <param name="other">The other block to merge.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MergeBlock(BasicBlock other)
            {
                this.AssertNotNull(other);
                this.Assert(other != BasicBlock);

                var otherBuilder = MethodBuilder[other];

                int offset = Count;
                otherBuilder.PerformRemoval(values);

                // Attach values to another block
                for (; offset < Count; ++offset)
                {
                    Value movedValue = values[offset];
                    movedValue.BasicBlock = BasicBlock;
                }
                otherBuilder.ClearLists();

                // Wire terminators
                Terminator = other.Terminator;

                // Update phi blocks
                RemapPhiArguments(
                    other.CurrentSuccessors,
                    new PhiValue.BlockRemapper(other, BasicBlock));
            }

            /// <summary>
            /// Replaces the given value with a call to the provided function.
            /// </summary>
            /// <param name="value">The value to replace.</param>
            /// <param name="implementationMethod">
            /// The target implementation method.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReplaceWithCall(Value value, Method implementationMethod)
            {
                this.AssertNotNull(value);
                this.Assert(value.BasicBlock == BasicBlock);
                this.AssertNotNull(implementationMethod);
                this.Assert(BasicBlock.Method != implementationMethod);

                int oldPosition = InsertPosition;
                SetupInsertPosition(value);

                var arguments = ValueList.Empty;
                value.Nodes.CopyTo(ref arguments);
                var call = CreateCall(
                    value.Location,
                    implementationMethod,
                    ref arguments);
                value.Replace(call);
                Remove(value);

                InsertPosition = oldPosition;
            }

            /// <summary cref="IRBuilder.CreateTerminator{T}(T)"/>
            protected override T CreateTerminator<T>(T node)
            {
                Terminator?.Replace(node);
                return (Terminator = node) as T;
            }

            /// <summary cref="IRBuilder.CreatePhiValue(PhiValue)"/>
            protected override PhiValue CreatePhiValue(PhiValue phiValue)
            {
                InsertAtBeginning(phiValue);
                return phiValue;
            }

            /// <summary cref="IRBuilder.Append{T}(T)"/>
            protected override T Append<T>(T node)
            {
                // Insert node into current basic block builder
                Add(node);
                return node;
            }

            /// <summary>
            /// Implicitly converts the current builder into its associated basic block.
            /// </summary>
            public BasicBlock ToBasicBlock() => BasicBlock;

            /// <summary>
            /// Dumps the underlying method to the given text writer.
            /// </summary>
            /// <param name="textWriter">The text writer.</param>
            public void Dump(TextWriter textWriter) => BasicBlock.Dump(textWriter);

            #endregion

            #region Operators

            /// <summary>
            /// Implicitly converts the given builder into its associated basic block.
            /// </summary>
            /// <param name="builder">The builder to convert.</param>
            public static implicit operator BasicBlock(Builder builder)
            {
                Debug.Assert(builder != null, "Invalid block builder");
                return builder.BasicBlock;
            }

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns a value enumerator.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(BasicBlock);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<ValueEntry> IEnumerable<ValueEntry>.GetEnumerator() =>
                GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion

            #region IDisposable

            /// <summary cref="DisposeBase.Dispose(bool)"/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    PerformRemoval();
                    BasicBlock.ReleaseBuilder(this);
                }
                base.Dispose(disposing);
            }

            #endregion
        }
    }
}
