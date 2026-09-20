// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

const AUTO_RELOAD_DELAY_MS = 5000;
let autoReloadTimeoutId = null;

function handleReconnectStateChanged(event) {
    if (event.detail.state === "show") {
        cancelAutoReload();
        reconnectModal.showModal();
    } else if (event.detail.state === "hide") {
        cancelAutoReload();
        reconnectModal.close();
    } else if (event.detail.state === "failed") {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        scheduleAutoReload();
    } else if (event.detail.state === "rejected") {
        triggerReload();
    } else if (event.detail.state === "resume-failed") {
        scheduleAutoReload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    cancelAutoReload();

    try {
        // Reconnect will asynchronously return:
        // - true to mean success
        // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
        // - exception to mean we didn't reach the server (this can be sync or async)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // We have been able to reach the server, but the circuit is no longer available.
            // We'll reload the page so the user can continue using the app as quickly as possible.
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                triggerReload();
            } else {
                cancelAutoReload();
                reconnectModal.close();
            }
        }
    } catch (err) {
        // We got an exception, server is currently unavailable
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        scheduleAutoReload();
    }
}

async function resume() {
    cancelAutoReload();

    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            triggerReload();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
        scheduleAutoReload();
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}

function scheduleAutoReload() {
    if (autoReloadTimeoutId !== null) {
        return;
    }

    autoReloadTimeoutId = window.setTimeout(() => {
        autoReloadTimeoutId = null;
        triggerReload();
    }, AUTO_RELOAD_DELAY_MS);
}

function cancelAutoReload() {
    if (autoReloadTimeoutId === null) {
        return;
    }

    window.clearTimeout(autoReloadTimeoutId);
    autoReloadTimeoutId = null;
}

function triggerReload() {
    cancelAutoReload();
    location.reload();
}
