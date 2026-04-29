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

// Reapply theme after Blazor navigation
document.addEventListener('blazor:navigated', () => {
    window.applyTheme();
});

// Protect against Blazor DOM diffing resetting data-bs-theme
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

// Initialise Bootstrap tooltips on all elements with data-bs-toggle="tooltip"
window.initTooltips = () => {
    const tooltipEls = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipEls.forEach(el => {
        const existing = bootstrap.Tooltip.getInstance(el);
        if (existing) existing.dispose();
        new bootstrap.Tooltip(el, {
            trigger: 'hover focus',
            html: true
        });
    });
};

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
