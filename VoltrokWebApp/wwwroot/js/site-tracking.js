window.voltrokTracking = window.voltrokTracking || {
    trackLandingIntroOpened() {
        if (typeof window.gtag === "function") {
            window.gtag("event", "landing_intro_opened");
        }

        if (typeof window.fbq === "function") {
            window.fbq("trackCustom", "LandingIntroOpened");
        }

        if (typeof window.clarity === "function") {
            window.clarity("event", "landing_intro_opened");
        }
    },

    trackLandingIntroDismissed(action) {
        const normalizedAction = typeof action === "string" && action.trim().length > 0
            ? action.trim()
            : "dismiss";

        if (typeof window.gtag === "function") {
            window.gtag("event", "landing_intro_closed", { action: normalizedAction });
        }

        if (typeof window.fbq === "function") {
            window.fbq("trackCustom", "LandingIntroClosed", { action: normalizedAction });
        }

        if (typeof window.clarity === "function") {
            window.clarity("event", `landing_intro_${normalizedAction}`);
        }
    },

    trackOnboardingCompleted() {
        if (typeof window.gtag === "function") {
            window.gtag("event", "game_onboarding_completed");
        }

        if (typeof window.fbq === "function") {
            window.fbq("trackCustom", "GameOnboardingCompleted");
        }

        if (typeof window.clarity === "function") {
            window.clarity("event", "game_onboarding_completed");
        }
    }
};
