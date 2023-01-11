// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: gui.js
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

export class Gui {
    constructor() {
    }


    addElementToSelect(item, value) {
        const select = document.getElementById("optimizationLevel");
        const option = document.createElement("OPTION"),
            txt = document.createTextNode(item);
        option.appendChild(txt);
        option.setAttribute("value", value);
        select.insertBefore(option, select.lastChild);
    }

    enableCompileButton() {
        let btn = document.getElementById('compile');
        btn.disabled = false;
        btn.innerHTML = 'Compile';
    }
}
