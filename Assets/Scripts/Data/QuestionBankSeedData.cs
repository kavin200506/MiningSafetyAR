using System.Collections.Generic;

namespace MiningSafetyAR.Data
{
    /// <summary>
    /// fire_safety content is real (not placeholder): grounded in the standard P.A.S.S. technique
    /// (Pull, Aim, Squeeze, Sweep) and general evacuation/alarm practice, and deliberately kept
    /// consistent with this app's OWN drill mechanics — e.g. the "3.5 feet" safe-distance answer
    /// matches ARProximitySafetyValidator's actual enforced threshold (safeDistanceThreshold =
    /// 1.0668m = exactly 3.5ft), not an arbitrary number. IMPORTANT CAVEAT: this is accurate,
    /// internally-consistent safety content, not formally certified/reviewed by a qualified mine
    /// safety officer (e.g. DGMS) — that sign-off still needs to happen before treating this as
    /// production-grade training material. gas_safety content below is still [PLACEHOLDER] — that
    /// module has no AR scene yet, so there was nothing concrete to write real questions against.
    /// </summary>
    public static class QuestionBankSeedData
    {
        public static List<(string moduleId, string submoduleId, QuestionBankItem item)> GetSeedQuestions()
        {
            var seeds = new List<(string, string, QuestionBankItem)>();

            QuestionBankItem Fire(string id, string tag, QuestionDifficulty diff, string stem, string[] options, int correct, string explanation) =>
                new QuestionBankItem
                {
                    id = id,
                    tags = string.IsNullOrEmpty(tag) ? System.Array.Empty<string>() : new[] { tag },
                    difficulty = diff,
                    stemEN = stem,
                    optionsEN = options,
                    correctIndex = correct,
                    explanationEN = explanation,
                    stemHI = "", optionsHI = System.Array.Empty<string>(), explanationHI = "",
                    stemSAT = "", optionsSAT = System.Array.Empty<string>(), explanationSAT = "",
                    stemTA = "", optionsTA = System.Array.Empty<string>(), explanationTA = "",
                };

            // ---------------- fire_safety / main — unsafe_proximity_to_fire ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_proximity_1", MistakeTags.UnsafeProximityToFire, QuestionDifficulty.Easy,
                "What is the minimum safe distance you should keep from an active fire while operating a fire extinguisher?",
                new[] { "3.5 feet", "8-10 feet", "1 foot", "There is no safe minimum distance" }, 0,
                "3.5 feet is the safe distance enforced in this drill. Standing closer risks burns and can block your escape route if the fire flares up.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_proximity_2", MistakeTags.UnsafeProximityToFire, QuestionDifficulty.Medium,
                "Why is maintaining a safe distance from a fire important, even while actively extinguishing it?",
                new[] { "It protects you from heat and keeps an escape route open if the fire grows", "It makes the extinguisher spray travel further", "It is only a legal requirement for large fires", "Distance does not matter if you act quickly" }, 0,
                "Safe distance is a physical safety margin, not a formality — it protects you from sudden flare-ups and keeps your retreat path clear.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_proximity_3", MistakeTags.UnsafeProximityToFire, QuestionDifficulty.Medium,
                "A fire flares up unexpectedly while you are extinguishing it. What should you do first?",
                new[] { "Step back to a safe distance immediately", "Spray faster and move closer", "Ignore it and keep your position", "Wait for it to die down on its own" }, 0,
                "Re-establishing safe distance is the immediate priority whenever a fire's behavior becomes unpredictable.")));

            // ---------------- fire_safety / main — extinguisher_depleted_before_out ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_extinguisher_depleted_1", MistakeTags.ExtinguisherDepletedBeforeOut, QuestionDifficulty.Medium,
                "Your extinguisher runs out before the fire is fully out. What should you do?",
                new[] { "Retreat immediately and alert others", "Keep pointing the empty extinguisher at the fire", "Try to smother it with your hands or clothing", "Wait a moment and try again" }, 0,
                "A depleted extinguisher can't do anything further — prioritize retreat and raising the alarm over continuing to engage the fire.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_extinguisher_depleted_2", MistakeTags.ExtinguisherDepletedBeforeOut, QuestionDifficulty.Easy,
                "Roughly how long does a typical portable fire extinguisher discharge last once triggered?",
                new[] { "About 8-15 seconds", "About 2-3 minutes", "About 10 minutes", "Unlimited, until the fire is out" }, 0,
                "Most portable extinguishers discharge fully in under 15 seconds — this is exactly why accurate aim and technique matter so much.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_extinguisher_depleted_3", MistakeTags.ExtinguisherDepletedBeforeOut, QuestionDifficulty.Hard,
                "Why does poor aim or technique increase the risk of running out of extinguishing agent before the fire is out?",
                new[] { "Because discharge time is very limited, so wasted spray directly reduces your effective working time", "Because it doesn't — extinguishers refill automatically while spraying", "Because technique only affects noise, not discharge duration", "Because more mistakes always produce more foam" }, 0,
                "With only seconds of discharge available, any spray that misses the fuel source is capacity you can't get back.")));

            // ---------------- fire_safety / main — alarm_not_activated ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_alarm_1", MistakeTags.AlarmNotActivated, QuestionDifficulty.Easy,
                "When should you sound the emergency alarm during a fire response?",
                new[] { "As early as possible, ideally before or while beginning to fight the fire", "Only after the fire is fully out", "Only if you personally can't put the fire out", "Alarms are optional if you feel confident" }, 0,
                "Sounding the alarm early gives everyone nearby the maximum possible warning time, regardless of how the fire response itself goes.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_alarm_2", MistakeTags.AlarmNotActivated, QuestionDifficulty.Medium,
                "Why should you raise the alarm even if you believe you can put the fire out yourself?",
                new[] { "It gives others time to evacuate or assist if the situation gets worse", "It is just a formality with no real safety benefit", "It is only needed to notify management for approval", "It has no effect once someone is already responding" }, 0,
                "You can't guarantee the outcome of fighting a fire — the alarm exists precisely for the case where it doesn't go as planned.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_alarm_3", MistakeTags.AlarmNotActivated, QuestionDifficulty.Medium,
                "Who benefits from an emergency alarm being activated early during a fire?",
                new[] { "Everyone nearby, since it gives them time to evacuate or respond", "Only the person operating the extinguisher", "Only supervisors and management", "No one — it is procedural noise" }, 0,
                "An alarm is a shared safety signal, not a personal tool — its value is in warning everyone in the area at once.")));

            // ---------------- fire_safety / main — poor_sweep_technique ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_sweep_1", MistakeTags.PoorSweepTechnique, QuestionDifficulty.Medium,
                "What is the correct technique when discharging an extinguisher at a fire?",
                new[] { "Sweep side-to-side across the base of the fire", "Hold it still, pointed at the top of the flames", "Spray in short bursts only, never continuously", "Circle around the fire while spraying upward" }, 0,
                "A steady side-to-side sweep at the base smothers the fuel source, not just the visible flame above it.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_sweep_2", MistakeTags.PoorSweepTechnique, QuestionDifficulty.Medium,
                "Why should you aim at the base of a fire rather than at the flames themselves?",
                new[] { "The base is where the fuel source is, so extinguishing there stops the fire at its source", "Flames are too hot to approach at any distance", "Aiming at flames wastes more extinguishing agent than aiming at the base", "It does not matter where you aim, as long as you spray" }, 0,
                "Flames are the visible combustion, not the fuel — putting out the base is what actually stops the fire from continuing.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_sweep_3", MistakeTags.PoorSweepTechnique, QuestionDifficulty.Hard,
                "In the P.A.S.S. technique (Pull, Aim, Squeeze, Sweep), what does the final \"Sweep\" step mean?",
                new[] { "Sweep the nozzle side to side across the base of the fire", "Stop spraying immediately after squeezing the handle once", "Sweep the surrounding area for other hazards", "Step back and sweep your surroundings for an exit" }, 0,
                "\"Sweep\" is the discharge motion itself — a controlled side-to-side pass across the fire's base, not a one-off burst.")));

            // ---------------- fire_safety / main — slow_evacuation ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_evacuation_1", MistakeTags.SlowEvacuation, QuestionDifficulty.Easy,
                "After a fire is extinguished, what should your next action be?",
                new[] { "Move promptly to the designated safe assembly point", "Stay and inspect the area for damage", "Return to your normal work immediately", "Wait for a supervisor before moving" }, 0,
                "Fires can reignite — evacuating promptly to the safe point remains the priority even after the flames appear out.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_evacuation_2", MistakeTags.SlowEvacuation, QuestionDifficulty.Medium,
                "Why should you evacuate promptly even after a fire looks fully out?",
                new[] { "Fires can reignite, and hidden embers or smoke hazards may remain", "It is only a formality once flames are no longer visible", "Only large fires require evacuation afterward", "Evacuation is optional if you personally feel safe" }, 0,
                "\"Out\" to the eye doesn't always mean fully extinguished at the source — prompt evacuation accounts for that uncertainty.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_evacuation_3", MistakeTags.SlowEvacuation, QuestionDifficulty.Medium,
                "What is the main purpose of a designated safe assembly point after a fire incident?",
                new[] { "It lets responders account for everyone and confirm all workers are safe", "It is just a social meeting spot", "It exists only to satisfy paperwork requirements", "It has no practical safety purpose" }, 0,
                "A known assembly point is what makes a headcount possible — without it, nobody can confirm everyone got out safely.")));

            // ---------------- fire_safety / main — general baseline (no tag) ----------------
            seeds.Add(("fire_safety", "main", Fire("fire_q_baseline_1", null, QuestionDifficulty.Hard,
                "Which class of fire extinguisher is typically used for electrical equipment fires?",
                new[] { "Class C", "Class A", "Class B", "Class K" }, 0,
                "Class C extinguishers use a non-conductive agent suited for fires involving live electrical equipment.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_baseline_2", null, QuestionDifficulty.Easy,
                "In the P.A.S.S. technique, what does the \"P\" stand for?",
                new[] { "Pull (the safety pin)", "Point (the nozzle upward)", "Push (the handle down)", "Press (the alarm button)" }, 0,
                "P.A.S.S. stands for Pull the pin, Aim at the base, Squeeze the handle, Sweep side to side.")));

            seeds.Add(("fire_safety", "main", Fire("fire_q_baseline_3", null, QuestionDifficulty.Easy,
                "What is the very first step before using a fire extinguisher?",
                new[] { "Pull the safety pin", "Squeeze the handle", "Sweep side to side", "Aim at the flames" }, 0,
                "The safety pin must be pulled before the handle can be squeezed to discharge the extinguisher.")));

            // ---------------- gas_safety / main ----------------
            seeds.Add(("gas_safety", "main", new QuestionBankItem
            {
                id = "gas_q_localization_1",
                tags = new[] { MistakeTags.WrongHazardLocalization },
                difficulty = QuestionDifficulty.Medium,
                stemEN = "[PLACEHOLDER] What is the most reliable way to pinpoint a suspected gas leak source underground?",
                optionsEN = new[] { "Follow your sense of smell only", "Use a calibrated multi-gas detector", "Guess based on the last known leak location", "Wait for visible symptoms in coworkers" },
                correctIndex = 1,
                explanationEN = "[PLACEHOLDER] A calibrated detector gives an objective reading; smell alone is unreliable and some gases are odorless.",
                stemHI = "", optionsHI = System.Array.Empty<string>(), explanationHI = "",
                stemSAT = "", optionsSAT = System.Array.Empty<string>(), explanationSAT = "",
                stemTA = "", optionsTA = System.Array.Empty<string>(), explanationTA = "",
            }));

            seeds.Add(("gas_safety", "main", new QuestionBankItem
            {
                id = "gas_q_ppe_1",
                tags = new[] { MistakeTags.MissedPpeCheck },
                difficulty = QuestionDifficulty.Easy,
                stemEN = "[PLACEHOLDER] What respiratory protection is required for a toxic, oxygen-deficient confined space?",
                optionsEN = new[] { "A standard dust mask", "Self-Contained Breathing Apparatus (SCBA)", "No protection if the visit is brief", "A wet cloth over the mouth" },
                correctIndex = 1,
                explanationEN = "[PLACEHOLDER] Dust masks don't supply oxygen or filter toxic/asphyxiant gases — only SCBA is adequate here.",
                stemHI = "", optionsHI = System.Array.Empty<string>(), explanationHI = "",
                stemSAT = "", optionsSAT = System.Array.Empty<string>(), explanationSAT = "",
                stemTA = "", optionsTA = System.Array.Empty<string>(), explanationTA = "",
            }));

            seeds.Add(("gas_safety", "main", new QuestionBankItem
            {
                id = "gas_q_buddy_1",
                tags = new[] { MistakeTags.UnsafeZoneEntry },
                difficulty = QuestionDifficulty.Medium,
                stemEN = "[PLACEHOLDER] Before entering a confined space, what must be confirmed with your standby buddy?",
                optionsEN = new[] { "Nothing, entry can proceed independently", "Two-way communication and a check-in plan", "Only that they know your name", "That they are on a lunch break" },
                correctIndex = 1,
                explanationEN = "[PLACEHOLDER] Confirmed two-way communication is what lets a buddy actually respond if something goes wrong inside.",
                stemHI = "", optionsHI = System.Array.Empty<string>(), explanationHI = "",
                stemSAT = "", optionsSAT = System.Array.Empty<string>(), explanationSAT = "",
                stemTA = "", optionsTA = System.Array.Empty<string>(), explanationTA = "",
            }));

            seeds.Add(("gas_safety", "main", new QuestionBankItem
            {
                id = "gas_q_baseline_1",
                tags = System.Array.Empty<string>(),
                difficulty = QuestionDifficulty.Hard,
                stemEN = "[PLACEHOLDER] Which gas is commonly associated with confined-space oxygen displacement in mining?",
                optionsEN = new[] { "Methane", "Helium", "Argon", "Neon" },
                correctIndex = 0,
                explanationEN = "[PLACEHOLDER] Methane buildup displaces breathable oxygen and is also flammable/explosive.",
                stemHI = "", optionsHI = System.Array.Empty<string>(), explanationHI = "",
                stemSAT = "", optionsSAT = System.Array.Empty<string>(), explanationSAT = "",
                stemTA = "", optionsTA = System.Array.Empty<string>(), explanationTA = "",
            }));

            return seeds;
        }
    }
}
