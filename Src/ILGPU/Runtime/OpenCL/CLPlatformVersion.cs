// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLPlatformVersion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL platform version.
    /// </summary>
    public readonly struct CLPlatformVersion
    {
        #region Static

        /// <summary>
        /// The OpenCL API version 1.0.
        /// </summary>
        public static readonly CLPlatformVersion CL10 = new CLPlatformVersion(1, 0);

        /// <summary>
        /// The OpenCL API version 2.0.
        /// </summary>
        public static readonly CLPlatformVersion CL20 = new CLPlatformVersion(2, 0);

        /// <summary>
        /// The internal regex that is used to parse OpenCL versions.
        /// </summary>
        private static readonly Regex VersionRegex =
            new Regex("\\s*OpenCL ([0-9]+).([0-9]+)\\s*.*");

        /// <summary>
        /// Tries to parse the given string expression into an OpenCL C version.
        /// </summary>
        /// <param name="expression">The expression to parse.</param>
        /// <param name="version">The parsed version (if any).</param>
        /// <returns>
        /// True, if the given expression could be parsed into an OpenCL C version.
        /// </returns>
        public static bool TryParse(string expression, out CLPlatformVersion version)
        {
            version = default;
            var match = VersionRegex.Match(expression);
            if (!match.Success)
                return false;
            version = new CLPlatformVersion(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value));
            return true;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL platform version.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        public CLPlatformVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The major OpenCL platform Version.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// The minor OpenCL platform Version.
        /// </summary>
        public int Minor { get; }

        #endregion

        #region Object

        /// <summary>
        /// Returns the OpenCL platform string representation.
        /// </summary>
        /// <returns>The string representation of this OpenCL platform version.</returns>
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
            CLPlatformVersion first,
            CLPlatformVersion second) =>
            first.Major < second.Major ||
            first.Major == second.Major && first.Minor < second.Minor;

        /// <summary>
        /// Returns true if the first version is greater than the second one.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, if the first version is greater than the second one.</returns>
        public static bool operator >(
            CLPlatformVersion first,
            CLPlatformVersion second) =>
            first.Major > second.Major ||
            first.Major == second.Major && first.Minor > second.Minor;

        /// <summary>
        /// Returns true if the first version is smaller than or equal to the second one.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, if the first version is smaller than the second one.</returns>
        public static bool operator <=(
            CLPlatformVersion first,
            CLPlatformVersion second) =>
            first.Major <= second.Major ||
            first.Major == second.Major && first.Minor <= second.Minor;

        /// <summary>
        /// Returns true if the first version is greater than or equal to the second one.
        /// </summary>
        /// <param name="first">The first version.</param>
        /// <param name="second">The second version.</param>
        /// <returns>True, if the first version is greater than the second one.</returns>
        public static bool operator >=(
            CLPlatformVersion first,
            CLPlatformVersion second) =>
            first.Major >= second.Major ||
            first.Major == second.Major && first.Minor >= second.Minor;

        #endregion
    }
}
