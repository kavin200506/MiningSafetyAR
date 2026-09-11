using UnityEngine;

namespace MiningSafetyAR.UI.Core
{
    public static class UIBootstrap
    {
        static bool spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (spawned) return;
            spawned = true;

            // NavigationManager persistent
            var navGo = new GameObject("NavigationManager");
            Object.DontDestroyOnLoad(navGo);
            navGo.AddComponent<Navigation.NavigationManager>();

            // AppDataService persistent
            var appGo = new GameObject("AppDataService");
            Object.DontDestroyOnLoad(appGo);
            var svc = appGo.AddComponent<Data.AppDataService>();
#if UNITY_EDITOR
            // Try to auto-assign databases in Editor
            var modDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.ModuleDatabase>("Assets/Data/ModuleDatabase.asset");
            var qDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.QuestionDatabase>("Assets/Data/QuestionDatabase.asset");
            var certDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.CertificateDatabase>("Assets/Data/CertificateDatabase.asset");
            var t = typeof(Data.AppDataService);
            var f1 = t.GetField("moduleDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f1 != null && modDb != null) f1.SetValue(svc, modDb);
            var f2 = t.GetField("questionDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f2 != null && qDb != null) f2.SetValue(svc, qDb);
            var f3 = t.GetField("certificateDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f3 != null && certDb != null) f3.SetValue(svc, certDb);
#endif

            // FaceVerificationService persistent
            var faceGo = new GameObject("FaceVerificationService");
            Object.DontDestroyOnLoad(faceGo);
            var faceSvc = faceGo.AddComponent<Data.FaceVerificationService>();
#if UNITY_EDITOR
            // Auto-assign the BlazeFace detector + its anchors.csv, and the MobileFaceNet embedder.
            var faceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/FaceDetection/blaze_face_short_range.onnx");
            var faceAnchors = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Models/FaceDetection/anchors.csv");
            var embedModel = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/Models/FaceEmbedding/mobile_facenet.onnx");
            var faceT = typeof(Data.FaceVerificationService);
            var ff1 = faceT.GetField("faceDetectionModel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ff1 != null && faceModel != null) ff1.SetValue(faceSvc, faceModel);
            var ff2 = faceT.GetField("detectionAnchorsCsv", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ff2 != null && faceAnchors != null) ff2.SetValue(faceSvc, faceAnchors);
            var ff3 = faceT.GetField("faceEmbeddingModel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ff3 != null && embedModel != null) ff3.SetValue(faceSvc, embedModel);
            if (faceModel == null) Debug.LogWarning("[UIBootstrap] blaze_face_short_range.onnx not found at expected path — FaceVerificationService will not detect faces.");
            if (faceAnchors == null) Debug.LogWarning("[UIBootstrap] anchors.csv not found at expected path — FaceVerificationService will not detect faces.");
            if (embedModel == null) Debug.LogWarning("[UIBootstrap] mobile_facenet.onnx not found at expected path — FaceVerificationService will not embed faces.");
#endif

            // QuizSelectionService persistent — no asset references to auto-assign, its
            // feedbackEndpointUrl default already points at the deployed Cloudflare Worker.
            var quizGo = new GameObject("QuizSelectionService");
            Object.DontDestroyOnLoad(quizGo);
            quizGo.AddComponent<Data.QuizSelectionService>();

            Debug.Log("[UIBootstrap] Spawned NavigationManager + AppDataService + FaceVerificationService + QuizSelectionService");
        }
    }
}
