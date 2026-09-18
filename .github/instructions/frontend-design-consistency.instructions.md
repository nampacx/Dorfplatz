---
description: "Use when styling or editing UI components in frontend/src — buttons, forms, cards, badges, layout. Enforces visual consistency across the app."
applyTo: "frontend/src/**/*.tsx, frontend/src/**/*.css"
---
# Frontend Design Consistency

- This app uses a shared design system defined in [frontend/src/styles.css](../../frontend/src/styles.css) (CSS variables for color, spacing `--sp-*`, font size `--fs-*`, radius, shadows) and fixed breakpoints (`600px`, `900px`, `1200px`).
- When you change the style of one instance of a UI element (e.g. a button, card, or badge) for a reason like contrast, spacing, or accessibility, check for other instances of the same element type across the app and apply the same fix — don't leave the app in a visually inconsistent state.
- Reuse existing CSS variables instead of introducing new hardcoded colors/spacing/font-sizes. Add a new variable to `:root` only if the value is genuinely reusable.
- Reuse the existing breakpoints instead of adding new ones.
- Check that layout changes don't cause clipping or overlap: dynamic/long text should wrap or truncate, flex/grid children should not overflow their parent, and overlays (modals, sheets, tab bar) should be checked at each breakpoint.
