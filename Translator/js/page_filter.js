// Hides the parts of a dictionary page named in its engine's "Hide" selectors (appsettings.json).
// MainWindow registers this script to run in every new document before the page's own scripts.
// Everything sits inside a function, so nothing here can clash with names the page declares.
(() => {
    // PageFilterScript replaces the placeholder below with the filters, keyed by host without "www.":
    // {"oxfordlearnersdictionaries.com": {"engine": "Oxford", "hide": ["#ox-header", ...]}, ...}
    const filters = /* filters */ {};

    if (window !== window.top) {
        return;
    }

    const host = location.hostname;
    const domain = Object.keys(filters).find(d => host === d || host.endsWith("." + d));
    if (domain === undefined) {
        return;
    }

    const { engine, hide } = filters[domain];

    const report = (selector, reason) => {
        try {
            window.chrome.webview.postMessage({ type: "page-filter", engine, selector, reason });
        } catch {
            // Outside WebView2 there is nobody to report to.
        }
    };

    // querySelector throws on invalid CSS, while a style sheet would silently drop the rule.
    const fragment = document.createDocumentFragment();
    const valid = hide.filter(selector => {
        try {
            fragment.querySelector(selector);
            return true;
        } catch {
            report(selector, "invalid-selector");
            return false;
        }
    });
    if (valid.length === 0) {
        return;
    }

    // One rule per selector. adoptedStyleSheets works before <head> exists, so the hidden parts are
    // never drawn, and the rules also cover elements the site adds later. Nothing is removed from
    // the page, so the site's own scripts still find their nodes.
    const sheet = new CSSStyleSheet();
    sheet.replaceSync(valid.map(selector => `${selector} { display: none !important; }`).join("\n"));
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];

    // A selector that matches nothing usually means the site has changed.
    window.addEventListener("load", () => {
        for (const selector of valid) {
            if (document.querySelector(selector) === null) {
                report(selector, "no-match");
            }
        }
    }, { once: true });
})();
