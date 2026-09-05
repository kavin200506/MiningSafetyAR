using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
            if (!Directory.Exists(m2RepoPath))
            {
                return;
            }

            string[] aarFiles = Directory.GetFiles(m2RepoPath, "*.aar", SearchOption.AllDirectories);
            bool anyChanged = false;

            foreach (string fullPath in aarFiles)
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
                        Debug.Log($"[FirebasePluginImportFixer] Disabled plugin import for: {relativePath}");
                    }
                }
            }

            // Also clean up any lingering GeneratedLocalRepo if it appears
            string generatedRepoPath = Path.Combine(Application.dataPath, "GeneratedLocalRepo");
            if (Directory.Exists(generatedRepoPath))
            {
                Directory.Delete(generatedRepoPath, true);
                string metaFile = generatedRepoPath + ".meta";
                if (File.Exists(metaFile))
                {
                    File.Delete(metaFile);
                }
                anyChanged = true;
                Debug.Log("[FirebasePluginImportFixer] Removed redundant GeneratedLocalRepo");
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
