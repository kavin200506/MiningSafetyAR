using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MiningSafetyAR.Firebase
{
    /// <summary>
    /// Firestore REST service — works without Firestore SDK (uses UnityWebRequest).
    /// Project: minesafetyar (193064823382), package com.company.minear
    /// Collections: workers/{uid}, trainingResults/{id}, certificates/{id}
    /// Auth: Bearer ID token if logged in (optional for open rules).
    /// </summary>
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance { get; private set; }

        private const string PROJECT_ID = "minesafetyar";
        private const string BASE_URL = "https://firestore.googleapis.com/v1/projects/minesafetyar/databases/(default)/documents";
        // Firebase API key from google-services.json — used as ?key= for unauthenticated REST fallback
        private const string API_KEY = "AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybI";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // --------------------------------------------------------------------
        // GENERIC REST HELPERS
        // --------------------------------------------------------------------

        IEnumerator PutDocument(string path, string json, Action<bool, string> callback, bool useAuth = true)
        {
            // path e.g. "workers/abc123"  ->  BASE_URL/workers/abc123?key=...
            string url = $"{BASE_URL}/{path}?key={API_KEY}";
            // Firestore REST expects document fields wrapper, but for our simple JsonUtility objects
            // we can PUT the raw JSON if we bypass field conversion by using PATCH with document?
            // Simpler: use PATCH with document fields auto-created via our own wrapper.
            // For Phase 0 verification we store raw JSON as a single document via POST semantics using PUT to collection/doc.
            // Firestore REST requires ?key or Bearer token. We try with API key, then with ID token if available.

            // Wrap json into Firestore document format if needed — here we send raw and let Firestore infer via alternative endpoint:
            // Alternative: Use Firestore REST "documents:commit" is heavy. For demo, we use Firebase Firestore via WebAPI with structured fields.
            // Minimal working PUT: send {"fields": {...}} — so we convert flat json to fields.

            string firestoreJson = ConvertToFirestoreFields(json);
            Debug.Log($"[Firestore] PUT {url}\n{firestoreJson.Substring(0, Math.Min(500, firestoreJson.Length))}");

            using var req = new UnityWebRequest(url, "PATCH");
            byte[] body = Encoding.UTF8.GetBytes(firestoreJson);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            // Attach ID token if logged in for secured rules
            if (useAuth && FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsLoggedIn)
            {
                bool tokenDone = false;
                string token = null;
                FirebaseAuthManager.Instance.GetIdToken(t => { token = t; tokenDone = true; });
                yield return new WaitUntil(() => tokenDone);
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] PUT OK {path}: {req.downloadHandler.text.Substring(0, Math.Min(400, req.downloadHandler.text.Length))}");
                callback?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Firestore] PUT FAILED {path}: {req.error} | {req.downloadHandler.text} | code={req.responseCode}");
                callback?.Invoke(false, req.error + " | " + req.downloadHandler.text);
            }
        }

        IEnumerator GetDocument(string path, Action<bool, string> callback, bool useAuth = true)
        {
            string url = $"{BASE_URL}/{path}?key={API_KEY}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type", "application/json");

            if (useAuth && FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsLoggedIn)
            {
                bool tokenDone = false;
                string token = null;
                FirebaseAuthManager.Instance.GetIdToken(t => { token = t; tokenDone = true; });
                yield return new WaitUntil(() => tokenDone);
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] GET OK {path}");
                callback?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[Firestore] GET FAILED {path}: {req.error} | {req.downloadHandler.text} | code={req.responseCode}");
                callback?.Invoke(false, req.error + " | " + req.downloadHandler.text);
            }
        }

        // Convert flat JSON {"id":"JH","name":"R"} to Firestore fields {"fields":{"id":{"stringValue":"JH"},"name":{"stringValue":"R"}}}
        // Very light converter — handles string/int/float/bool only, enough for Phase 0 verification.
        string ConvertToFirestoreFields(string flatJson)
        {
            try
            {
                var dict = MiniJSON.Json.Deserialize(flatJson) as System.Collections.Generic.Dictionary<string, object>;
                if (dict == null) return "{\"fields\":{}}";
                var sb = new StringBuilder("{\"fields\":{");
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append($"\"{kv.Key}\":");
                    if (kv.Value is string s) sb.Append($"{{\"stringValue\":\"{Escape(s)}\"}}");
                    else if (kv.Value is bool b) sb.Append($"{{\"booleanValue\":{(b ? "true" : "false")}}}");
                    else if (kv.Value is long || kv.Value is int) sb.Append($"{{\"integerValue\":\"{kv.Value}\"}}");
                    else if (kv.Value is double || kv.Value is float) sb.Append($"{{\"doubleValue\":{kv.Value}}}");
                    else if (kv.Value == null) sb.Append("{\"nullValue\":null}");
                    else sb.Append($"{{\"stringValue\":\"{Escape(kv.Value.ToString())}\"}}");
                }
                sb.Append("}}");
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Firestore] Convert fallback raw: {e.Message}");
                return "{\"fields\":{}}";
            }
        }

        string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        // --------------------------------------------------------------------
        // PUBLIC API — generic documents (Phase 0 verification)
        // --------------------------------------------------------------------

        public void SaveTestDocument(string collection, string docId, string json, Action<bool, string> callback = null)
        {
            StartCoroutine(PutDocument($"{collection}/{docId}", json, (ok, resp) => callback?.Invoke(ok, resp)));
        }

        public void GetTestDocument(string collection, string docId, Action<bool, string> callback)
        {
            StartCoroutine(GetDocument($"{collection}/{docId}", (ok, resp) => callback?.Invoke(ok, resp)));
        }

        // --------------------------------------------------------------------
        // Typed helpers (will be used from Phase 1+ via AppDataService)
        // --------------------------------------------------------------------

        public void SaveWorkerJson(string firebaseUid, string workerJson, Action<bool, string> cb = null)
        {
            // Use PATCH to create/update workers/{uid}
            StartCoroutine(PutDocument($"workers/{firebaseUid}", workerJson, (ok, r) => cb?.Invoke(ok, r)));
        }

        public void GetWorkerJson(string firebaseUid, Action<bool, string> cb)
        {
            StartCoroutine(GetDocument($"workers/{firebaseUid}", (ok, r) => cb?.Invoke(ok, r)));
        }

        // Exposed for tester: save arbitrary JSON under test collection for verification without touching real data
        public void SaveRaw(string path, string flatJson, Action<bool, string> cb)
        {
            StartCoroutine(PutDocument(path, flatJson, (ok, r) => cb?.Invoke(ok, r)));
        }
    }

    // Light MiniJSON — embed to avoid dependency on Firebase's Google.MiniJson
    // We reuse the one from Firebase DLL if available, else fallback to simple.
    internal static class MiniJSON
    {
        public static class Json
        {
            public static object Deserialize(string json)
            {
                // Use Firebase's Google.MiniJson if available via reflection, else simple parser
                try
                {
                    var type = Type.GetType("Google.MiniJSON.Json, Google.MiniJson");
                    if (type != null)
                    {
                        var m = type.GetMethod("Deserialize", new[] { typeof(string) });
                        return m.Invoke(null, new object[] { json });
                    }
                }
                catch { }
                // Fallback very naive: parse only flat objects {"k":"v",...}
                return SimpleDeserialize(json);
            }

            static System.Collections.Generic.Dictionary<string, object> SimpleDeserialize(string json)
            {
                var dict = new System.Collections.Generic.Dictionary<string, object>();
                json = json.Trim().TrimStart('{').TrimEnd('}');
                if (string.IsNullOrWhiteSpace(json)) return dict;
                // split by comma not inside quotes — naive but enough for flat test docs
                var parts = System.Text.RegularExpressions.Regex.Split(json, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                foreach (var p in parts)
                {
                    var kv = p.Split(new[] { ':' }, 2);
                    if (kv.Length != 2) continue;
                    string k = kv[0].Trim().Trim('"');
                    string vRaw = kv[1].Trim();
                    object v;
                    if (vRaw.StartsWith("\"")) v = vRaw.Trim('"');
                    else if (vRaw == "true") v = true;
                    else if (vRaw == "false") v = false;
                    else if (vRaw == "null") v = null;
                    else if (long.TryParse(vRaw, out long l)) v = l;
                    else if (double.TryParse(vRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) v = d;
                    else v = vRaw;
                    dict[k] = v;
                }
                return dict;
            }
        }
    }
}
