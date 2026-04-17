window.scrollToBottom = (id) => {
    const el = document.getElementById(id);
    if (el) el.scrollTop = el.scrollHeight;
};

// Prevent Blazor from treating tab focus changes as reconnection events
document.addEventListener('visibilitychange', (e) => e.stopImmediatePropagation(), true);
