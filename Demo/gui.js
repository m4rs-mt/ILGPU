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