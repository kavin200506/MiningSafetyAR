using UnityEditor;
using UnityEngine;
using MiningSafetyAR.Data;
using System.Collections.Generic;
using System.Linq;

public class AddSubModulesScript
{
    [MenuItem("Tools/Mining Safety/Fix Sub-Modules")]
    public static void GenerateSubModules()
    {
        var db = AssetDatabase.LoadAssetAtPath<ModuleDatabase>("Assets/Resources/Data/ModuleDatabase.asset");
        if (db == null)
        {
            Debug.LogError("Could not find ModuleDatabase at Assets/Resources/Data/ModuleDatabase.asset");
            return;
        }

        // 1. Clean up old incorrect sub-modules
        db.modules.RemoveAll(m => !string.IsNullOrEmpty(m.parentId));

        // 2. Add them back with the CORRECT parent IDs that match your database!
        var categories = new Dictionary<string, string[]>
        {
            { "fire_safety", new[] { "Fire Extinguisher Protocol", "High-Voltage Panel Arc Flash", "Suspended Coal Dust Ignition", "Hydraulic Fluid Spill Fire", "Methane Gas Pocket Ignition" } },
            { "gas_safety", new[] { "Hydrogen Sulfide (H2S) Sump Leak", "Methane Pocket Strike During Drilling", "Blackdamp Accumulation in Abandoned Shaft", "Unprepared Confined Space Entry Rescue", "Diesel Exhaust Inhalation" } },
            // Mapping Structural to electrical_safety for now since your DB doesn't have a Structural module
            { "electrical_safety", new[] { "Unshored Trench Wall Cave-In", "Room-and-Pillar Roof Collapse", "Overloaded Scaffolding Collapse", "Material Silo Structural Rupture", "Open-Pit Highwall Landslide" } },
            { "machinery_safety", new[] { "Dump Truck Blind Spot Crushing", "Excavator Rollover on Uneven Terrain", "Conveyor Belt Entanglement", "Haul Truck Brake Failure on Incline", "Suspended Crane Load Drop" } },
            { "heights_safety", new[] { "Fall from Unprotected Highwall Edge", "Slip on Oil-Coated Walkway", "Broken Rung Ladder Fall", "Plunge through Unmarked Floor Opening", "Trip over Unsecured Power Cables" } }
        };

        var emojis = new Dictionary<string, string> {
            { "fire_safety", "🔥" }, { "gas_safety", "☠️" }, { "electrical_safety", "🏗️" }, { "machinery_safety", "⚙️" }, { "heights_safety", "⚠️" }
        };

        var domains = new Dictionary<string, string> {
            { "fire_safety", "Fire Safety" }, { "gas_safety", "Gas/Hazmat" }, { "electrical_safety", "Structural" }, { "machinery_safety", "Machinery" }, { "heights_safety", "Slips & Falls" }
        };

        var colors = new Dictionary<string, string> {
            { "fire_safety", "#FFCDD2" }, { "gas_safety", "#E1BEE7" }, { "electrical_safety", "#FFE0B2" }, { "machinery_safety", "#B3E5FC" }, { "heights_safety", "#C8E6C9" }
        };

        int addedCount = 0;

        foreach (var kvp in categories)
        {
            string parentId = kvp.Key;
            string[] subNames = kvp.Value;
            
            for (int i = 0; i < subNames.Length; i++)
            {
                string subId = $"{parentId}_sub{i + 1}";
                
                if (db.modules.Any(m => m.id == subId))
                {
                    continue; // already exists
                }

                var sub = new ModuleData
                {
                    id = subId,
                    parentId = parentId,
                    title = subNames[i],
                    iconEmoji = emojis[parentId],
                    domain = domains[parentId],
                    duration = "15 min",
                    difficulty = "Medium",
                    status = ModuleStatus.NotStarted,
                    color = colors[parentId],
                    description = $"Learn how to handle {subNames[i].ToLower()} safely and efficiently in an interactive AR environment.",
                    objectives = new[] { "Identify hazards", "Apply correct protocols", "Evacuate safely" }
                };

                db.modules.Add(sub);
                addedCount++;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully fixed and added {addedCount} sub-modules to ModuleDatabase!");
    }
}
