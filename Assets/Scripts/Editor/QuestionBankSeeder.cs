using UnityEditor;
using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.Editor
{
    /// <summary>
    /// One-off dev utility to push QuestionBankSeedData's PLACEHOLDER questions into Firestore.
    /// Must be run in Play mode, logged in as a worker (FirestoreService.SaveQuestionBankItem
    /// requires auth — see its doc-comment on why that's the least-bad default, not a real access
    /// control, since this project has no firestore.rules file yet).
    /// </summary>
    public static class QuestionBankSeeder
    {
        [MenuItem("Mining Safety AR/Seed Question Bank (PLACEHOLDER content)")]
        public static void SeedQuestionBank()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Seed Question Bank",
                    "Enter Play mode and log in as a worker first — this writes over the network via FirestoreService, which only exists at runtime.",
                    "OK");
                return;
            }

            if (FirestoreService.Instance == null)
            {
                Debug.LogError("[QuestionBankSeeder] FirestoreService.Instance is null — is the app fully booted?");
                return;
            }

            var seeds = QuestionBankSeedData.GetSeedQuestions();
            Debug.Log($"[QuestionBankSeeder] Seeding {seeds.Count} PLACEHOLDER question bank docs...");

            int done = 0, failed = 0;
            foreach (var (moduleId, submoduleId, item) in seeds)
            {
                string flatJson = MiniJSON.Json.Serialize(item.ToFlatDict());
                FirestoreService.Instance.SaveQuestionBankItem(moduleId, submoduleId, item.id, flatJson, (ok, resp) =>
                {
                    if (ok) done++; else { failed++; Debug.LogError($"[QuestionBankSeeder] Failed to seed {moduleId}/{submoduleId}/{item.id}: {resp}"); }
                    if (done + failed == seeds.Count)
                        Debug.Log($"[QuestionBankSeeder] Done — {done} succeeded, {failed} failed.");
                });
            }
        }
    }
}
