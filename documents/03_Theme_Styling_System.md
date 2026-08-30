# 03 — Theme & Styling System

## Light Theme CSS Variables (Theme.uss)

```uss
/* ========================================
   THEME.uss — MiningSafetyAR Light Theme
   White bg, orange accent (#FF6D00)
   ======================================== */

:root {
    /* Backgrounds */
    --bg-primary: #FFFFFF;
    --bg-secondary: #F8F8F8;
    --bg-card: #F5F5F5;
    --bg-input: #F0F0F0;

    /* Accent Colors */
    --accent: #FF6D00;
    --accent-light: #FF9E40;
    --accent-dark: #E65100;

    /* Semantic Colors */
    --success: #4CAF50;
    --danger: #F44336;
    --warning: #FFC107;

    /* Text Colors */
    --text-primary: #1A1A1A;
    --text-secondary: #666666;
    --text-muted: #999999;
    --text-disabled: #CCCCCC;

    /* Border Colors */
    --border: #E0E0E0;
    --border-light: #EEEEEE;
    --border-accent: #FF6D0033;

    /* Shadows */
    --shadow-card: 0 1px 3px rgba(0, 0, 0, 0.08);
    --shadow-elevated: 0 4px 12px rgba(0, 0, 0, 0.1);

    /* Typography */
    --font-family: "Inter", -apple-system, sans-serif;
    --font-size-xs: 10px;
    --font-size-sm: 11px;
    --font-size-base: 13px;
    --font-size-md: 14px;
    --font-size-lg: 16px;
    --font-size-xl: 18px;
    --font-size-2xl: 22px;
    --font-size-3xl: 36px;

    --font-weight-regular: 400;
    --font-weight-medium: 500;
    --font-weight-semibold: 600;
    --font-weight-bold: 700;
    --font-weight-extrabold: 800;

    /* Spacing */
    --space-xs: 2px;
    --space-sm: 4px;
    --space-md: 8px;
    --space-lg: 12px;
    --space-xl: 16px;
    --space-2xl: 20px;
    --space-3xl: 24px;
    --space-4xl: 32px;

    /* Border Radius */
    --radius-sm: 6px;
    --radius-md: 8px;
    --radius-lg: 10px;
    --radius-xl: 12px;
    --radius-2xl: 14px;
    --radius-full: 9999px;

    /* Layout */
    --max-width: 430px;
    --header-height: 56px;
    --bottom-nav-height: 64px;
    --safe-area-bottom: 0px;
}
```

## Reset (Reset.uss)

```uss
Label, Button, TextField, VisualElement {
    margin: 0;
    padding: 0;
}

Button {
    background-color: transparent;
    border-width: 0;
    border-radius: 0;
    color: var(--text-primary);
    font-family: var(--font-family);
    cursor: pointer;
}

Label {
    color: var(--text-primary);
    font-family: var(--font-family);
    font-size: var(--font-size-base);
    white-space: pre-wrap;
}

TextField {
    background-color: var(--bg-input);
    border-color: var(--border);
    border-width: 1px;
    border-radius: var(--radius-md);
    color: var(--text-primary);
    font-family: var(--font-family);
    font-size: var(--font-size-md);
    padding: 12px 14px;
}

TextField:focus {
    border-color: var(--accent);
}
```

## Component Styles (Components.uss)

```uss
/* --- Primary Button --- */
.btn-primary {
    background-color: var(--accent);
    color: #FFFFFF;
    border-width: 0;
    border-radius: var(--radius-md);
    height: 48px;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-semibold);
    transition: background-color 0.2s ease, scale 0.15s ease;
}

.btn-primary:hover {
    background-color: var(--accent-light);
}

.btn-primary:active {
    scale: 0.97;
    opacity: 0.9;
}

/* --- Secondary Button --- */
.btn-secondary {
    background-color: transparent;
    color: var(--accent);
    border-color: var(--accent);
    border-width: 1px;
    border-radius: var(--radius-md);
    height: 48px;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-semibold);
    transition: background-color 0.2s ease, scale 0.15s ease;
}

.btn-secondary:hover {
    background-color: #FF6D0008;
}

.btn-secondary:active {
    scale: 0.97;
}

/* --- Danger Button --- */
.btn-danger {
    background-color: transparent;
    color: var(--danger);
    border-color: var(--danger);
    border-width: 1px;
    border-radius: var(--radius-md);
    height: 48px;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-semibold);
}

.btn-danger:active {
    scale: 0.97;
}

/* --- Text Button --- */
.btn-text {
    background-color: transparent;
    border-width: 0;
    color: var(--accent);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-semibold);
}

/* --- Cards --- */
.card {
    background-color: var(--bg-card);
    border-color: var(--border);
    border-width: 1px;
    border-radius: var(--radius-2xl);
    padding: var(--space-xl);
    box-shadow: var(--shadow-card);
}

.card-interactive {
    background-color: var(--bg-card);
    border-color: var(--border);
    border-width: 1px;
    border-radius: var(--radius-xl);
    padding: var(--space-lg);
    transition: scale 0.15s ease, box-shadow 0.15s ease;
    cursor: pointer;
}

.card-interactive:active {
    scale: 0.98;
    box-shadow: var(--shadow-elevated);
}

.card-accent {
    background-color: #FFF3E0;
    border-color: var(--border-accent);
    border-width: 1px;
    border-radius: var(--radius-2xl);
    padding: var(--space-xl);
}

/* --- Badges --- */
.badge {
    border-radius: var(--radius-full);
    padding: 4px 10px;
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-semibold);
}

.badge--completed { background-color: #E8F5E9; color: #2E7D32; }
.badge--in_progress { background-color: #FFF3E0; color: var(--accent); }
.badge--not_started { background-color: #F5F5F5; color: var(--text-muted); }
.badge--locked { background-color: #EEEEEE; color: var(--text-disabled); }
.badge--pass { background-color: #E8F5E9; color: #2E7D32; }
.badge--fail { background-color: #FFEBEE; color: #C62828; }

/* --- Pills --- */
.pill {
    background-color: var(--bg-input);
    border-radius: var(--radius-full);
    padding: 4px 10px;
    font-size: var(--font-size-sm);
    color: var(--text-secondary);
}

/* --- Dividers --- */
.divider {
    height: 1px;
    background-color: var(--border);
    margin: var(--space-xl) 0;
}

/* --- Page Container --- */
.page-container {
    flex: 1;
    background-color: var(--bg-primary);
    flex-direction: column;
    max-width: var(--max-width);
    align-self: center;
    width: 100%;
}

.scroll-area {
    flex: 1;
    overflow-y: auto;
    padding: var(--space-xl);
}

.section-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--space-lg);
}

.section-title {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-bold);
    color: var(--text-primary);
}
```

## Form Styles (Forms.uss)

```uss
.input-field {
    background-color: var(--bg-input);
    border-color: var(--border);
    border-width: 1px;
    border-radius: var(--radius-md);
    color: var(--text-primary);
    font-size: var(--font-size-md);
    padding: 12px 14px;
    height: 48px;
}

.input-field:focus {
    border-color: var(--accent);
}

.form-group {
    margin-bottom: var(--space-xl);
}

.form-label {
    font-size: var(--font-size-sm);
    color: var(--text-secondary);
    margin-bottom: var(--space-sm);
    font-weight: var(--font-weight-medium);
}

/* Toggle Switch */
.toggle-switch {
    width: 48px;
    height: 28px;
    border-radius: 14px;
    background-color: var(--border);
    padding: 2px;
    cursor: pointer;
    transition: background-color 0.2s ease;
}

.toggle-switch--on {
    background-color: var(--accent);
}

.toggle-switch__knob {
    width: 24px;
    height: 24px;
    border-radius: 12px;
    background-color: #FFFFFF;
    transition: translate 0.2s ease;
}

.toggle-switch--on .toggle-switch__knob {
    translate: 20px 0;
}
```

## Bottom Nav (BottomNav.uss)

```uss
.bottom-nav {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: var(--bottom-nav-height);
    background-color: #FFFFFF;
    border-top-color: var(--border);
    border-top-width: 1px;
    flex-direction: row;
    justify-content: space-around;
    align-items: center;
    padding-bottom: var(--safe-area-bottom);
    max-width: var(--max-width);
    align-self: center;
    width: 100%;
}

.bottom-nav__tab {
    flex-direction: column;
    align-items: center;
    justify-content: center;
    flex: 1;
    height: 100%;
    cursor: pointer;
}

.bottom-nav__tab-icon {
    width: 24px;
    height: 24px;
    margin-bottom: 2px;
    --unity-image-tint-color: var(--text-muted);
}

.bottom-nav__tab-label {
    font-size: 10px;
    color: var(--text-muted);
}

.bottom-nav__tab--active .bottom-nav__tab-icon {
    --unity-image-tint-color: var(--accent);
}

.bottom-nav__tab--active .bottom-nav__tab-label {
    color: var(--accent);
    font-weight: var(--font-weight-semibold);
}
```

## Header (Header.uss)

```uss
.header {
    flex-direction: row;
    align-items: center;
    height: var(--header-height);
    background-color: #FFFFFF;
    padding: 0 var(--space-xl);
    border-bottom-color: var(--border-light);
    border-bottom-width: 1px;
}

.header__back-btn {
    width: 40px;
    height: 40px;
    justify-content: center;
    align-items: center;
    background-color: transparent;
    border-width: 0;
    cursor: pointer;
}

.header__title {
    flex: 1;
    text-align: center;
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-bold);
    color: var(--text-primary);
}

.header__right-action {
    width: 40px;
    height: 40px;
}
```

## Tables (Tables.uss)

```uss
.table {
    width: 100%;
    border-radius: var(--radius-xl);
    overflow: hidden;
    background-color: #FFFFFF;
    border-color: var(--border);
    border-width: 1px;
    box-shadow: var(--shadow-card);
}

.table-header {
    flex-direction: row;
    padding: var(--space-md) var(--space-lg);
    background-color: var(--bg-card);
    border-bottom-color: var(--border);
    border-bottom-width: 1px;
}

.table-header__cell {
    font-size: var(--font-size-sm);
    color: var(--text-muted);
    font-weight: var(--font-weight-semibold);
}

.table-row {
    flex-direction: row;
    padding: var(--space-md) var(--space-lg);
    border-bottom-color: var(--border-light);
    border-bottom-width: 1px;
    align-items: center;
}

.table-cell {
    font-size: var(--font-size-base);
    color: var(--text-primary);
}

.table-cell--accent { color: var(--accent); }
.table-cell--success { color: var(--success); }
.table-cell--danger { color: var(--danger); }
```

## Filter Pills

```uss
.filter-row {
    flex-direction: row;
    gap: 8px;
    padding: 0 var(--space-xl);
    margin-bottom: var(--space-lg);
}

.filter-pill {
    background-color: var(--bg-card);
    border-color: var(--border);
    border-width: 1px;
    border-radius: var(--radius-full);
    padding: 6px 14px;
    font-size: var(--font-size-sm);
    color: var(--text-secondary);
    cursor: pointer;
    transition: background-color 0.2s ease, color 0.2s ease;
}

.filter-pill--active {
    background-color: var(--accent);
    border-color: var(--accent);
    color: #FFFFFF;
}
```

## Animations (Animations.uss)

```uss
.progress-fill {
    transition: width 0.6s ease;
}

.score-fill {
    transition: width 0.6s ease 0.2s;
}

.pressable {
    transition: scale 0.15s ease, opacity 0.15s ease;
}

.pressable:active {
    scale: 0.97;
    opacity: 0.9;
}

.timer-green { color: var(--success); transition: color 0.3s ease; }
.timer-yellow { color: var(--warning); transition: color 0.3s ease; }
.timer-red { color: var(--danger); transition: color 0.3s ease; }

@keyframes spin {
    0% { rotate: 0deg; }
    100% { rotate: 360deg; }
}

.spinner {
    width: 24px;
    height: 24px;
    border-color: var(--accent);
    border-width: 2px;
    border-style: solid;
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin 1s linear infinite;
}
```
