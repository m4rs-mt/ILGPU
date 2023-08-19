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
   const menuToggle = document.getElementById("sidebar-collapse-icon");
   const sidebar = document.getElementById("sidebar");
   const content = document.getElementById("wiki-main");
   const breadcrumbs = document.querySelector("#breadcrumbs .container");

   const elements = [menuToggle, sidebar, content, breadcrumbs];

   menuToggle.addEventListener('click', toggleCollapsed);

   function toggleCollapsed() {
      for (const element of elements) {
         element.classList.add("sidebar-animated");
         element.classList.toggle("sidebar-collapsed");
      }

      const isCollapsed = localStorage.getItem("sidebar-collapse") === "true";
      localStorage.setItem("sidebar-collapse", isCollapsed ? "false" : "true");
   }

   if(localStorage.getItem("sidebar-collapse") === "true")  {
      for (const element of elements) {
         element.classList.remove("sidebar-animated");
         element.classList.toggle("sidebar-collapsed");
      }
   }
}

window.addEventListener('load', main);
