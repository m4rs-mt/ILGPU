// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: sidebar-collapse.js
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

function main() {
   const menuToggle = document.getElementById("sidebar-collapse");
   const sidebar = document.getElementById("sidebar");
   const content = document.getElementById("wiki-main");
   const breadcrumbs = document.querySelector("#breadcrumbs .container");

   menuToggle.addEventListener('click', toggleCollapsed);

   function toggleCollapsed() {
      menuToggle.classList.toggle("sidebar-collapsed");
      sidebar.classList.toggle("sidebar-collapsed");
      content.classList.toggle("sidebar-collapsed");
      breadcrumbs.classList.toggle("sidebar-collapsed");
   }
}

window.addEventListener('load', main);
