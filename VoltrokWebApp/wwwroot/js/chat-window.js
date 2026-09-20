export function enableChatWindowDrag(chatWindow, handleSelector) {
    if (!chatWindow) {
        return;
    }

    const handle = handleSelector ? chatWindow.querySelector(handleSelector) : chatWindow;
    if (!handle) {
        return;
    }

    let isDragging = false;
    let startX = 0;
    let startY = 0;
    let startLeft = 0;
    let startTop = 0;

    const getPosition = () => {
        const rect = chatWindow.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top
        };
    };

    const onPointerMove = (event) => {
        if (!isDragging) {
            return;
        }

        const deltaX = event.clientX - startX;
        const deltaY = event.clientY - startY;
        chatWindow.style.left = `${startLeft + deltaX}px`;
        chatWindow.style.top = `${startTop + deltaY}px`;
    };

    const stopDragging = () => {
        if (!isDragging) {
            return;
        }

        isDragging = false;
        document.removeEventListener("pointermove", onPointerMove);
        document.removeEventListener("pointerup", stopDragging);
    };

    handle.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) {
            return;
        }

        event.preventDefault();
        const position = getPosition();
        chatWindow.style.transform = "none";
        chatWindow.style.right = "auto";
        chatWindow.style.left = `${position.left}px`;
        chatWindow.style.top = `${position.top}px`;
        startLeft = position.left;
        startTop = position.top;
        startX = event.clientX;
        startY = event.clientY;
        isDragging = true;
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup", stopDragging);
    });
}

export function enableFloatingPanelDrag(panel, handleSelector) {
    if (!panel) {
        return;
    }

    if (panel.dataset.floatingPanelDragInitialized === "true") {
        const syncPosition = panel.__syncFloatingPanelPosition;
        if (typeof syncPosition === "function") {
            syncPosition();
        }
        return;
    }

    const handle = handleSelector ? panel.querySelector(handleSelector) : panel;
    if (!handle) {
        return;
    }

    let isDragging = false;
    let startX = 0;
    let startY = 0;
    let startLeft = 0;
    let startBottom = 0;

    const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

    const getPosition = () => {
        const rect = panel.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            bottom: window.innerHeight - rect.bottom,
            width: rect.width,
            height: rect.height
        };
    };

    const syncPositionToViewport = () => {
        const position = getPosition();
        const nextLeft = clamp(position.left, 0, Math.max(0, window.innerWidth - position.width));
        const nextBottom = clamp(position.bottom, 0, Math.max(0, window.innerHeight - position.height));

        panel.style.transform = "none";
        panel.style.right = "auto";
        panel.style.left = `${nextLeft}px`;
        panel.style.top = "auto";
        panel.style.bottom = `${nextBottom}px`;
    };

    const onPointerMove = (event) => {
        if (!isDragging) {
            return;
        }

        const deltaX = event.clientX - startX;
        const deltaY = event.clientY - startY;
        const nextLeft = clamp(startLeft + deltaX, 0, Math.max(0, window.innerWidth - panel.offsetWidth));
        const nextBottom = clamp(startBottom - deltaY, 0, Math.max(0, window.innerHeight - panel.offsetHeight));

        panel.style.right = "auto";
        panel.style.left = `${nextLeft}px`;
        panel.style.top = "auto";
        panel.style.bottom = `${nextBottom}px`;
    };

    const stopDragging = () => {
        if (!isDragging) {
            return;
        }

        isDragging = false;
        document.removeEventListener("pointermove", onPointerMove);
        document.removeEventListener("pointerup", stopDragging);
    };

    handle.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) {
            return;
        }

        event.preventDefault();
        const position = getPosition();
        panel.style.transform = "none";
        panel.style.right = "auto";
        panel.style.left = `${position.left}px`;
        panel.style.top = "auto";
        panel.style.bottom = `${position.bottom}px`;
        startLeft = position.left;
        startBottom = position.bottom;
        startX = event.clientX;
        startY = event.clientY;
        isDragging = true;
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup", stopDragging);
    });

    panel.__syncFloatingPanelPosition = syncPositionToViewport;
    panel.dataset.floatingPanelDragInitialized = "true";
    window.addEventListener("resize", syncPositionToViewport, { passive: true });
    syncPositionToViewport();
}

export function enableOnboardingPanelDrag(panel, handleSelector) {
    if (!panel) {
        return;
    }

    if (panel.dataset.onboardingPanelDragInitialized === "true") {
        const syncPosition = panel.__syncOnboardingPanelPosition;
        if (typeof syncPosition === "function") {
            syncPosition();
        }
        return;
    }

    const handle = handleSelector ? panel.querySelector(handleSelector) : panel;
    if (!handle) {
        return;
    }

    handle.style.touchAction = "none";
    handle.style.cursor = "grab";

    let isDragging = false;
    let startX = 0;
    let startY = 0;
    let startLeft = 0;
    let startTop = 0;

    const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

    const getPosition = () => {
        const rect = panel.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            width: rect.width,
            height: rect.height
        };
    };

    const syncPositionToViewport = () => {
        const position = getPosition();
        const nextLeft = clamp(position.left, 0, Math.max(0, window.innerWidth - position.width));
        const nextTop = clamp(position.top, 0, Math.max(0, window.innerHeight - position.height));

        panel.style.transform = "none";
        panel.style.right = "auto";
        panel.style.left = `${nextLeft}px`;
        panel.style.top = `${nextTop}px`;
        panel.style.bottom = "auto";
    };

    const onPointerMove = (event) => {
        if (!isDragging) {
            return;
        }

        const deltaX = event.clientX - startX;
        const deltaY = event.clientY - startY;
        const nextLeft = clamp(startLeft + deltaX, 0, Math.max(0, window.innerWidth - panel.offsetWidth));
        const nextTop = clamp(startTop + deltaY, 0, Math.max(0, window.innerHeight - panel.offsetHeight));

        panel.style.right = "auto";
        panel.style.left = `${nextLeft}px`;
        panel.style.top = `${nextTop}px`;
        panel.style.bottom = "auto";
    };

    const stopDragging = () => {
        if (!isDragging) {
            return;
        }

        isDragging = false;
        document.removeEventListener("pointermove", onPointerMove);
        document.removeEventListener("pointerup", stopDragging);
        panel.style.userSelect = "";
        handle.style.cursor = "grab";
    };

    handle.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) {
            return;
        }

        event.preventDefault();
        const position = getPosition();
        panel.style.transform = "none";
        panel.style.right = "auto";
        panel.style.left = `${position.left}px`;
        panel.style.top = `${position.top}px`;
        panel.style.bottom = "auto";
        startLeft = position.left;
        startTop = position.top;
        startX = event.clientX;
        startY = event.clientY;
        isDragging = true;
        handle.style.cursor = "grabbing";
        panel.style.userSelect = "none";
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup", stopDragging);
    });

    panel.__syncOnboardingPanelPosition = syncPositionToViewport;
    panel.dataset.onboardingPanelDragInitialized = "true";
    window.addEventListener("resize", syncPositionToViewport, { passive: true });
    syncPositionToViewport();
}
