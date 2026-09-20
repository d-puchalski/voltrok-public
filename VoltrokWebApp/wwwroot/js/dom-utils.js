export function scrollElementIntoView(element) {
    if (!element) {
        return;
    }

    element.scrollIntoView({
        behavior: "smooth",
        block: "start",
        inline: "nearest"
    });
}
