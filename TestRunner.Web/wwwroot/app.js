// Apply theme from localStorage to the document root
window.applyTheme = () => {
    const theme = localStorage.getItem('prat-theme') || 'dark';
    document.documentElement.setAttribute('data-bs-theme', theme);
    return theme;
};

window.getTheme = () => {
    return localStorage.getItem('prat-theme') || 'dark';
};

window.setTheme = (theme) => {
    localStorage.setItem('prat-theme', theme);
    document.documentElement.setAttribute('data-bs-theme', theme);
};

window.toggleTheme = () => {
    const current = document.documentElement.getAttribute('data-bs-theme') || 'dark';
    const next = current === 'dark' ? 'light' : 'dark';
    window.setTheme(next);
    return next;
};

// Reapply theme after Blazor navigation — covers both enhanced nav and circuit reconnects
document.addEventListener('blazor:navigated', () => {
    window.applyTheme();
});

// Also protect against Blazor resetting the attribute via a MutationObserver
// This catches any DOM diffing that removes data-bs-theme from <html>
const themeObserver = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
        if (mutation.attributeName === 'data-bs-theme') {
            const current = document.documentElement.getAttribute('data-bs-theme');
            const saved   = localStorage.getItem('prat-theme') || 'dark';
            if (current !== saved) {
                document.documentElement.setAttribute('data-bs-theme', saved);
            }
        }
    }
});

themeObserver.observe(document.documentElement, { attributes: true });

// Scroll output panel to bottom
window.scrollToBottom = (id) => {
    const el = document.getElementById(id);
    if (el) el.scrollTop = el.scrollHeight;
};

// Calculate and set CSS variable for dynamic output panel height
window.setOutputTop = () => {
    const wrapper = document.getElementById('output-wrapper');
    if (!wrapper) return;
    const rect = wrapper.getBoundingClientRect();
    const top  = rect.top + window.scrollY;
    document.documentElement.style.setProperty('--output-top', `${top}px`);
};

window.addEventListener('resize', () => {
    window.setOutputTop();
});