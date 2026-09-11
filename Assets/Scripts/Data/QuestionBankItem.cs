using System;
using System.Collections.Generic;
using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.Data
{
    public enum QuestionDifficulty { Easy, Medium, Hard }

    /// <summary>
    /// One question in questionBank/{moduleId}/submodules/{submoduleId}/questions/{questionId} — see
    /// FirestoreService's class-level schema doc-comment. Content (stem/options/explanation) is
    /// authored per-language matching LanguageManager's 4-language pattern used elsewhere (WorkerData,
    /// QuizQuestionData, etc.) rather than a generic dictionary, for consistency with the rest of the
    /// codebase.
    /// </summary>
    [Serializable]
    public class QuestionBankItem
    {
        public string id;
        public string[] tags;              // matches the MistakeTags taxonomy
        public QuestionDifficulty difficulty;

        public string stemEN, stemHI, stemSAT, stemTA;
        public string[] optionsEN, optionsHI, optionsSAT, optionsTA;
        public int correctIndex;
        public string explanationEN, explanationHI, explanationSAT, explanationTA;

        public string GetStem(Language lang) => lang switch
        {
            Language.Hindi => !string.IsNullOrEmpty(stemHI) ? stemHI : stemEN,
            Language.Santali => !string.IsNullOrEmpty(stemSAT) ? stemSAT : stemEN,
            Language.Tamil => !string.IsNullOrEmpty(stemTA) ? stemTA : stemEN,
            _ => stemEN,
        };

        public string[] GetOptions(Language lang) => lang switch
        {
            Language.Hindi => (optionsHI != null && optionsHI.Length > 0) ? optionsHI : optionsEN,
            Language.Santali => (optionsSAT != null && optionsSAT.Length > 0) ? optionsSAT : optionsEN,
            Language.Tamil => (optionsTA != null && optionsTA.Length > 0) ? optionsTA : optionsEN,
            _ => optionsEN,
        };

        public string GetExplanation(Language lang) => lang switch
        {
            Language.Hindi => !string.IsNullOrEmpty(explanationHI) ? explanationHI : explanationEN,
            Language.Santali => !string.IsNullOrEmpty(explanationSAT) ? explanationSAT : explanationEN,
            Language.Tamil => !string.IsNullOrEmpty(explanationTA) ? explanationTA : explanationEN,
            _ => explanationEN,
        };

        /// <summary>Flat Dictionary&lt;string,object&gt; ready for MiniJSON.Json.Serialize + FirestoreService.SaveQuestionBankItem.</summary>
        public Dictionary<string, object> ToFlatDict()
        {
            var tagList = new List<object>();
            if (tags != null) foreach (var t in tags) tagList.Add(t);

            return new Dictionary<string, object>
            {
                { "tags", tagList },
                { "difficulty", difficulty.ToString().ToLowerInvariant() },
                { "stemEN", stemEN }, { "stemHI", stemHI }, { "stemSAT", stemSAT }, { "stemTA", stemTA },
                { "optionsEN", ToObjectList(optionsEN) }, { "optionsHI", ToObjectList(optionsHI) },
                { "optionsSAT", ToObjectList(optionsSAT) }, { "optionsTA", ToObjectList(optionsTA) },
                { "correctIndex", correctIndex },
                { "explanationEN", explanationEN }, { "explanationHI", explanationHI },
                { "explanationSAT", explanationSAT }, { "explanationTA", explanationTA },
            };
        }

        static List<object> ToObjectList(string[] arr)
        {
            var list = new List<object>();
            if (arr != null) foreach (var s in arr) list.Add(s);
            return list;
        }

        /// <summary>Parses one raw Firestore wire-format fields dict (from ListCollection) into a QuestionBankItem.</summary>
        public static QuestionBankItem FromFirestoreDocument(Dictionary<string, object> doc)
        {
            if (doc == null) return null;

            string name = null;
            if (doc.TryGetValue("name", out var nameObj)) name = nameObj as string;
            var fields = doc.TryGetValue("fields", out var f) ? f as Dictionary<string, object> : doc;
            if (fields == null) return null;

            var item = new QuestionBankItem
            {
                id = !string.IsNullOrEmpty(name) ? name.Substring(name.LastIndexOf('/') + 1) : Guid.NewGuid().ToString("N"),
                difficulty = ParseDifficulty(FirestoreService.GetstringValue(fields, "difficulty")),
                stemEN = FirestoreService.GetstringValue(fields, "stemEN"),
                stemHI = FirestoreService.GetstringValue(fields, "stemHI"),
                stemSAT = FirestoreService.GetstringValue(fields, "stemSAT"),
                stemTA = FirestoreService.GetstringValue(fields, "stemTA"),
                correctIndex = FirestoreService.GetintValue(fields, "correctIndex"),
                explanationEN = FirestoreService.GetstringValue(fields, "explanationEN"),
                explanationHI = FirestoreService.GetstringValue(fields, "explanationHI"),
                explanationSAT = FirestoreService.GetstringValue(fields, "explanationSAT"),
                explanationTA = FirestoreService.GetstringValue(fields, "explanationTA"),
                tags = ParseStringArrayField(fields, "tags"),
                optionsEN = ParseStringArrayField(fields, "optionsEN"),
                optionsHI = ParseStringArrayField(fields, "optionsHI"),
                optionsSAT = ParseStringArrayField(fields, "optionsSAT"),
                optionsTA = ParseStringArrayField(fields, "optionsTA"),
            };
            return item;
        }

        static QuestionDifficulty ParseDifficulty(string s) => (s ?? "").ToLowerInvariant() switch
        {
            "hard" => QuestionDifficulty.Hard,
            "medium" => QuestionDifficulty.Medium,
            _ => QuestionDifficulty.Easy,
        };

        /// <summary>
        /// Plain string-array Firestore field extraction — FirestoreService.GetarrayValues() only
        /// handles arrays-of-maps, same gap FaceVerificationService.ParseFloatArrayField() hit for
        /// embeddingVector.
        /// </summary>
        static string[] ParseStringArrayField(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return Array.Empty<string>();
            if (v is Dictionary<string, object> d && d.TryGetValue("arrayValue", out var av) &&
                av is Dictionary<string, object> arrVal && arrVal.TryGetValue("values", out var valuesObj) &&
                valuesObj is List<object> values)
            {
                var result = new string[values.Count];
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i] is Dictionary<string, object> item && item.TryGetValue("stringValue", out var sv))
                        result[i] = sv as string ?? "";
                }
                return result;
            }
            return Array.Empty<string>();
        }
    }
}
