# Reusable Components Reference

## 1. BottomNav (`components/BottomNav.jsx`)

### Purpose
Global bottom tab navigation with 4 tabs.

### Props
None (reads location from router).

### Tabs
| Tab | Route | Icon |
|---|---|---|
| Home | `/dashboard` | House SVG |
| Training | `/training` | Book SVG |
| Progress | `/progress` | Bar chart SVG |
| Settings | `/settings` | Gear SVG |

### Visibility Rules
- Hidden on: `/splash`, `/login`, `/register`
- Hidden when path matches: `/training/:id/learn`, `/training/:id/ar`, `/training/:id/quiz`

### Styling
- Fixed to bottom, max-width 430px
- Active tab: `#FF6D00` (orange)
- Inactive tab: `#707070`
- Uses `env(safe-area-inset-bottom)` for safe area

---

## 2. Header (`components/Header.jsx`)

### Purpose
Sticky page header with optional back button and right action.

### Props
| Prop | Type | Default | Description |
|---|---|---|---|
| `title` | string | - | Centered title text |
| `showBack` | boolean | `false` | Shows back arrow button |
| `rightAction` | ReactNode | `null` | Custom right-side content |

### Features
- Back button calls `navigate(-1)`
- Background: `#121212`
- Title: 18px, weight 700, white

---

## 3. ModuleCard (`components/ModuleCard.jsx`)

### Purpose
Training module card with icon, title, meta, status badge, and progress bar.

### Props
| Prop | Type | Description |
|---|---|---|
| `module` | object | Module data with icon, title, duration, difficulty, status, progress, bestScore |
| `onClick` | function | Click handler |

### Features
- Status badge colors: completed=`#4CAF50`, in_progress=`#FF6D00`, not_started=`#707070`, locked=`#555`
- Locked: opacity 0.5, cursor not-allowed, no progress bar
- Shows "Best: X%" if bestScore > 0

---

## 4. ProgressBar (`components/ProgressBar.jsx`)

### Purpose
Animated horizontal progress bar.

### Props
| Prop | Type | Default | Description |
|---|---|---|---|
| `value` | number | `0` | Progress value (0-100) |
| `height` | number | `8` | Bar height in px |
| `color` | string | `#FF6D00` | Fill color |
| `showLabel` | boolean | `false` | Shows percentage text below |

### Features
- Animated fill with 0.6s ease transition
- Green `#4CAF50` when value >= 100
- Clamped to 0-100 range
- Track: `#2A2A2A`

---

## 5. ScoreBar (`components/ScoreBar.jsx`)

### Purpose
Labeled horizontal score bar with percentage display.

### Props
| Prop | Type | Default | Description |
|---|---|---|---|
| `label` | string | - | Category name |
| `value` | number | - | Score value |
| `maxValue` | number | `100` | Maximum value |
| `color` | string | `#FF6D00` | Bar color |

### Color Logic
- Green `#4CAF50`: value >= 80%
- Orange `#FF6D00`: value >= 60%
- Red `#F44336`: value < 60%

### Features
- Shows "XX%" text above bar
- 6px tall track with animated fill
