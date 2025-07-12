// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLDeviceVersion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace ILGPU.Runtime.OpenCL;

/// <summary>
/// Represents an OpenCL device version.
/// </summary>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
public readonly partial record struct CLDeviceVersion(int Major, int Minor)
{
    #region Static

    /// <summary>
    /// The OpenCL device version 1.0.
    /// </summary>
    public static readonly CLDeviceVersion CL10 = new(1, 0);

    /// <summary>
    /// The OpenCL device version 1.1.
    /// </summary>
    public static readonly CLDeviceVersion CL11 = new(1, 1);

    /// <summary>
    /// The OpenCL device version 1.2.
    /// </summary>
    public static readonly CLDeviceVersion CL12 = new(1, 2);

    /// <summary>
    /// The OpenCL device version 2.0.
    /// </summary>
    public static readonly CLDeviceVersion CL20 = new(2, 0);

    /// <summary>
    /// The OpenCL device version 2.1.
    /// </summary>
    public static readonly CLDeviceVersion CL21 = new(2, 1);

    /// <summary>
    /// The OpenCL device version 2.2.
    /// </summary>
    public static readonly CLDeviceVersion CL22 = new(2, 2);

    /// <summary>
    /// The OpenCL device version 3.0.
    /// </summary>
    public static readonly CLDeviceVersion CL30 = new(3, 0);

    [GeneratedRegex("\\s*OpenCL ([0-9]+).([0-9]+)\\s*.*")]
    private static partial Regex OpenCLDeviceVersionRegex();

    /// <summary>
    /// The internal regex that is used to parse OpenCL versions.
    /// </summary>
    private static readonly Regex VersionRegex =
        OpenCLDeviceVersionRegex();

    /// <summary>
    /// Tries to parse the given string expression into an OpenCL C version.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="version">The parsed version (if any).</param>
    /// <returns>
    /// True, if the given expression could be parsed into an OpenCL C version.
    /// </returns>
    public static bool TryParse(string expression, out CLDeviceVersion version)
    {
        version = default;
        var match = VersionRegex.Match(expression);
        if (!match.Success)
            return false;
        version = new CLDeviceVersion(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value));
        return true;
    }

    #endregion

    #region Object

    /// <summary>
    /// Returns the OpenCL device string representation.
    /// </summary>
    /// <returns>The string representation of this OpenCL device version.</returns>
    public override string ToString() => $"{Major}.{Minor}";

    #endregion

    #region Operators

    /// <summary>
    /// Returns true if the first version is smaller than the second one.
    /// </summary>
    /// <param name="first">The first version.</param>
    /// <param name="second">The second version.</param>
    /// <returns>True, if the first version is smaller than the second one.</returns>
    public static bool operator <(
        CLDeviceVersion first,
        CLDeviceVersion second) =>
        first.Major < second.Major ||
        first.Major == second.Major && first.Minor < second.Minor;

    /// <summary>
    /// Returns true if the first version is greater than the second one.
    /// </summary>
    /// <param name="first">The first version.</param>
    /// <param name="second">The second version.</param>
    /// <returns>True, if the first version is greater than the second one.</returns>
    public static bool operator >(
        CLDeviceVersion first,
        CLDeviceVersion second) =>
        first.Major > second.Major ||
        first.Major == second.Major && first.Minor > second.Minor;

    /// <summary>
    /// Returns true if the first version is smaller than or equal to the second one.
    /// </summary>
    /// <param name="first">The first version.</param>
    /// <param name="second">The second version.</param>
    /// <returns>True, if the first version is smaller than the second one.</returns>
    public static bool operator <=(
        CLDeviceVersion first,
        CLDeviceVersion second) =>
        first.Major <= second.Major ||
        first.Major == second.Major && first.Minor <= second.Minor;

    /// <summary>
    /// Returns true if the first version is greater than or equal to the second one.
    /// </summary>
    /// <param name="first">The first version.</param>
    /// <param name="second">The second version.</param>
    /// <returns>True, if the first version is greater than the second one.</returns>
    public static bool operator >=(
        CLDeviceVersion first,
        CLDeviceVersion second) =>
        first.Major >= second.Major ||
        first.Major == second.Major && first.Minor >= second.Minor;

    #endregion
}
