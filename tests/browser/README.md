# Browser verification

The first customer preview was checked through the in-app browser. See `docs/reports/help-center-preview.md` for exact scope and results. Playwright automation for full chat/ticket/auth journeys remains planned; those features are not implemented.

To reproduce the responsive fixture browser check while API and Vite are running, copy `tests/browser/responsive-check.html` to `src/haven-support-web/layout-check.html`, visit `http://127.0.0.1:5173/layout-check.html`, and inspect the rendered measurements above each iframe. Remove only that copied harness afterward. It is not part of the shipped application. The harness checks five viewport widths with real API data. Decorative element bounds may extend beyond their clipped container; compare document scroll width against viewport width to detect page overflow.

Manual journey: search `return shipping`; confirm one Returns guide; open it and inspect RET-02; Escape returns focus; search an absent term and recover through Browse all policies; save a policy, open Saved policies through navigation, reload and verify the bookmark persists; remove it and inspect the empty state. Use Tab/Shift+Tab/Escape on source dialogs and mobile navigation. Test network failure/retry and blocked clipboard/storage separately before release.
