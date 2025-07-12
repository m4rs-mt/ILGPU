// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NodeId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPUC.IR;

/// <summary>
/// Represents a node id.
/// </summary>
/// <param name="Id">The raw id.</param>
readonly record struct NodeId(long Id) : IEquatable<NodeId>, IComparable<NodeId>
{
    #region Static

    private static long instanceIdCounter;

    /// <summary>
    /// Creates a new unique node id.
    /// </summary>
    /// <returns>A new unique node id.</returns>
    public static NodeId CreateNew() => new(Interlocked.Add(ref instanceIdCounter, 1));

    #endregion

    #region Methods

    /// <summary>
    /// Returns true if the given id is equal to this node id.
    /// </summary>
    /// <param name="id">The id to test.</param>
    /// <returns>True if the given id is equal to this node id.</returns>
    public bool Is(long id) => Id == id;

    /// <summary>
    /// Returns a compatible function name for all runtime backends.
    /// </summary>
    /// <param name="name">The source name.</param>
    public string GetCompatibleName(string name)
    {
        var chars = name.ToCharArray();
        for (int i = 0, e = chars.Length; i < e; ++i)
        {
            ref var charValue = ref chars[i];
            // Map to ASCII and letter/digit characters only
            if (charValue >= 128 || !char.IsLetterOrDigit(charValue))
                charValue = '_';
        }
        return $"{new string(chars)}_{Id}";
    }

    #endregion

    #region IComparable

    /// <summary>
    /// Compares this id to the given one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(NodeId other) => Id.CompareTo(other.Id);

    #endregion

    #region Object

    /// <summary>
    /// Returns the string representation of this id.
    /// </summary>
    /// <returns>The string representation of this id.</returns>
    public override string ToString() => $"{Id}";

    #endregion

    #region Operators

    /// <summary>
    /// Converts the given node id into its underlying long value.
    /// </summary>
    /// <param name="nodeId">The node id.</param>
    public static implicit operator long(NodeId nodeId) => nodeId.Id;

    /// <summary>
    /// Returns true if the first id is smaller than the second one.
    /// </summary>
    /// <param name="first">The first id.</param>
    /// <param name="second">The second id.</param>
    /// <returns>True, if the first id is smaller than the second one.</returns>
    public static bool operator <(NodeId first, NodeId second) =>
        first.Id < second.Id;

    /// <summary>
    /// Returns true if the first id is smaller than or equal to the second one.
    /// </summary>
    /// <param name="first">The first id.</param>
    /// <param name="second">The second id.</param>
    /// <returns>
    /// True, if the first id is smaller than or equal to the second one.
    /// </returns>
    public static bool operator <=(NodeId first, NodeId second) =>
        first.Id <= second.Id;

    /// <summary>
    /// Returns true if the first id is greater than the second one.
    /// </summary>
    /// <param name="first">The first id.</param>
    /// <param name="second">The second id.</param>
    /// <returns>True, if the first id is greater than the second one.</returns>
    public static bool operator >(NodeId first, NodeId second) =>
        first.Id > second.Id;

    /// <summary>
    /// Returns true if the first id is greater than or equal to the second one.
    /// </summary>
    /// <param name="first">The first id.</param>
    /// <param name="second">The second id.</param>
    /// <returns>
    /// True, if the first id is greater than or equal to the second one.
    /// </returns>
    public static bool operator >=(NodeId first, NodeId second) =>
        first.Id >= second.Id;

    #endregion
}
