using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;

namespace MiningSafetyAR.Editor
{
    [InitializeOnLoad]
    public class FirebasePluginImportFixer : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100; // Run early before other build preprocessors

        static FirebasePluginImportFixer()
        {
            EditorApplication.delayCall += FixM2RepositoryPluginImporters;
        }

        [MenuItem("Tools/Firebase/Fix Plugin Importers")]
        public static void FixM2RepositoryPluginImporters()
        {
            string m2RepoPath = Path.Combine(Application.dataPath, "Firebase", "m2repository");
            string generatedRepoPath = Path.Combine(Application.dataPath, "GeneratedLocalRepo");

            List<string> allAarFiles = new List<string>();
            
            if (Directory.Exists(m2RepoPath))
            {
                allAarFiles.AddRange(Directory.GetFiles(m2RepoPath, "*.aar", SearchOption.AllDirectories));
            }
            if (Directory.Exists(generatedRepoPath))
            {
                allAarFiles.AddRange(Directory.GetFiles(generatedRepoPath, "*.aar", SearchOption.AllDirectories));
            }

            bool anyChanged = false;

            foreach (string fullPath in allAarFiles)
            {
                string relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                PluginImporter importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;

                if (importer != null)
                {
                    bool isAndroid = importer.GetCompatibleWithPlatform(BuildTarget.Android);
                    bool isAny = importer.GetCompatibleWithAnyPlatform();

                    if (isAndroid || isAny)
                    {
                        importer.SetCompatibleWithAnyPlatform(false);
                        importer.SetCompatibleWithEditor(false);
                        importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                        importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
                        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                        importer.SaveAndReimport();
                        anyChanged = true;
                        Debug.Log($"[FirebasePluginImportFixer] Disabled native Unity plugin import for: {relativePath}");
                    }
                }
            }

            if (anyChanged)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            FixM2RepositoryPluginImporters();
        }
    }
}
