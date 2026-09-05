using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MiningSafetyAR.Firebase
{
    /// <summary>
    /// Centralized Firestore REST service. All reads/writes go through here.
    ///
    /// Schema:
    ///   workers/{uid}                      – profile fields only
    ///   workers/{uid}/progress/{moduleId}  – one doc per module
    ///   trainingResults/{resultId}         – quiz results
    ///
    /// No SDK required — uses UnityWebRequest + REST API.
    /// </summary>
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance { get; private set; }

        private const string PROJECT_ID = "minesafetyar";
        private const string BASE_URL = "https://firestore.googleapis.com/v1/projects/minesafetyar/databases/(default)/documents";
        private const string API_KEY = "AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybI";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ----------------------------------------------------------------
        // GENERIC REST
        // ----------------------------------------------------------------

        IEnumerator PatchDocument(string path, string json, Action<bool, string> cb, bool useAuth = true)
        {
            string url = $"{BASE_URL}/{path}?key={API_KEY}";
            string firestoreJson = ConvertToFirestoreFields(json);
            Debug.Log($"[Firestore] PATCH {path} ({firestoreJson.Length} bytes)");

            using var req = new UnityWebRequest(url, "PATCH");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(firestoreJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (useAuth) yield return AttachAuthToken(req);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] PATCH OK {path}");
                cb?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Firestore] PATCH FAIL {path}: {req.error} | {req.downloadHandler.text}");
                cb?.Invoke(false, req.error + "|" + req.downloadHandler.text);
            }
        }

        IEnumerator GetDocument(string path, Action<bool, string> cb, bool useAuth = true)
        {
            string url = $"{BASE_URL}/{path}?key={API_KEY}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type", "application/json");

            if (useAuth) yield return AttachAuthToken(req);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] GET OK {path}");
                cb?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[Firestore] GET FAIL {path}: {req.error} | {req.downloadHandler.text}");
                cb?.Invoke(false, req.error + "|" + req.downloadHandler.text);
            }
        }

        IEnumerator DeleteDocument(string path, Action<bool, string> cb, bool useAuth = true)
        {
            string url = $"{BASE_URL}/{path}?key={API_KEY}";
            using var req = new UnityWebRequest(url, "DELETE");
            req.downloadHandler = new DownloadHandlerBuffer();

            if (useAuth) yield return AttachAuthToken(req);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Firestore] DELETE OK {path}");
                cb?.Invoke(true, "");
            }
            else
            {
                Debug.LogWarning($"[Firestore] DELETE FAIL {path}: {req.error}");
                cb?.Invoke(false, req.error);
            }
        }

        IEnumerator RunQuery(string collectionPath, string orderBy, int limit, Action<bool, List<Dictionary<string, object>>> cb, bool useAuth = true)
        {
            // Use structured query via documents:runQuery
            string url = $"{BASE_URL}/{collectionPath}:runQuery?key={API_KEY}";
            var queryBody = new StringBuilder("{");
            queryBody.Append("\"from\":[{\"collectionId\":\"" + collectionPath.Split('/')[^1] + "\"}]");
            if (!string.IsNullOrEmpty(orderBy))
                queryBody.Append(",\"orderBy\":[{\"field\":{\"fieldPath\":\"" + orderBy + "\"},\"direction\":\"DESCENDING\"}]");
            if (limit > 0)
                queryBody.Append(",\"limit\":" + limit);
            queryBody.Append("}");

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(queryBody.ToString()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (useAuth) yield return AttachAuthToken(req);

            yield return req.SendWebRequest();

            var results = new List<Dictionary<string, object>>();
            if (req.result == UnityWebRequest.Result.Success)
            {
                // Response is a JSON array of objects, each with "document" or "readTime"
                var arr = MiniJSON.Json.Deserialize(req.downloadHandler.text) as List<object>;
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        var dict = item as Dictionary<string, object>;
                        if (dict != null && dict.ContainsKey("document"))
                        {
                            var doc = dict["document"] as Dictionary<string, object>;
                            if (doc != null) results.Add(doc);
                        }
                    }
                }
                cb?.Invoke(true, results);
            }
            else
            {
                Debug.LogWarning($"[Firestore] QUERY FAIL {collectionPath}: {req.error}");
                cb?.Invoke(false, results);
            }
        }

        IEnumerator AttachAuthToken(UnityWebRequest req)
        {
            if (FirebaseAuthManager.Instance == null || !FirebaseAuthManager.Instance.IsLoggedIn)
                yield break;
            bool done = false;
            string token = null;
            FirebaseAuthManager.Instance.GetIdToken(t => { token = t; done = true; });
            yield return new WaitUntil(() => done);
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);
        }

        // ----------------------------------------------------------------
        // CERTIFICATES API
        // ----------------------------------------------------------------

        public void SaveCertificateToFirestore(string firebaseUid, string certId, string certJson, Action<bool, string> cb)
        {
            if (string.IsNullOrEmpty(firebaseUid) || string.IsNullOrEmpty(certId))
            {
                cb?.Invoke(false, "Invalid params");
                return;
            }

            // Save under worker's subcollection: workers/{uid}/certificates/{certId}
            StartCoroutine(PatchDocument($"workers/{firebaseUid}/certificates/{certId}", certJson, (ok1, resp1) =>
            {
                // Also save under global public collection: certificates/{certId}
                StartCoroutine(PatchDocument($"certificates/{certId}", certJson, (ok2, resp2) =>
                {
                    cb?.Invoke(ok1 || ok2, resp1);
                }, useAuth: false));
            }));
        }

        public void GetAllCertificates(string firebaseUid, Action<bool, List<Dictionary<string, object>>> cb)
        {
            if (string.IsNullOrEmpty(firebaseUid))
            {
                cb?.Invoke(false, new List<Dictionary<string, object>>());
                return;
            }
            string url = $"{BASE_URL}/workers/{firebaseUid}/certificates?key={API_KEY}";
            StartCoroutine(ListCollection(url, cb));
        }

        // ----------------------------------------------------------------
        // JSON -> FIRESTORE FIELDS CONVERSION
        // ----------------------------------------------------------------

        string ConvertToFirestoreFields(string flatJson)
        {
            try
            {
                var obj = MiniJSON.Json.Deserialize(flatJson) as Dictionary<string, object>;
                if (obj == null) return "{\"fields\":{}}";
                var sb = new StringBuilder("{\"fields\":{");
                ConvertObject(sb, obj);
                sb.Append("}}");
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Firestore] ConvertToFirestoreFields fallback: {e.Message}");
                return "{\"fields\":{}}";
            }
        }

        void ConvertObject(StringBuilder sb, Dictionary<string, object> dict)
        {
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"\"{kv.Key}\":");
                ConvertValue(sb, kv.Value);
            }
        }

        void ConvertValue(StringBuilder sb, object value)
        {
            if (value == null)
                sb.Append("{\"nullValue\":null}");
            else if (value is string s)
                sb.Append($"{{\"stringValue\":\"{Escape(s)}\"}}");
            else if (value is bool b)
                sb.Append($"{{\"booleanValue\":{(b ? "true" : "false")}}}");
            else if (value is int || value is long)
                sb.Append($"{{\"integerValue\":\"{value}\"}}");
            else if (value is double || value is float)
                sb.Append($"{{\"doubleValue\":{value}}}");
            else if (value is Dictionary<string, object> nested)
            {
                // Nested object — recurse
                sb.Append("{\"mapValue\":{\"fields\":{");
                ConvertObject(sb, nested);
                sb.Append("}}}");
            }
            else if (value is List<object> list)
            {
                // Array — convert to Firestore arrayValue
                sb.Append("{\"arrayValue\":{\"values\":[");
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    ConvertValue(sb, item);
                }
                sb.Append("]}}");
            }
            else
                sb.Append($"{{\"stringValue\":\"{Escape(value.ToString())}\"}}");
        }

        string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        // ----------------------------------------------------------------
        // FIRESTORE FIELDS -> DICTIONARY PARSING
        // ----------------------------------------------------------------

        public static Dictionary<string, object> ParseFirestoreFields(string firestoreJson)
        {
            try
            {
                var root = MiniJSON.Json.Deserialize(firestoreJson) as Dictionary<string, object>;
                if (root != null && root.TryGetValue("fields", out var fieldsObj))
                    return fieldsObj as Dictionary<string, object>;
                // Might be a raw document without "fields" wrapper
                return root;
            }
            catch { return null; }
        }

        public static string GetstringValue(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return "";
            if (v is Dictionary<string, object> d)
            {
                if (d.TryGetValue("stringValue", out var sv)) return sv as string ?? "";
                if (d.TryGetValue("integerValue", out var iv)) return iv.ToString();
                if (d.TryGetValue("doubleValue", out var dv)) return dv.ToString();
                if (d.TryGetValue("booleanValue", out var bv)) return bv.ToString();
            }
            return v?.ToString() ?? "";
        }

        public static int GetintValue(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return 0;
            if (v is Dictionary<string, object> d)
            {
                if (d.TryGetValue("integerValue", out var iv) && int.TryParse(iv.ToString(), out int i)) return i;
                if (d.TryGetValue("stringValue", out var sv) && int.TryParse(sv.ToString(), out int i2)) return i2;
                if (d.TryGetValue("doubleValue", out var dv) && int.TryParse(dv.ToString(), out int i3)) return i3;
            }
            return 0;
        }

        public static bool GetboolValue(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return false;
            if (v is Dictionary<string, object> d && d.TryGetValue("booleanValue", out var bv))
                return bv is bool b ? b : bool.TryParse(bv.ToString(), out var br) && br;
            return false;
        }

        public static Dictionary<string, object> GetmapValue(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return null;
            if (v is Dictionary<string, object> d && d.TryGetValue("mapValue", out var mv))
            {
                if (mv is Dictionary<string, object> mapVal && mapVal.TryGetValue("fields", out var mapFields))
                    return mapFields as Dictionary<string, object>;
            }
            return null;
        }

        public static List<Dictionary<string, object>> GetarrayValues(Dictionary<string, object> fields, string key)
        {
            var result = new List<Dictionary<string, object>>();
            if (fields == null || !fields.TryGetValue(key, out var v)) return result;
            if (v is Dictionary<string, object> d && d.TryGetValue("arrayValue", out var av))
            {
                if (av is Dictionary<string, object> arrVal && arrVal.TryGetValue("values", out var valuesObj) && valuesObj is List<object> values)
                {
                    foreach (var item in values)
                    {
                        if (item is Dictionary<string, object> itemDict && itemDict.TryGetValue("mapValue", out var itemMv))
                        {
                            if (itemMv is Dictionary<string, object> itemMapVal && itemMapVal.TryGetValue("fields", out var itemFields))
                                result.Add(itemFields as Dictionary<string, object>);
                        }
                    }
                }
            }
            return result;
        }

        // ----------------------------------------------------------------
        // WORKER PROFILE
        // ----------------------------------------------------------------

        public void SaveWorker(string firebaseUid, string flatJson, Action<bool, string> cb = null)
        {
            StartCoroutine(PatchDocument($"workers/{firebaseUid}", flatJson, cb));
        }

        public void GetWorker(string firebaseUid, Action<bool, string> cb)
        {
            StartCoroutine(GetDocument($"workers/{firebaseUid}", cb));
        }

        // ----------------------------------------------------------------
        // MODULE PROGRESS  (subcollection: workers/{uid}/progress/{moduleId})
        // ----------------------------------------------------------------

        public void SaveModuleProgress(string firebaseUid, string moduleId, string flatJson, Action<bool, string> cb = null)
        {
            StartCoroutine(PatchDocument($"workers/{firebaseUid}/progress/{moduleId}", flatJson, cb));
        }

        public void GetModuleProgress(string firebaseUid, string moduleId, Action<bool, string> cb)
        {
            StartCoroutine(GetDocument($"workers/{firebaseUid}/progress/{moduleId}", cb));
        }

        public void GetAllModuleProgress(string firebaseUid, Action<bool, List<Dictionary<string, object>>> cb)
        {
            // List all documents in workers/{uid}/progress subcollection
            string url = $"{BASE_URL}/workers/{firebaseUid}/progress?key={API_KEY}";
            StartCoroutine(ListCollection(url, cb));
        }

        public void DeleteModuleProgress(string firebaseUid, string moduleId, Action<bool, string> cb = null)
        {
            StartCoroutine(DeleteDocument($"workers/{firebaseUid}/progress/{moduleId}", cb));
        }

        IEnumerator ListCollection(string url, Action<bool, List<Dictionary<string, object>>> cb, bool useAuth = true)
        {
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type", "application/json");
            if (useAuth) yield return AttachAuthToken(req);
            yield return req.SendWebRequest();

            var results = new List<Dictionary<string, object>>();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var root = MiniJSON.Json.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
                if (root != null && root.TryGetValue("documents", out var docsObj) && docsObj is List<object> docs)
                {
                    foreach (var doc in docs)
                    {
                        var docDict = doc as Dictionary<string, object>;
                        if (docDict != null) results.Add(docDict);
                    }
                }
                cb?.Invoke(true, results);
            }
            else
            {
                Debug.LogWarning($"[Firestore] LIST FAIL: {req.error}");
                cb?.Invoke(false, results);
            }
        }

        // ----------------------------------------------------------------
        // TRAINING RESULTS (subcollection: workers/{uid}/results/{resultId})
        // ----------------------------------------------------------------

        public void SaveTrainingResult(string firebaseUid, string resultId, string flatJson, Action<bool, string> cb = null)
        {
            StartCoroutine(PatchDocument($"workers/{firebaseUid}/results/{resultId}", flatJson, cb));
        }

        public void GetAllTrainingResults(string firebaseUid, Action<bool, List<Dictionary<string, object>>> cb)
        {
            string url = $"{BASE_URL}/workers/{firebaseUid}/results?key={API_KEY}";
            StartCoroutine(ListCollection(url, cb));
        }

        // ----------------------------------------------------------------
        // LEGACY COMPAT (kept for Phase 0 tester — delegates to typed methods)
        // ----------------------------------------------------------------

        public void SaveTestDocument(string collection, string docId, string json, Action<bool, string> callback = null)
        {
            StartCoroutine(PatchDocument($"{collection}/{docId}", json, callback));
        }

        public void GetTestDocument(string collection, string docId, Action<bool, string> callback)
        {
            StartCoroutine(GetDocument($"{collection}/{docId}", callback));
        }

        public void SaveRaw(string path, string flatJson, Action<bool, string> cb)
        {
            StartCoroutine(PatchDocument(path, flatJson, cb));
        }
    }

    // ----------------------------------------------------------------
    // Minimal JSON parser (uses Firebase's Google.MiniJSON if available)
    // ----------------------------------------------------------------
    internal static class MiniJSON
    {
        public static class Json
        {
            public static string Serialize(object obj)
            {
                var sb = new StringBuilder();
                SerializeValue(sb, obj);
                return sb.ToString();
            }

            static void SerializeValue(StringBuilder sb, object value)
            {
                if (value == null) { sb.Append("null"); return; }
                if (value is string s) { sb.Append($"\"{EscapeJson(s)}\""); return; }
                if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
                if (value is int || value is long || value is float || value is double) { sb.Append(value); return; }
                if (value is Dictionary<string, object> dict)
                {
                    sb.Append("{");
                    bool first = true;
                    foreach (var kv in dict)
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        sb.Append($"\"{EscapeJson(kv.Key)}\":");
                        SerializeValue(sb, kv.Value);
                    }
                    sb.Append("}");
                    return;
                }
                if (value is List<object> list)
                {
                    sb.Append("[");
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        SerializeValue(sb, item);
                    }
                    sb.Append("]");
                    return;
                }
                // fallback
                sb.Append($"\"{EscapeJson(value.ToString())}\"");
            }

            static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

            public static object Deserialize(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                int pos = 0;
                try
                {
                    return ParseValue(json, ref pos);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MiniJSON] Parse error at pos {pos}: {e.Message}");
                    return null;
                }
            }

            static object ParseValue(string json, ref int pos)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) return null;
                char c = json[pos];
                if (c == '{') return ParseObject(json, ref pos);
                if (c == '[') return ParseArray(json, ref pos);
                if (c == '"') return ParseString(json, ref pos);
                if (c == 't' || c == 'f') return ParseBool(json, ref pos);
                if (c == 'n') return ParseNull(json, ref pos);
                return ParseNumber(json, ref pos);
            }

            static Dictionary<string, object> ParseObject(string json, ref int pos)
            {
                var dict = new Dictionary<string, object>();
                pos++; // skip '{'
                while (pos < json.Length)
                {
                    SkipWhitespace(json, ref pos);
                    if (json[pos] == '}') { pos++; return dict; }
                    string key = ParseString(json, ref pos);
                    SkipWhitespace(json, ref pos);
                    if (json[pos] == ':') pos++;
                    dict[key] = ParseValue(json, ref pos);
                    SkipWhitespace(json, ref pos);
                    if (json[pos] == ',') pos++;
                }
                return dict;
            }

            static List<object> ParseArray(string json, ref int pos)
            {
                var list = new List<object>();
                pos++; // skip '['
                while (pos < json.Length)
                {
                    SkipWhitespace(json, ref pos);
                    if (json[pos] == ']') { pos++; return list; }
                    list.Add(ParseValue(json, ref pos));
                    SkipWhitespace(json, ref pos);
                    if (json[pos] == ',') pos++;
                }
                return list;
            }

            static string ParseString(string json, ref int pos)
            {
                pos++; // skip '"'
                var sb = new StringBuilder();
                while (pos < json.Length)
                {
                    char c = json[pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (pos >= json.Length) break;
                        char esc = json[pos++];
                        if (esc == 'n') sb.Append('\n');
                        else if (esc == 'r') sb.Append('\r');
                        else if (esc == 't') sb.Append('\t');
                        else sb.Append(esc);
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            static bool ParseBool(string json, ref int pos)
            {
                if (json[pos] == 't') { pos += 4; return true; }
                pos += 5; return false;
            }

            static object ParseNull(string json, ref int pos)
            {
                pos += 4; return null;
            }

            static object ParseNumber(string json, ref int pos)
            {
                int start = pos;
                while (pos < json.Length && "-+0123456789.eE".IndexOf(json[pos]) >= 0) pos++;
                string numStr = json.Substring(start, pos - start);
                if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                {
                    if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) return d;
                }
                if (long.TryParse(numStr, out long l)) return l;
                return 0;
            }

            static void SkipWhitespace(string json, ref int pos)
            {
                while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            }
        }
    }
}
