(() => {
    "use strict";

    const PLUGIN_ID = "9985ba03-cbb5-4b44-a997-3a141a1232ab";
    // Not anchored on "#indexPage:not(.hide)" as well: on some builds the home tab
    // (#homeTab.is-active > .homeSectionsContainer) renders nested inside a React-mounted
    // subtree where #indexPage's presence/hide state can't be relied on. #homeTab.is-active is
    // specific enough on its own to mean "the currently visible home tab".
    const HOME_CONTAINER_SELECTOR = "#homeTab.is-active .homeSectionsContainer";
    const RETRY_DELAY_MS = 4000;
    const initializingContainers = new WeakSet();
    const initializedContainers = new WeakSet();

    const STYLE = `
        .personalRecommendationsSection { padding: 0 max(env(safe-area-inset-left), 3.3%) 1.8em; }
        .personalRecommendationsHeaderBar {
            display: flex; align-items: center; justify-content: space-between; padding: 0 0 0.5em;
        }
        .personalRecommendationsHeading {
            display: inline-flex; align-items: center; gap: 0.35em;
            background: none; border: none; color: inherit; cursor: pointer;
            font-size: 1.3em; font-weight: 600; padding: 0; margin: 0;
        }
        .personalRecommendationsHeading:hover { color: #00a4dc; }
        .personalRecommendationsHeading .material-icons { font-size: 0.8em; }
        .personalRecommendationsScrollButtons { display: flex; gap: 0.4em; flex: none; }
        .personalRecommendationsScrollButton {
            background: rgba(255, 255, 255, 0.08); border: none; color: inherit; cursor: pointer;
            width: 2.2em; height: 2.2em; border-radius: 50%; display: flex; align-items: center;
            justify-content: center; transition: background 0.15s ease;
        }
        .personalRecommendationsScrollButton:hover { background: rgba(255, 255, 255, 0.18); }
        .personalRecommendationsScrollButton[hidden] { display: none; }
        .personalRecommendationsRow {
            display: flex; gap: 1em; overflow-x: auto; overflow-y: hidden;
            scroll-behavior: smooth;
            /* pan-y, not pan-x/none: finger-drag no longer scrolls the row at all (that's the
               touch interaction that was triggering the Android black-screen bug, across
               several narrower attempts to fix it) - only the arrow buttons' scrollBy() can
               move it now. pan-y keeps vertical swipes over the row scrolling the page normally
               instead of being swallowed. overflow-x stays "auto" so scrollBy() still has a
               real scroll container to act on. */
            overscroll-behavior-x: contain; touch-action: pan-y;
            padding-bottom: 0.5em; -ms-overflow-style: none; scrollbar-width: none;
        }
        .personalRecommendationsRow::-webkit-scrollbar { display: none; }
        .personalRecommendationsCard {
            flex: 0 0 auto; width: 150px; text-decoration: none; color: inherit;
        }
        .personalRecommendationsPoster {
            width: 150px; height: 225px; border-radius: 0.2em; background: #202020; overflow: hidden;
            box-shadow: 0 1px 3px rgba(0,0,0,0.5); transition: transform 0.15s ease;
        }
        .personalRecommendationsCard:hover .personalRecommendationsPoster { transform: scale(1.04); }
        .personalRecommendationsPosterImage {
            width: 100%; height: 100%; object-fit: cover; display: block;
            opacity: 0; transition: opacity 0.2s ease;
        }
        .personalRecommendationsPosterImage.personalRecommendationsLoaded { opacity: 1; }
        .personalRecommendationsTitle {
            display: block; margin-top: 0.4em; font-size: 0.85em; white-space: nowrap;
            overflow: hidden; text-overflow: ellipsis;
        }
        .personalRecommendationsOverlay {
            position: fixed; inset: 0; background: rgba(0,0,0,0.75); z-index: 10000;
            display: flex; align-items: center; justify-content: center; padding: 2em 1em;
        }
        .personalRecommendationsOverlayPanel {
            background: #101010; border-radius: 0.4em; max-width: 1100px; width: 100%;
            max-height: 85vh; overflow-y: auto; padding: 1.5em;
        }
        .personalRecommendationsOverlayHeader {
            display: flex; align-items: center; justify-content: space-between; margin-bottom: 1em;
        }
        .personalRecommendationsOverlayHeader h2 { margin: 0; font-size: 1.3em; }
        .personalRecommendationsOverlayClose {
            background: none; border: none; color: inherit; cursor: pointer; font-size: 1.6em; line-height: 1;
        }
        .personalRecommendationsGrid {
            display: flex; flex-wrap: wrap; justify-content: center; gap: 1.2em;
        }
    `;

    function ensureStyle() {
        if (document.getElementById("personalRecommendationsStyle")) {
            return;
        }

        const style = document.createElement("style");
        style.id = "personalRecommendationsStyle";
        style.textContent = STYLE;
        document.head.appendChild(style);
    }

    function getBaseUrl() {
        try {
            return `${Emby.Page.baseUrl()}/`;
        } catch (e) {
            return "/";
        }
    }

    function posterUrl(item) {
        try {
            return ApiClient.getImageUrl(item.Id, { type: "Primary", maxHeight: 450, quality: 90 });
        } catch (e) {
            return "";
        }
    }

    function buildCard(item, baseUrl) {
        const a = document.createElement("a");
        a.className = "personalRecommendationsCard";
        a.href = `${baseUrl}#/details?id=${item.Id}`;
        a.addEventListener("click", (event) => {
            event.preventDefault();
            try {
                Emby.Page.showItem(item.Id);
            } catch (e) {
                window.location.href = a.href;
            }
        });

        const poster = document.createElement("div");
        poster.className = "personalRecommendationsPoster";
        const url = posterUrl(item);
        if (url) {
            // A real <img> (not a CSS background) so the browser can lazy-load and prioritize
            // it the same way it does for native rows - a background-image has no loading
            // attribute, so every card's image was being requested immediately regardless of
            // whether it was in view, competing for bandwidth and loading slower as a batch.
            const img = document.createElement("img");
            img.className = "personalRecommendationsPosterImage";
            img.loading = "lazy";
            img.decoding = "async";
            img.alt = "";
            img.addEventListener("load", () => img.classList.add("personalRecommendationsLoaded"), { once: true });
            img.src = url;
            poster.appendChild(img);
        }

        const title = document.createElement("span");
        title.className = "personalRecommendationsTitle";
        title.textContent = item.Name;

        a.appendChild(poster);
        a.appendChild(title);
        return a;
    }

    function openOverlay(items, heading, baseUrl) {
        const overlay = document.createElement("div");
        overlay.className = "personalRecommendationsOverlay";
        overlay.addEventListener("click", (event) => {
            if (event.target === overlay) {
                overlay.remove();
            }
        });

        const panel = document.createElement("div");
        panel.className = "personalRecommendationsOverlayPanel";

        const header = document.createElement("div");
        header.className = "personalRecommendationsOverlayHeader";
        const h2 = document.createElement("h2");
        h2.textContent = heading;
        const closeButton = document.createElement("button");
        closeButton.className = "personalRecommendationsOverlayClose";
        closeButton.setAttribute("aria-label", "Close");
        closeButton.textContent = "✕";
        closeButton.addEventListener("click", () => overlay.remove());
        header.appendChild(h2);
        header.appendChild(closeButton);

        const grid = document.createElement("div");
        grid.className = "personalRecommendationsGrid";
        items.forEach((item) => grid.appendChild(buildCard(item, baseUrl)));

        panel.appendChild(header);
        panel.appendChild(grid);
        overlay.appendChild(panel);
        document.body.appendChild(overlay);
    }

    function buildScrollButton(direction) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "personalRecommendationsScrollButton";
        button.setAttribute("aria-label", direction === "left" ? "Scroll left" : "Scroll right");
        button.innerHTML = `<span class="material-icons chevron_${direction}" aria-hidden="true"></span>`;
        return button;
    }

    function wireScrollButtons(row, scrollButtons, leftButton, rightButton) {
        // Deliberately NOT updated on the row's own "scroll" event: doing DOM writes (toggling
        // [hidden]) from a scroll handler means layout work happening in lockstep with touch-
        // driven momentum scrolling, which on some Android WebView versions causes exactly the
        // rendering stalls (briefly-black content) this was built to avoid. Visibility is only
        // ever computed at render time and on resize - both rare, neither during an active
        // scroll gesture. The trade-off: an arrow can stay visible after scrolling all the way
        // to that end; clicking it then is a harmless no-op (nothing left to scroll to).
        const updateOverflow = () => {
            scrollButtons.hidden = row.scrollWidth <= row.clientWidth + 1;
        };

        leftButton.addEventListener("click", () => row.scrollBy({ left: -row.clientWidth * 0.9, behavior: "smooth" }));
        rightButton.addEventListener("click", () => row.scrollBy({ left: row.clientWidth * 0.9, behavior: "smooth" }));
        window.addEventListener("resize", updateOverflow);
        updateOverflow();
    }

    function renderSection(container, items, heading, baseUrl) {
        const section = document.createElement("div");
        section.className = "personalRecommendationsSection";

        const headerBar = document.createElement("div");
        headerBar.className = "personalRecommendationsHeaderBar";

        const headingButton = document.createElement("button");
        headingButton.type = "button";
        headingButton.className = "personalRecommendationsHeading";
        headingButton.innerHTML = `${heading} <span class="material-icons chevron_right" aria-hidden="true"></span>`;
        headingButton.addEventListener("click", () => openOverlay(items, heading, baseUrl));

        const scrollButtons = document.createElement("div");
        scrollButtons.className = "personalRecommendationsScrollButtons";
        const leftButton = buildScrollButton("left");
        const rightButton = buildScrollButton("right");
        scrollButtons.appendChild(leftButton);
        scrollButtons.appendChild(rightButton);

        headerBar.appendChild(headingButton);
        headerBar.appendChild(scrollButtons);

        const row = document.createElement("div");
        row.className = "personalRecommendationsRow";
        items.forEach((item) => row.appendChild(buildCard(item, baseUrl)));

        section.appendChild(headerBar);
        section.appendChild(row);
        container.prepend(section);

        wireScrollButtons(row, scrollButtons, leftButton, rightButton);
    }

    async function setup() {
        const containers = Array.from(document.querySelectorAll(HOME_CONTAINER_SELECTOR)).filter((element) => {
            if (element.querySelector(":scope > .personalRecommendationsSection")) {
                initializedContainers.add(element);
                return false;
            }

            if (initializingContainers.has(element) || initializedContainers.has(element)) {
                return false;
            }

            initializingContainers.add(element);
            return true;
        });

        if (!containers.length) {
            return;
        }

        let config;
        try {
            config = await ApiClient.getPluginConfiguration(PLUGIN_ID);
        } catch (e) {
            containers.forEach((c) => initializingContainers.delete(c));
            return;
        }

        if (!config || config.Enabled === false || config.HomeScreenWidgetEnabled === false) {
            containers.forEach((c) => initializingContainers.delete(c));
            return;
        }

        const userId = ApiClient.getCurrentUserId();
        let items;
        try {
            items = await ApiClient.getJSON(ApiClient.getUrl(`Recommendations/${userId}`));
        } catch (e) {
            containers.forEach((c) => initializingContainers.delete(c));
            return;
        }

        containers.forEach((c) => initializingContainers.delete(c));

        if (!items || !items.length) {
            // Likely a cache miss on the server (e.g. shortly after a restart): it returns an
            // empty list immediately and warms the cache in the background. Retry once shortly
            // after instead of waiting for an incidental DOM mutation to trigger another check.
            window.setTimeout(scheduleSetup, RETRY_DELAY_MS);
            return;
        }

        ensureStyle();
        const baseUrl = getBaseUrl();
        const heading = config.WidgetHeading || "Recommended For You";

        for (const element of containers) {
            if (!element.isConnected || !element.matches(HOME_CONTAINER_SELECTOR)) {
                continue;
            }

            if (element.querySelector(":scope > .personalRecommendationsSection")) {
                initializedContainers.add(element);
                continue;
            }

            renderSection(element, items, heading, baseUrl);
            initializedContainers.add(element);
        }
    }

    let setupScheduled = false;
    function scheduleSetup() {
        if (setupScheduled) {
            return;
        }

        setupScheduled = true;
        window.requestAnimationFrame(() => {
            setupScheduled = false;
            setup();
        });
    }

    function initialize() {
        const target = document.body;
        if (!target) {
            return;
        }

        // Deliberately broad: reacting only to mutations of specific nodes/attributes missed
        // the actual moment .homeSectionsContainer appears on some builds (it's added, or gains
        // its class, deeper in a React-rendered subtree, after #homeTab itself already exists).
        // scheduleSetup() is requestAnimationFrame-debounced and setup() is cheap when nothing's
        // new, so reacting to any DOM/class churn in body is safe and far more reliable than
        // trying to guess which mutation matters.
        const observer = new MutationObserver(() => scheduleSetup());

        observer.observe(target, {
            attributes: true,
            attributeFilter: ["class"],
            childList: true,
            subtree: true
        });

        scheduleSetup();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
