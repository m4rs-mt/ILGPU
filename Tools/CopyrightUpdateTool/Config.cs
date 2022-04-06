// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Config.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;

// disable: max_line_length

namespace CopyrightUpdateTool
{
    static class Config
    {
        public const int LineLength = 90;

        public const string CopyrightText = "Copyright (c)";

        public const string CopyrightOwner = "ILGPU Project";

        public const string CopyrightOwnerWebsite = "www.ilgpu.net";

        public static readonly ImmutableArray<string> CopyrightLicenseText =
            ImmutableArray.Create(
                "This file is part of ILGPU and is distributed under the University of Illinois Open",
                "Source License. See LICENSE.txt for details."
            );
    }
}
