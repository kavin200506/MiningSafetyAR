# TrainingCataloguePage (`/training`)

## Purpose
Filterable list of all training modules with status filter buttons.

## Route
`/training`

## File
`src/pages/TrainingCataloguePage.jsx` (106 lines)

## Imports
- `useState` from React
- `useNavigate` from react-router-dom
- `modules` from data
- `ModuleCard` component
- `Header` component

## Constants
```js
filters = [
  { key: 'all', label: 'All' },
  { key: 'completed', label: 'Completed' },
  { key: 'in_progress', label: 'In Progress' },
  { key: 'not_started', label: 'Not Started' },
]
```

## State
| Variable | Type | Initial | Description |
|---|---|---|---|
| `activeFilter` | string | `'all'` | Currently selected filter |

## Computed Values
| Variable | Logic |
|---|---|
| `filtered` | If `activeFilter === 'all'` -> all modules; else `modules.filter(m => m.status === activeFilter)` |

## UI Elements (Top to Bottom)

### 1. Container
- Height: `100vh`, background: `#121212`

### 2. Header Component
- Props: `title="Training Modules"`

### 3. Scroll Area
- Height: `calc(100vh - 56px)` (below header)
- OverflowY: auto, padding: 0 16px

### 4. Filter Row
- Flex row, gap 6px, margin bottom 12px
- OverflowX: auto (horizontal scroll if needed)
- **4 filter pill buttons:**
  - Each: padding 6px 14px, border radius 8px, font 12px, weight 600
  - **Active:** bg `#FF6D00`, text white, border `#FF6D00`
  - **Inactive:** bg `#262626`, text `#B0B0B0`, border `1px solid #333`
  - White space: nowrap, flex shrink 0

### 5. Module Count
- Text: "5 modules" (or "1 module" for singular)
- Font: 12px, `#707070`, margin bottom 10px

### 6. Module List
- Maps over `filtered` array
- Renders `ModuleCard` component for each
- Props: `module={mod}`, `onClick={() => navigate(/training/${mod.id})}`

### 7. Empty State (when `filtered.length === 0`)
- Centered flex column, padding 60px 0
- Pushpin emoji 📌 (32px)
- "No modules found" -- `#707070`, margin top 8px

### 8. Bottom Spacer
- Height: 80px (BottomNav clearance)

## Navigation
- ModuleCard click -> `/training/${mod.id}`

## Filter Behavior
| Filter | Modules Shown |
|---|---|
| All | All 5 modules (including locked) |
| Completed | fire_safety, gas_safety |
| In Progress | machinery_safety |
| Not Started | electrical_safety |
| (locked not shown in any filter except All) | |

## Visual Layout
```
┌─────────────────────────┐
│   Training Modules      │  (Header component)
│─────────────────────────│
│ [All][Completed][In P..]│  (filter pills, All is orange)
│                         │
│ 5 modules               │
│                         │
│ ┌─────────────────────┐ │
│ │ 🔥 Fire & Explosion │ │  (ModuleCard)
│ │    45 min · Medium  │ │
│ │    ✅ Completed     │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ ☣️ Gas Leak & Conf. │ │
│ │    50 min · Hard    │ │
│ │    ✅ Completed     │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ ⚙️ Machinery Safety │ │
│ │    40 min · Medium  │ │
│ │    ▶️ In Progress   │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ ⚡ Electrical Safety│ │
│ │    35 min · Medium  │ │
│ │    ○ Not Started    │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ 🏔️ Working at Heig.│ │
│ │    40 min · Hard    │ │
│ │    🔒 Locked        │ │
│ └─────────────────────┘ │
│                         │
│   [Bottom Nav Bar]      │
└─────────────────────────┘
```
