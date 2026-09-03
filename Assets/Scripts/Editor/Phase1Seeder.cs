using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using MiningSafetyAR.Data;

public static class Phase1Seeder
{
    [MenuItem("Mining Safety AR/Phase 1 - Create Foundation Assets")]
    public static void CreateAll()
    {
        CreatePanelSettings();
        CreateModuleDatabase();
        CreateQuestionDatabase();
        CreateCertificateDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Phase1Seeder] All Phase 1 assets created.");
    }

    static void CreatePanelSettings()
    {
        string path = "Assets/UI/PanelSettings/DefaultPanelSettings.asset";
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        if (existing != null) { Debug.Log($"[Seeder] PanelSettings already exists at {path}"); return; }

        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        // Configure for mobile 430px
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(430, 932);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0f;
        ps.referenceDpi = 96;
        ps.fallbackDpi = 96;
        // themeStyleSheet is ThemeStyleSheet type in UI Toolkit 6000 — leave default for Phase 1

        // Ensure directory
        System.IO.Directory.CreateDirectory("Assets/UI/PanelSettings");
        AssetDatabase.CreateAsset(ps, path);
        Debug.Log($"[Seeder] Created {path}");
    }

    static void CreateModuleDatabase()
    {
        string path = "Assets/Data/ModuleDatabase.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ModuleDatabase>(path);
        if (existing != null) { Debug.Log($"[Seeder] ModuleDatabase exists, skipping"); return; }

        var db = ScriptableObject.CreateInstance<ModuleDatabase>();
        db.modules = new System.Collections.Generic.List<ModuleData>
        {
            new ModuleData{
                id="fire_safety", title="Fire & Explosion Response", iconEmoji="🔥", domain="Fire Safety",
                duration="45 min", difficulty="Medium", status=ModuleStatus.Completed, progress=100, bestScore=85, attempts=3, lastAttempt="2026-08-26", certificateId="JH-FIRE-001928", color="#FF6D00",
                description="Master fire prevention, P.A.S.S. technique, and evacuation protocols for mining environments.",
                objectives=new[]{"Identify fire hazards","Use extinguisher correctly","Execute evacuation"},
                competencyScores=new CompetencyScores{hazardRecognition=85, extinguisherUse=88, ppeSelection=70, evacuation=82, emergencyResponse=80}
            },
            new ModuleData{
                id="gas_safety", title="Gas Leak & Confined Space", iconEmoji="☣️", domain="Chemical Safety",
                duration="50 min", difficulty="Hard", status=ModuleStatus.Completed, progress=100, bestScore=72, attempts=2, lastAttempt="2026-08-28", certificateId="JH-GAS-002156", color="#D32F2F",
                description="Detect gas leaks, select PPE, and follow confined space entry protocols.",
                objectives=new[]{"Detect gas hazards","Select correct PPE","Follow buddy system"},
                competencyScores=new CompetencyScores{hazardRecognition=78, extinguisherUse=60, ppeSelection=85, evacuation=70, emergencyResponse=75}
            },
            new ModuleData{
                id="machinery_safety", title="Machinery Safety", iconEmoji="⚙️", domain="Equipment Safety",
                duration="40 min", difficulty="Medium", status=ModuleStatus.InProgress, progress=45, bestScore=60, attempts=1, lastAttempt="2026-08-27", certificateId="", color="#1976D2",
                description="Lockout/Tagout, machine guarding, and safe operation of mining machinery.",
                objectives=new[]{"Apply LOTO","Inspect guards","Operate safely"},
                competencyScores=new CompetencyScores{hazardRecognition=60, extinguisherUse=50, ppeSelection=65, evacuation=55, emergencyResponse=60}
            },
            new ModuleData{
                id="electrical_safety", title="Electrical Safety", iconEmoji="⚡", domain="Electrical Safety",
                duration="35 min", difficulty="Medium", status=ModuleStatus.NotStarted, progress=0, bestScore=0, attempts=0, lastAttempt="", certificateId="", color="#FBC02D",
                description="Identify electrical hazards and apply safe work practices.",
                objectives=new[]{"Spot electrical risks","Use insulating PPE","Respond to electrocution"},
                competencyScores=new CompetencyScores()
            },
            new ModuleData{
                id="heights_safety", title="Working at Heights", iconEmoji="⛰️", domain="Fall Protection",
                duration="40 min", difficulty="Hard", status=ModuleStatus.Locked, progress=0, bestScore=0, attempts=0, lastAttempt="", certificateId="", color="#388E3C",
                description="Fall protection, harness inspection, and scaffold safety.",
                objectives=new[]{"Use harness","Inspect anchors","Work on scaffolds"},
                competencyScores=new CompetencyScores()
            },
        };
        System.IO.Directory.CreateDirectory("Assets/Data");
        AssetDatabase.CreateAsset(db, path);
        Debug.Log($"[Seeder] Created {path} with {db.modules.Count} modules");
    }

    static void CreateQuestionDatabase()
    {
        string path = "Assets/Data/QuestionDatabase.asset";
        var existing = AssetDatabase.LoadAssetAtPath<QuestionDatabase>(path);
        if (existing != null) { Debug.Log($"[Seeder] QuestionDatabase exists, skipping"); return; }

        var db = ScriptableObject.CreateInstance<QuestionDatabase>();
        db.questions = new System.Collections.Generic.List<QuizQuestionData>();

        // Fire safety 5 Q
        db.questions.Add(new QuizQuestionData{ id="fire_q1", moduleId="fire_safety", textEN="What does P.A.S.S. stand for?", optionsEN=new[]{"Pull, Aim, Squeeze, Sweep","Push, Aim, Squeeze, Sweep","Pull, Aim, Spray, Sweep","Press, Aim, Squeeze, Sweep"}, correctIndex=0, competency="extinguisherUse" });
        db.questions.Add(new QuizQuestionData{ id="fire_q2", moduleId="fire_safety", textEN="Safe distance from fire?", optionsEN=new[]{"1m","2m","3m","5m"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="fire_q3", moduleId="fire_safety", textEN="Fire triangle components?", optionsEN=new[]{"Heat, Fuel, Oxygen","Heat, Fuel, Water","Fuel, Oxygen, CO2","Heat, Water, Oxygen"}, correctIndex=0, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="fire_q4", moduleId="fire_safety", textEN="Aim extinguisher nozzle at?", optionsEN=new[]{"Top of flames","Base of fire","Middle of fire","Smoke"}, correctIndex=1, competency="extinguisherUse" });
        db.questions.Add(new QuizQuestionData{ id="fire_q5", moduleId="fire_safety", textEN="First step in evacuation?", optionsEN=new[]{"Grab belongings","Sound alarm","Run fast","Call family"}, correctIndex=1, competency="evacuation" });

        // Gas safety 5 Q
        db.questions.Add(new QuizQuestionData{ id="gas_q1", moduleId="gas_safety", textEN="First action on gas leak?", optionsEN=new[]{"Enter to check","Evacuate and alert","Open windows only","Ignore odor"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="gas_q2", moduleId="gas_safety", textEN="Correct PPE for gas?", optionsEN=new[]{"Gloves only","Respirator","Helmet only","Boots only"}, correctIndex=1, competency="ppeSelection" });
        db.questions.Add(new QuizQuestionData{ id="gas_q3", moduleId="gas_safety", textEN="Buddy system means?", optionsEN=new[]{"Work alone","Pair and monitor","Only supervisor","One worker watches"}, correctIndex=1, competency="emergencyResponse" });
        db.questions.Add(new QuizQuestionData{ id="gas_q4", moduleId="gas_safety", textEN="Odorless gas requires?", optionsEN=new[]{"No check","Detector test","Smell test","Visual check"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="gas_q5", moduleId="gas_safety", textEN="Pre-entry test checks?", optionsEN=new[]{"Oxygen only","Oxygen, toxic, flammable","Temperature only","Humidity"}, correctIndex=1, competency="hazardRecognition" });

        // Machinery 4 Q
        db.questions.Add(new QuizQuestionData{ id="mach_q1", moduleId="machinery_safety", textEN="LOTO stands for?", optionsEN=new[]{"Lockout/Tagout","Lock On, Tag Off","Leave On, Turn Off","Lock Tools"}, correctIndex=0, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="mach_q2", moduleId="machinery_safety", textEN="When to do LOTO?", optionsEN=new[]{"Only at night","Before maintenance","After work","During operation"}, correctIndex=1, competency="emergencyResponse" });
        db.questions.Add(new QuizQuestionData{ id="mach_q3", moduleId="machinery_safety", textEN="Machine guards must be?", optionsEN=new[]{"Removed","In place and secure","Painted","Oiled"}, correctIndex=1, competency="ppeSelection" });
        db.questions.Add(new QuizQuestionData{ id="mach_q4", moduleId="machinery_safety", textEN="Unguarded machine should be?", optionsEN=new[]{"Used cautiously","Tagged out","Used quickly","Ignored"}, correctIndex=1, competency="emergencyResponse" });

        // Electrical 4 Q
        db.questions.Add(new QuizQuestionData{ id="elec_q1", moduleId="electrical_safety", textEN="Common cause of electrical hazard?", optionsEN=new[]{"Wet hands on live wire","Dry wood","Plastic","Glass"}, correctIndex=0, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="elec_q2", moduleId="electrical_safety", textEN="Electrical PPE includes?", optionsEN=new[]{"Sandals","Insulated gloves","Cotton shirt","Shorts"}, correctIndex=1, competency="ppeSelection" });
        db.questions.Add(new QuizQuestionData{ id="elec_q3", moduleId="electrical_safety", textEN="Importance of grounding?", optionsEN=new[]{"Looks nice","Prevents shock","Saves power","Faster work"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="elec_q4", moduleId="electrical_safety", textEN="Electrocution victim: first step?", optionsEN=new[]{"Touch them","Cut power","Pour water","Pull with hands"}, correctIndex=1, competency="emergencyResponse" });

        // Heights 4 Q
        db.questions.Add(new QuizQuestionData{ id="height_q1", moduleId="heights_safety", textEN="Fall protection required above?", optionsEN=new[]{"1m","1.8m","3m","5m"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="height_q2", moduleId="heights_safety", textEN="Harness inspection checks?", optionsEN=new[]{"Color only","Webbing, stitching, hardware","Price","Brand"}, correctIndex=1, competency="ppeSelection" });
        db.questions.Add(new QuizQuestionData{ id="height_q3", moduleId="heights_safety", textEN="Anchor point must support?", optionsEN=new[]{"100kg","5000kg per worker","50kg","1000kg"}, correctIndex=1, competency="hazardRecognition" });
        db.questions.Add(new QuizQuestionData{ id="height_q4", moduleId="heights_safety", textEN="Scaffold safety requires?", optionsEN=new[]{"Loose boards","Guardrails and secure planks","No guardrails","Single plank"}, correctIndex=1, competency="evacuation" });

        System.IO.Directory.CreateDirectory("Assets/Data");
        AssetDatabase.CreateAsset(db, path);
        Debug.Log($"[Seeder] Created {path} with {db.questions.Count} questions");
    }

    static void CreateCertificateDatabase()
    {
        string path = "Assets/Data/CertificateDatabase.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CertificateDatabase>(path);
        if (existing != null) { Debug.Log($"[Seeder] CertificateDatabase exists, skipping"); return; }

        var db = ScriptableObject.CreateInstance<CertificateDatabase>();
        db.certificates = new System.Collections.Generic.List<CertificateData>
        {
            new CertificateData{ id="JH-FIRE-001928", workerName="Ramesh Kumar", workerId="W-10492", moduleId="fire_safety", moduleTitle="Fire & Explosion Response", score=85, issuedDate="2026-08-26", expiryDate="2027-08-26", organization="Jharkhand Steel Works", status="VALID", signatureHash="" },
            new CertificateData{ id="JH-GAS-002156", workerName="Ramesh Kumar", workerId="W-10492", moduleId="gas_safety", moduleTitle="Gas Leak & Confined Space", score=72, issuedDate="2026-08-28", expiryDate="2027-08-28", organization="Jharkhand Steel Works", status="VALID", signatureHash="" },
        };
        System.IO.Directory.CreateDirectory("Assets/Data");
        AssetDatabase.CreateAsset(db, path);
        Debug.Log($"[Seeder] Created {path} with {db.certificates.Count} certs");
    }
}
