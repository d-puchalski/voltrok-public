window.authRegister = window.authRegister || {};

window.authRegister.scrollToNext = (selector) => {
    if (!selector) {
        return;
    }

    const target = document.querySelector(selector);
    if (!target) {
        return;
    }

    const scrollContainer = target.closest('.register-form-body, .main-content-panel, .dialog-body-container');
    if (scrollContainer) {
        const containerRect = scrollContainer.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const offset = targetRect.top - containerRect.top + scrollContainer.scrollTop - 12;
        scrollContainer.scrollTo({ top: Math.max(offset, 0), behavior: 'smooth' });
    } else {
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    const focusTarget = target.matches('input,select,textarea,button')
        ? target
        : target.querySelector('input,select,textarea,button');

    if (focusTarget) {
        window.setTimeout(() => {
            focusTarget.focus({ preventScroll: true });
        }, 220);
    }
};

window.authRegister.trackLead = () => {
    if (typeof window.fbq === 'function') {
        window.fbq('track', 'Lead');
    }
};
