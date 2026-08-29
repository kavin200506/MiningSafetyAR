# Data Layer Reference

## 1. Worker Data (`data/worker.js`)

### Single Worker Object
```js
{
  id: 'JH10293',
  name: 'Ramesh Kumar',
  pin: '1234',
  organization: 'Jharkhand Steel Works',
  sector: 'Steel Manufacturing',
  phone: '9876543210',
  language: 'English',
  joinDate: '2026-01-15',
  overallProgress: 68,
  certificatesEarned: 2,
  totalAttempts: 7,
  competencyScores: {
    hazardRecognition: 82,
    ppeSelection: 65,
    evacuation: 78,
    emergencyResponse: 71
  },
  attempts: [
    { module: 'fire_safety', date: '2026-08-26', score: 85, passed: true, attempt: 2 },
    { module: 'fire_safety', date: '2026-08-25', score: 60, passed: false, attempt: 1 },
    { module: 'gas_safety', date: '2026-08-28', score: 72, passed: true, attempt: 2 },
    { module: 'gas_safety', date: '2026-08-27', score: 55, passed: false, attempt: 1 },
    { module: 'machinery_safety', date: '2026-08-29', score: 60, passed: true, attempt: 1 }
  ]
}
```

---

## 2. Modules Data (`data/modules.js`)

### 5 Training Modules

| ID | Title | Icon | Domain | Duration | Difficulty | Status | Progress | Best Score | Certificate ID |
|---|---|---|---|---|---|---|---|---|---|
| `fire_safety` | Fire & Explosion Response | 🔥 | Fire Safety | 45 min | Medium | completed | 100% | 85% | JH-FIRE-001928 |
| `gas_safety` | Gas Leak & Confined Space | ☣️ | Chemical Safety | 50 min | Hard | completed | 100% | 72% | JH-GAS-002156 |
| `machinery_safety` | Machinery Safety | ⚙️ | Equipment Safety | 40 min | Medium | in_progress | 45% | 60% | null |
| `electrical_safety` | Electrical Safety | ⚡ | Electrical Safety | 35 min | Medium | not_started | 0% | 0% | null |
| `heights_safety` | Working at Heights | 🏔️ | Fall Protection | 40 min | Hard | locked | 0% | 0% | null |

### Module Object Structure
```js
{
  id: 'fire_safety',
  title: 'Fire & Explosion Response',
  icon: '🔥',
  domain: 'Fire Safety',
  duration: '45 min',
  difficulty: 'Medium',
  status: 'completed',  // completed | in_progress | not_started | locked
  progress: 100,
  bestScore: 85,
  attempts: 2,
  lastAttempt: '2026-08-26',
  certificateId: 'JH-FIRE-001928',
  color: '#FF6D00',
  description: 'Fire safety training covering...',
  objectives: [
    'Identify different classes of fires',
    'Use fire extinguisher using P.A.S.S. technique',
    'Execute proper evacuation procedures',
    'Recognize fire hazards in industrial settings',
    'Respond effectively to fire emergencies'
  ],
  competencyScores: {
    hazardRecognition: 85,
    extinguisherUse: 80,  // or ppeSelection for other modules
    evacuation: 78,
    emergencyResponse: 75
  }
}
```

### Exported Functions
- `getModuleById(id)` -- returns single module object

---

## 3. Questions Data (`data/questions.js`)

### 22 Total Questions Across 5 Modules

#### fire_safety (5 questions)
| # | Question | Correct | Competency |
|---|---|---|---|
| 1 | What does P.A.S.S. stand for? | A | extinguisherUse |
| 2 | Safe distance from fire? | C | hazardRecognition |
| 3 | Three elements of fire triangle? | B | hazardRecognition |
| 4 | Where to aim extinguisher nozzle? | A | extinguisherUse |
| 5 | Best way to move through smoke? | C | evacuation |

#### gas_safety (5 questions)
| # | Question | Correct | Competency |
|---|---|---|---|
| 1 | First response to gas leak? | A | emergencyResponse |
| 2 | Essential PPE for gas hazards? | C | ppeSelection |
| 3 | Buddy system requirement? | B | emergencyResponse |
| 4 | Odorless gases danger? | A | hazardRecognition |
| 5 | Pre-entry testing order? | D | hazardRecognition |

#### machinery_safety (4 questions)
| # | Question | Correct | Competency |
|---|---|---|---|
| 1 | LOTO first step? | A | hazardRecognition |
| 2 | Before maintenance? | B | hazardRecognition |
| 3 | Machine guards purpose? | A | hazardRecognition |
| 4 | Unguarded machines? | C | hazardRecognition |

#### electrical_safety (4 questions)
| # | Question | Correct | Competency |
|---|---|---|---|
| 1 | Most common cause? | B | hazardRecognition |
| 2 | Electrical PPE? | C | ppeSelection |
| 3 | Grounding purpose? | A | hazardRecognition |
| 4 | Electrocution response? | D | emergencyResponse |

#### heights_safety (4 questions)
| # | Question | Correct | Competency |
|---|---|---|---|
| 1 | Fall protection height? | B | hazardRecognition |
| 2 | Harness inspection? | A | hazardRecognition |
| 3 | Anchor point strength? | C | hazardRecognition |
| 4 | Scaffold safety? | A | hazardRecognition |

### Question Object Structure
```js
{
  id: 'fire_q1',
  text: 'What does P.A.S.S. stand for in fire extinguisher use?',
  options: [
    'Pull, Aim, Squeeze, Sweep',
    'Push, Aim, Squeeze, Sweep',
    'Pull, Aim, Spray, Sweep',
    'Pull, Align, Squeeze, Sweep'
  ],
  correct: 0,  // index of correct option (0-3)
  competency: 'extinguisherUse'
}
```

### Exported Functions
- `getQuestionsForModule(id)` -- returns array of questions for a module

---

## 4. Certificates Data (`data/certificates.js`)

### 2 Certificates

| ID | Worker | Worker ID | Module | Score | Issued | Expires | Status |
|---|---|---|---|---|---|---|---|
| JH-FIRE-001928 | Ramesh Kumar | JH10293 | Fire & Explosion Response | 85% | 2026-08-26 | 2027-08-26 | valid |
| JH-GAS-002156 | Ramesh Kumar | JH10293 | Gas Leak & Confined Space | 72% | 2026-08-28 | 2027-08-28 | valid |

### Certificate Object Structure
```js
{
  id: 'JH-FIRE-001928',
  workerName: 'Ramesh Kumar',
  workerId: 'JH10293',
  moduleId: 'fire_safety',
  moduleTitle: 'Fire & Explosion Response',
  score: 85,
  issuedDate: '2026-08-26',
  expiryDate: '2027-08-26',
  organization: 'Jharkhand Steel Works',
  status: 'valid'
}
```

### Exported Functions
- `getCertificateById(id)` -- returns single certificate
- `getCertificatesByWorker(workerId)` -- returns array of certificates for a worker
