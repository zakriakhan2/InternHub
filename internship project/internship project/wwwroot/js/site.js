// InternHub — small helpers Bootstrap doesn't cover out of the box.
// Modals, tabs, and the sidebar collapse are all native Bootstrap components
// (data-bs-toggle attributes in the markup) — no JS needed for those.
(function () {
  'use strict';

  // Switch the active conversation in a chat widget
  document.querySelectorAll('.chat-list .conv').forEach(function (item) {
    item.addEventListener('click', function () {
      var list = item.closest('.chat-list');
      list.querySelectorAll('.conv').forEach(function (i) { i.classList.remove('active'); });
      item.classList.add('active');
      var nameEl = item.closest('.chat-panel').querySelector('[data-chat-active-name]');
      if (nameEl) nameEl.textContent = item.getAttribute('data-name') || '';
    });
  });

  // Mock "send" — appends message locally; wire this up to SignalR later
  document.querySelectorAll('[data-chat-send]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      e.preventDefault();
      var input = form.querySelector('input');
      if (!input.value.trim()) return;
      var messages = form.closest('.chat-body').querySelector('.chat-messages');
      var bubble = document.createElement('div');
      bubble.className = 'bubble out';
      var time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      bubble.innerHTML = input.value.replace(/</g, '&lt;') + '<span class="time">' + time + '</span>';
      messages.appendChild(bubble);
      messages.scrollTop = messages.scrollHeight;
      input.value = '';
    });
  });
})();
