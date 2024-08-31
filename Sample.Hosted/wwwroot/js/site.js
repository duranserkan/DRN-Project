/* global bootstrap: false */
(() => {
  'use strict'
  const tooltipTriggerList = Array.from(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
  tooltipTriggerList.forEach(tooltipTriggerEl => {
    new bootstrap.Tooltip(tooltipTriggerEl)
  });

  document.getElementById('sidebarToggle').addEventListener('click', function () {
    var fullSidebar = document.getElementById('sidebar-full');
    var collapsedSidebar = document.getElementById('sidebar-collapsed');

    fullSidebar.classList.toggle('collapsed');
    collapsedSidebar.classList.toggle('collapsed');
  });
})()