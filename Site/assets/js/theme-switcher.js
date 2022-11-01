// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: theme-switcher.js
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

function main() {
    // get default theme
    let defaultTheme = 'light';
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        defaultTheme = 'dark';
    }
    // Set the theme from localStorage(if exist) || or set default light
    // let theme = window.localStorage.getItem('theme') || 'light';
    let theme = window.localStorage.getItem('theme') || defaultTheme || '';
    // Activate the current theme (using class)
    const body = document.getElementsByTagName('body')[0];
    // const body = document.body;
    body.classList = theme;
    // Available Themes
    let available_themes = document.querySelectorAll('[dd-theme]');
    // Toggle Dark / Light theme
    let _toggler = document.querySelector('[dd-toggle]');
    if (_toggler) {
        _toggler.addEventListener('click', toggleTheme);
    }

    // Function: toggle theme(light & dark)
    function toggleTheme() {
        // Toggle light / dark theme
        theme = theme === 'light' ? 'dark' : 'light';
        // Apply class to body
        body.classList = theme;
        // Store theme var to localStorage
        window.localStorage.setItem('theme', theme);
    }
}

window.addEventListener('load', main);
