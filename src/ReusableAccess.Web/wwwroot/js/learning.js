/* Interop for focus and one-shot media fallback; no teaching data is stored here. */
(() => {
    "use strict";
    document.addEventListener("error", e => {
        const image = e.target;
        if (!(image instanceof HTMLImageElement) || !image.dataset.learningFallback) return;
        const fallback = image.dataset.learningFallback;
        delete image.dataset.learningFallback;
        image.src = fallback;
    }, true);
    window.raLearning = {
        jump(heading) {
            const chapter = document.getElementById(heading);
            if (!(chapter instanceof HTMLDetailsElement)) return;
            chapter.open = true;
            chapter.querySelector("summary")?.focus({preventScroll: true});
            chapter.scrollIntoView({block: "start"});
        }
    };
})();
