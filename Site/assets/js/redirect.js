---
# Add this header to tell Jekyll to process the file
---

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: redirect.js
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

function redirectAfterFew() {
    setTimeout(function () {
        window.location.href = '{{ "/" | relative_url }}';
    }, 3000);
}

window.addEventListener('load', redirectAfterFew);
