// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: easter-egg.js
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

function easteregg() {
    const span = document.getElementById('easter-egg');
    if (span.textContent === 'ö') {
        span.textContent = 'œ';
    } else if (span.textContent === 'œ') {
        span.textContent = 'ö';
    }
}
