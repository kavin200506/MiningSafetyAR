using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.Data
{
    /// <summary>
    /// On-device face enrollment/verification, used as an extra check that the worker attempting
    /// a training session is the same person who was originally enrolled (anti-proxy / anti-
    /// impersonation for training-completion records), NOT a general biometric login replacement.
    ///
    /// PRIVACY / SECURITY — read before touching this file:
    ///   Raw camera frames NEVER leave the device. Only the MobileFaceNet embedding — a fixed-length
    ///   float vector with no direct visual meaning — is ever sent to Firestore. Every frame captured
    ///   from the camera (WebCamTexture snapshots, cropped/aligned 112x112 face crops) lives purely in
    ///   local memory/Texture2D objects for the duration of one enroll/verify call and is discarded
    ///   immediately after the embedding is computed. If you are adding new code here, do not add any
    ///   path that uploads a Texture2D, byte[] image, or WebCamTexture frame anywhere — only float[]
    ///   embeddings are allowed to cross the network boundary.
    ///
    /// Storage: workers/{uid}/private/faceData (NOT the main workers/{uid} profile document) — see
    /// FirestoreService.SaveFaceData/GetFaceData. Fields are documented next to BuildFaceDataFields().
    ///
    /// MODEL I/O CONTRACT:
    ///
    ///   Face detection — unity/inference-engine-blaze-face (MediaPipe BlazeFace "short range", verified
    ///   2026-09-10 against the actual downloaded blaze_face_short_range.onnx graph and against Unity's
    ///   own reference decode in inference-engine-samples/BlazeDetectionSample/Face):
    ///     input:  "input", shape [1, 128, 128, 3] — NHWC (channels LAST, not NCHW), float32, values
    ///             rescaled to [-1, 1] (BuildDetectorInputTensor does this rescale in C# on the CPU;
    ///             Unity's own sample instead splices a `2*x-1` op into the model graph via
    ///             Unity.InferenceEngine.Functional — mathematically identical result, simpler here
    ///             since we don't also need their GPU compute-shader affine sampler).
    ///     output: TWO separate tensors (not one merged tensor as originally assumed):
    ///               "regressors"     [1, 896, 16] — per anchor: [xOffset, yOffset, width, height,
    ///                                 kp0x, kp0y, kp1x, kp1y, ..., kp5x, kp5y] (6 keypoints × 2).
    ///                                 Box center = anchor position (from anchors.csv) + xOffset/yOffset,
    ///                                 in 128-space pixels; width/height are already absolute 128-space
    ///                                 pixels (not further anchor-scaled). Same formula for keypoints.
    ///               "classificators" [1, 896, 1]  — RAW logit per anchor; apply sigmoid to get 0..1
    ///                                 confidence (NOT already a probability).
    ///             Decoding requires anchors.csv (896 rows of x,y,w,h — w/h are always 1 and unused;
    ///             only x,y anchor-center offsets matter) shipped alongside the model on Hugging Face.
    ///             NMS is done in plain C# here (RunFaceDetection) rather than Unity's graph-level
    ///             Functional.NMS, since we only need "is there exactly one face" for a single still
    ///             frame, not real-time multi-face tracking.
    ///     KEYPOINTS: the 6 regressed keypoints are POSITIONS only (per MediaPipe's published ordering
    ///                for this model family: 0=right eye, 1=left eye, 2=nose tip, 3=mouth, 4=right ear
    ///                tragion, 5=left ear tragion — this ordering is sourced from MediaPipe's docs, NOT
    ///                independently re-derivable from the anonymous ONNX output channels themselves).
    ///                *** THIS MODEL HAS NO EYE-OPEN/CLOSED OR BLINK SIGNAL OF ANY KIND. *** The
    ///                blink-liveness design in this file's original spec assumed a per-eye "openness"
    ///                confidence that does not exist in BlazeFace's output (confirmed by reading the
    ///                raw ONNX graph AND Unity's own reference sample — neither exposes anything beyond
    ///                these 6 keypoint coordinates). `enableBlinkLiveness` is left OFF and the capture
    ///                pipeline does NOT gate on a blink — see CaptureFaceWithLiveness()'s comment for
    ///                what it does instead. Re-enabling real blink-based liveness needs a different
    ///                model (e.g. a MediaPipe FaceMesh/iris model with eyelid landmarks for an
    ///                eye-aspect-ratio calculation) — flagged back to the requester rather than faked.
    ///
    ///   MobileFaceNet embedding — qualcomm/MobileFaceNet (verified 2026-09-10 against the actual
    ///   mobile_facenet.onnx graph downloaded from the model's real asset host — the Hugging Face repo
    ///   qualcomm/MobileFaceNet is metadata-only and does not host the weights directly; its README
    ///   links straight to the QAI Hub release zip, which is what was actually inspected):
    ///     input:  TWO named inputs "img1" and "img2", each [1, 3, 112, 112] — NCHW (this one IS
    ///             channels-first, unlike BlazeFace), float32, value range [0, 1] confirmed by the
    ///             export's own metadata.json. This matches what this file already assumed, so no
    ///             NHWC-style fix was needed here — but the export shape is NOT what was assumed (see
    ///             below), and TextureConverter.ToTensor(Texture,int,int,int) used previously is also
    ///             obsolete in this package version in favour of ToTensor(Texture, Tensor, TextureTransform).
    ///     output: ONE tensor "embeddings", shape [2, 128] — NOT [1, embeddingDimensions] as originally
    ///             assumed. This is a PAIRWISE face-verification export (QAI Hub packages it this way
    ///             for their own LFW-style benchmark harness): both named inputs run through the same
    ///             shared-weight backbone independently (batched, no cross-input interaction — traced
    ///             through the raw ONNX graph to confirm: img1 and img2 are Sub/Div-normalized, flipped,
    ///             Concat'd along the batch axis, convolved as one batch, then Split back apart and
    ///             flip-TTA-summed per input before being Stacked into the [2,128] result), so row 0 is
    ///             purely img1's embedding and row 1 is purely img2's — there is no cross term.
    ///             RunEmbedding() exploits this: it feeds the SAME 112x112 face crop as both img1 and
    ///             img2 and reads only row 0. The flip-TTA sum this graph does internally leaves the
    ///             embedding un-normalized (roughly 2x a "plain" embedding's magnitude) — irrelevant
    ///             here since this file L2-normalizes every embedding before storing/comparing, and
    ///             cosine similarity is invariant to a uniform positive scaling anyway.
    ///     embeddingDimensions is 128 for this specific export (updated from the original 512 guess) —
    ///     EMBEDDING_MODEL_VERSION was bumped accordingly, which correctly forces re-enrollment for
    ///     anyone previously enrolled under the old placeholder version string.
    /// </summary>
    public class FaceVerificationService : MonoBehaviour
    {
        public static FaceVerificationService Instance { get; private set; }

        // Bump this string (and re-export a new MobileFaceNet model) any time the embedding model
        // changes — VerifyFace() refuses to compare across mismatched versions (see VerifyFaceRoutine).
        public const string EMBEDDING_MODEL_VERSION = "MobileFaceNet_QAIHub_v0.62.1_128d";
        public const string CONSENT_VERSION = "1.0";

        const int DETECTION_INPUT_SIZE = 128;   // BlazeFace short-range's verified input side length
        const int NUM_ANCHORS = 896;            // matches data/anchors.csv row count for this model
        const int NUM_KEYPOINTS = 6;
        const int REGRESSOR_STRIDE = 16;        // 4 box floats + 6 keypoints * 2
        const int RIGHT_EYE_KEYPOINT = 0;
        const int LEFT_EYE_KEYPOINT = 1;
        const int FACE_CROP_SIZE = 112;

        [Header("Inference Engine Models")]
        [Tooltip("unity/inference-engine-blaze-face — blaze_face_short_range.onnx. Output contract: see class-level MODEL I/O CONTRACT comment.")]
        [SerializeField] Unity.InferenceEngine.ModelAsset faceDetectionModel;
        [Tooltip("896-row anchors.csv shipped alongside the BlazeFace model on Hugging Face — required to decode regressor offsets into box/keypoint positions.")]
        [SerializeField] TextAsset detectionAnchorsCsv;
        [Tooltip("qualcomm/MobileFaceNet — mobile_facenet.onnx. Pairwise export (\"img1\"/\"img2\" -> [2,128] \"embeddings\"); see class-level MODEL I/O CONTRACT comment for how RunEmbedding() adapts this to a single-image call.")]
        [SerializeField] Unity.InferenceEngine.ModelAsset faceEmbeddingModel;
        [Tooltip("128 for the verified qualcomm/MobileFaceNet export.")]
        [SerializeField] int embeddingDimensions = 128;
        [SerializeField] Unity.InferenceEngine.BackendType inferenceBackend = Unity.InferenceEngine.BackendType.CPU;

        [Header("Verification Tuning")]
        [Tooltip("Minimum cosine similarity between stored and live embedding to accept a match. Explicitly tunable.")]
        [SerializeField] float verificationThreshold = 0.7f;
        [Tooltip("Minimum BlazeFace sigmoid confidence to accept a candidate face box. 0.5 matches Unity's own sample default.")]
        [SerializeField] float detectionConfidenceThreshold = 0.5f;
        [Tooltip("IoU above which two candidate boxes are treated as the same face during NMS. 0.3 matches Unity's own sample default.")]
        [SerializeField] float detectionIouThreshold = 0.3f;

        [Header("Capture Stability")]
        [Tooltip("Total time window to sample camera frames looking for a single, confidently-detected, stable face.")]
        [SerializeField] float captureWindowSeconds = 4f;
        [SerializeField] int captureSampleCount = 24;
        [Tooltip("Consecutive good (exactly-one-face, confident) samples required before accepting the frame, to reject one-off misdetections.")]
        [SerializeField] int minConsecutiveStableSamples = 3;

        [Header("Liveness (blink) — NOT CURRENTLY FUNCTIONAL")]
        [Tooltip("BlazeFace (the currently wired detector) only outputs 6 keypoint POSITIONS (eyes/nose/mouth/ears) — it has no eye-open/closed or blink signal, so there is nothing to gate a blink check on. Left OFF. Do not flip this on without first wiring a model that actually reports eye state (e.g. a FaceMesh/iris model) — see class-level MODEL I/O CONTRACT comment.")]
        [SerializeField] bool enableBlinkLiveness = false;

        [Header("Camera")]
        [SerializeField] int cameraRequestWidth = 480;
        [SerializeField] int cameraRequestHeight = 640;
        [SerializeField] float cameraInitTimeoutSeconds = 3f;

        Unity.InferenceEngine.Worker detectionWorker;
        Unity.InferenceEngine.Worker embeddingWorker;
        WebCamTexture webCamTexture;
        Texture2D frameBuffer;
        float[,] anchorsCache; // [NUM_ANCHORS, 4] = x,y,w,h (w/h always 1, unused — see LoadAnchorsIfNeeded)

        public float VerificationThreshold => verificationThreshold;

        struct FaceDetection
        {
            // All coordinates are normalized 0..1, top-left-origin (x right+, y DOWN+) — the standard
            // image/detector convention. See CropAndResize()'s comment for how this maps onto Unity's
            // native bottom-left-origin Texture2D pixel arrays.
            public float x, y, width, height;
            public float confidence;
            public Vector2 rightEye, leftEye; // positions only — no openness data, see class comment
        }

        [Serializable]
        public class FaceVerificationResult
        {
            public bool passed;
            public float similarityScore;
            // Placeholder pending a future iris-landmark-based implementation — always false today and
            // NOT a real liveness/anti-spoofing guarantee; do not treat a `false` (or a future `true`
            // without re-reading this comment) as evidence anti-spoofing is actually working.
            public bool livenessConfirmed;
            public string failureReason; // "", "not_enrolled", "model_version_mismatch_reenrollment_required",
                                          // "no_face_detected", "multiple_faces_detected", "capture_timed_out"
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            detectionWorker?.Dispose();
            embeddingWorker?.Dispose();
            if (webCamTexture != null) webCamTexture.Stop();
            if (frameBuffer != null) Destroy(frameBuffer);
            if (Instance == this) Instance = null;
        }

        void EnsureWorkers()
        {
            if (detectionWorker == null && faceDetectionModel != null)
            {
                var model = Unity.InferenceEngine.ModelLoader.Load(faceDetectionModel);
                detectionWorker = new Unity.InferenceEngine.Worker(model, inferenceBackend);
                Debug.Log("[DIAG] FaceVerificationService Face-detection Sentis worker created.");
            }
            if (embeddingWorker == null && faceEmbeddingModel != null)
            {
                var model = Unity.InferenceEngine.ModelLoader.Load(faceEmbeddingModel);
                embeddingWorker = new Unity.InferenceEngine.Worker(model, inferenceBackend);
                Debug.Log("[DIAG] FaceVerificationService MobileFaceNet Sentis worker created.");
            }
        }

        // ================================================================
        // PUBLIC API
        // ================================================================

        /// <summary>
        /// Records (or re-records, after a withdrawal) consent for face verification. Must be called
        /// from FaceConsentPageController before EnrollFace() is ever invoked for this worker.
        /// </summary>
        public void RecordConsent(string firebaseUid, string language, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(firebaseUid)) { onComplete?.Invoke(false, "missing_uid"); return; }
            StartCoroutine(RecordConsentRoutine(firebaseUid, language, onComplete));
        }

        public void EnrollFace(string firebaseUid, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(firebaseUid)) { onComplete?.Invoke(false, "missing_uid"); return; }
            StartCoroutine(EnrollFaceRoutine(firebaseUid, onComplete));
        }

        public void VerifyFace(string firebaseUid, Action<FaceVerificationResult> onComplete)
        {
            if (string.IsNullOrEmpty(firebaseUid))
            {
                onComplete?.Invoke(new FaceVerificationResult { passed = false, failureReason = "missing_uid" });
                return;
            }
            StartCoroutine(VerifyFaceRoutine(firebaseUid, onComplete));
        }

        /// <summary>
        /// Triggered from SettingsPageController's "Withdraw Face Data Consent" option. Deletes the
        /// embeddingVector field outright (not just a flag flip) and disables verification for this
        /// worker going forward, while keeping isActive:false / withdrawnAt as an audit record.
        /// </summary>
        public void WithdrawConsent(string firebaseUid, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(firebaseUid)) { onComplete?.Invoke(false, "missing_uid"); return; }
            StartCoroutine(WithdrawConsentRoutine(firebaseUid, onComplete));
        }

        public void HasActiveEnrollment(string firebaseUid, Action<bool> onResult)
        {
            if (string.IsNullOrEmpty(firebaseUid)) { onResult?.Invoke(false); return; }
            FirestoreService.Instance.GetFaceData(firebaseUid, (ok, json) =>
            {
                if (!ok) { onResult?.Invoke(false); return; }
                var fields = FirestoreService.ParseFirestoreFields(json);
                bool isActive = fields != null && FirestoreService.GetboolValue(fields, "isActive");
                bool hasVector = fields != null && ParseFloatArrayField(fields, "embeddingVector") != null;
                onResult?.Invoke(isActive && hasVector);
            });
        }

        // ================================================================
        // CONSENT
        // ================================================================

        IEnumerator RecordConsentRoutine(string firebaseUid, string language, Action<bool, string> onComplete)
        {
            Debug.Log($"[INFO] FaceVerificationService Recording face-data consent for worker {firebaseUid} (lang={language}).");

            var fields = new Dictionary<string, object>
            {
                { "consentGiven", true },
                { "consentTimestamp", NowIso() },
                { "consentLanguage", language ?? "English" },
                { "consentVersion", CONSENT_VERSION },
                { "isActive", false },          // not active until an embedding is actually enrolled
                { "embeddingModelVersion", "" },
                { "enrolledAt", null },
                { "lastVerifiedAt", null },
                { "verificationAttemptCount", 0 },
                { "withdrawnAt", null },
            };

            bool done = false; bool ok = false; string resp = "";
            SaveFaceDataFields(firebaseUid, fields, (o, r) => { ok = o; resp = r; done = true; });
            yield return new WaitUntil(() => done);

            if (ok) Debug.Log($"[INFO] FaceVerificationService Consent recorded for worker {firebaseUid}.");
            else Debug.LogWarning($"[WARN] FaceVerificationService Failed to record consent for worker {firebaseUid}: {resp}");

            onComplete?.Invoke(ok, resp);
        }

        // ================================================================
        // ENROLLMENT
        // ================================================================

        IEnumerator EnrollFaceRoutine(string firebaseUid, Action<bool, string> onComplete)
        {
            Debug.Log($"[INFO] FaceVerificationService EnrollFace started for worker {firebaseUid}.");

            bool getDone = false; bool getOk = false; string getJson = null;
            FirestoreService.Instance.GetFaceData(firebaseUid, (o, j) => { getOk = o; getJson = j; getDone = true; });
            yield return new WaitUntil(() => getDone);

            var existingFields = getOk ? FirestoreService.ParseFirestoreFields(getJson) : null;
            bool consentGiven = existingFields != null && FirestoreService.GetboolValue(existingFields, "consentGiven");
            if (!consentGiven)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService EnrollFace blocked — no consent on record for worker {firebaseUid}.");
                onComplete?.Invoke(false, "consent_required");
                yield break;
            }

            Texture2D face112 = null;
            bool capturedOk = false;
            string captureError = null;
            yield return CaptureFaceWithLiveness(result => { face112 = result.face; capturedOk = result.capturedOk; captureError = result.error; });

            if (face112 == null || !capturedOk)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService EnrollFace capture failed for worker {firebaseUid}: {captureError}");
                onComplete?.Invoke(false, captureError ?? "capture_failed");
                yield break;
            }

            float[] embedding = RunEmbedding(face112);
            Destroy(face112);

            if (embedding == null)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService EnrollFace embedding inference failed for worker {firebaseUid}.");
                onComplete?.Invoke(false, "embedding_failed");
                yield break;
            }

            var updated = ToPlainDict(existingFields) ?? new Dictionary<string, object>();
            updated["embeddingVector"] = FloatArrayToObjectList(embedding);
            updated["embeddingModelVersion"] = EMBEDDING_MODEL_VERSION;
            updated["enrolledAt"] = NowIso();
            updated["isActive"] = true;
            updated["withdrawnAt"] = null;
            updated["verificationAttemptCount"] = existingFields != null ? FirestoreService.GetintValue(existingFields, "verificationAttemptCount") : 0;

            bool saveDone = false; bool saveOk = false; string saveResp = "";
            SaveFaceDataFields(firebaseUid, updated, (o, r) => { saveOk = o; saveResp = r; saveDone = true; });
            yield return new WaitUntil(() => saveDone);

            Debug.Log($"[INFO] FaceVerificationService EnrollFace {(saveOk ? "succeeded" : "FAILED")} for worker {firebaseUid} (model={EMBEDDING_MODEL_VERSION}).");
            onComplete?.Invoke(saveOk, saveResp);
        }

        // ================================================================
        // VERIFICATION
        // ================================================================

        IEnumerator VerifyFaceRoutine(string firebaseUid, Action<FaceVerificationResult> onComplete)
        {
            Debug.Log($"[INFO] FaceVerificationService VerifyFace started for worker {firebaseUid}.");
            var result = new FaceVerificationResult();

            bool getDone = false; bool getOk = false; string getJson = null;
            FirestoreService.Instance.GetFaceData(firebaseUid, (o, j) => { getOk = o; getJson = j; getDone = true; });
            yield return new WaitUntil(() => getDone);

            var fields = getOk ? FirestoreService.ParseFirestoreFields(getJson) : null;
            float[] storedEmbedding = fields != null ? ParseFloatArrayField(fields, "embeddingVector") : null;
            bool isActive = fields != null && FirestoreService.GetboolValue(fields, "isActive");

            if (fields == null || storedEmbedding == null || !isActive)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService VerifyFace — no active enrollment for worker {firebaseUid}.");
                result.failureReason = "not_enrolled";
                onComplete?.Invoke(result);
                yield break;
            }

            string storedModelVersion = FirestoreService.GetstringValue(fields, "embeddingModelVersion");
            if (storedModelVersion != EMBEDDING_MODEL_VERSION)
            {
                Debug.LogWarning("[WARN] FaceVerificationService Stored embedding model version mismatch — re-enrollment required.");
                result.failureReason = "model_version_mismatch_reenrollment_required";
                onComplete?.Invoke(result);
                yield break;
            }

            Texture2D face112 = null;
            bool capturedOk = false;
            string captureError = null;
            yield return CaptureFaceWithLiveness(r => { face112 = r.face; capturedOk = r.capturedOk; captureError = r.error; });

            // Always false today — see CaptureResult/FaceVerificationResult comments: BlazeFace has no
            // blink/eye-state signal, so this is informational only and never gates `passed`.
            result.livenessConfirmed = false;

            int attemptCount = FirestoreService.GetintValue(fields, "verificationAttemptCount") + 1;
            var updated = ToPlainDict(fields);
            updated["verificationAttemptCount"] = attemptCount;
            updated["lastVerifiedAt"] = NowIso();

            if (face112 == null || !capturedOk)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService VerifyFace capture/liveness failed for worker {firebaseUid}: {captureError}");
                result.failureReason = captureError ?? "capture_failed";
                SaveFaceDataFields(firebaseUid, updated, null); // still record the attempt
                onComplete?.Invoke(result);
                yield break;
            }

            float[] liveEmbedding = RunEmbedding(face112);
            Destroy(face112);

            float score = CosineSimilarity(storedEmbedding, liveEmbedding);
            bool passed = liveEmbedding != null && score >= verificationThreshold;

            result.similarityScore = score;
            result.passed = passed;
            if (!passed && string.IsNullOrEmpty(result.failureReason)) result.failureReason = "similarity_below_threshold";

            // Required trace line — kept byte-for-byte so mismatches are greppable during demo/testing.
            Debug.Log($"[INFO] FaceVerificationService Verification similarity={score:F3}, threshold={verificationThreshold}, result={passed}");

            bool saveDone = false;
            SaveFaceDataFields(firebaseUid, updated, (o, r) => saveDone = true);
            yield return new WaitUntil(() => saveDone);

            onComplete?.Invoke(result);
        }

        // ================================================================
        // CONSENT WITHDRAWAL
        // ================================================================

        IEnumerator WithdrawConsentRoutine(string firebaseUid, Action<bool, string> onComplete)
        {
            bool getDone = false; bool getOk = false; string getJson = null;
            FirestoreService.Instance.GetFaceData(firebaseUid, (o, j) => { getOk = o; getJson = j; getDone = true; });
            yield return new WaitUntil(() => getDone);

            var fields = getOk ? FirestoreService.ParseFirestoreFields(getJson) : null;
            var updated = ToPlainDict(fields) ?? new Dictionary<string, object>();

            // Explicitly delete the embedding — PatchDocument writes without an updateMask fully
            // replace the document, so simply omitting the key from `updated` is what removes it.
            updated.Remove("embeddingVector");
            updated["isActive"] = false;
            updated["withdrawnAt"] = NowIso();
            updated["consentGiven"] = false; // re-enrolling later requires going through consent again

            bool saveDone = false; bool saveOk = false; string saveResp = "";
            SaveFaceDataFields(firebaseUid, updated, (o, r) => { saveOk = o; saveResp = r; saveDone = true; });
            yield return new WaitUntil(() => saveDone);

            if (saveOk)
                Debug.Log($"[INFO] FaceVerificationService Face embedding deleted per consent withdrawal for worker {firebaseUid}, audit record retained.");
            else
                Debug.LogWarning($"[WARN] FaceVerificationService Consent withdrawal save failed for worker {firebaseUid}: {saveResp}");

            onComplete?.Invoke(saveOk, saveResp);
        }

        // ================================================================
        // CAPTURE + LIVENESS (blink) PIPELINE
        // ================================================================

        struct CaptureResult
        {
            public Texture2D face;
            public bool capturedOk;
            public string error;
        }

        /// <summary>
        /// Shared by EnrollFace and VerifyFace: starts the camera, samples frames over the capture
        /// window, and returns a cropped+resized 112x112 face texture once exactly one face has been
        /// confidently and consistently detected for `minConsecutiveStableSamples` samples in a row
        /// (guards against accepting a single fluky misdetection). Caller owns/destroys the returned
        /// Texture2D.
        ///
        /// NOTE ON LIVENESS: this does NOT perform a blink check. The originally-specified design asked
        /// for one, but the BlazeFace model actually wired up here (see class-level MODEL I/O CONTRACT
        /// comment) reports only 6 keypoint *positions* — no eye-open/closed classification exists to
        /// gate on. Rather than fabricate a proxy signal, this was flagged back to the requester;
        /// `enableBlinkLiveness` stays off and FaceVerificationResult.livenessConfirmed stays false.
        /// </summary>
        IEnumerator CaptureFaceWithLiveness(Action<CaptureResult> callback)
        {
            EnsureWorkers();
            LoadAnchorsIfNeeded();
            if (detectionWorker == null || embeddingWorker == null)
            {
                Debug.LogWarning("[WARN] FaceVerificationService Inference Engine models not assigned in Inspector.");
                callback(new CaptureResult { error = "models_not_configured" });
                yield break;
            }
            if (anchorsCache == null)
            {
                Debug.LogWarning("[WARN] FaceVerificationService detectionAnchorsCsv not assigned or failed to parse.");
                callback(new CaptureResult { error = "anchors_not_configured" });
                yield break;
            }
            if (enableBlinkLiveness)
            {
                Debug.LogWarning("[WARN] FaceVerificationService enableBlinkLiveness is on but no blink signal is implemented — ignoring.");
            }

            yield return EnsureCamera();
            if (webCamTexture == null || webCamTexture.width <= 16)
            {
                callback(new CaptureResult { error = "camera_unavailable" });
                yield break;
            }

            Debug.Log("[INFO] FaceVerificationService Capture started — please look directly at the camera.");

            float interval = captureWindowSeconds / Mathf.Max(1, captureSampleCount);
            int consecutiveGood = 0;
            Texture2D lastGoodFrame = null;
            FaceDetection lastGoodDetection = default;
            string failReason = null;

            for (int i = 0; i < captureSampleCount && consecutiveGood < minConsecutiveStableSamples; i++)
            {
                yield return new WaitForSeconds(interval);

                frameBuffer = SnapshotFrame(frameBuffer);
                var detections = RunFaceDetection(frameBuffer);

                if (detections.Count == 0)
                {
                    failReason = "no_face_detected";
                    consecutiveGood = 0;
                    Debug.Log("[DIAG] FaceVerificationService Sample frame: no face detected.");
                    continue;
                }
                if (detections.Count > 1)
                {
                    failReason = "multiple_faces_detected";
                    consecutiveGood = 0;
                    Debug.Log($"[DIAG] FaceVerificationService Sample frame: {detections.Count} faces detected, need exactly one.");
                    continue;
                }

                failReason = null;
                consecutiveGood++;
                lastGoodDetection = detections[0];

                if (lastGoodFrame == null || lastGoodFrame.width != frameBuffer.width || lastGoodFrame.height != frameBuffer.height)
                {
                    if (lastGoodFrame != null) Destroy(lastGoodFrame);
                    lastGoodFrame = new Texture2D(frameBuffer.width, frameBuffer.height, TextureFormat.RGB24, false);
                }
                lastGoodFrame.SetPixels32(frameBuffer.GetPixels32());
                lastGoodFrame.Apply(false);

                Debug.Log($"[DIAG] FaceVerificationService Sample frame: 1 face, confidence={lastGoodDetection.confidence:F3}, stable={consecutiveGood}/{minConsecutiveStableSamples}.");
            }

            if (consecutiveGood < minConsecutiveStableSamples)
            {
                if (lastGoodFrame != null) Destroy(lastGoodFrame);
                string reason = failReason ?? "capture_timed_out";
                Debug.LogWarning($"[WARN] FaceVerificationService Capture failed: {reason}");
                callback(new CaptureResult { error = reason });
                yield break;
            }

            var cropped = CropAndResize(lastGoodFrame, NormalizedBoxWithMargin(lastGoodDetection), FACE_CROP_SIZE);
            Destroy(lastGoodFrame);

            callback(new CaptureResult { face = cropped, capturedOk = true, error = null });
        }

        IEnumerator EnsureCamera()
        {
            if (webCamTexture != null && webCamTexture.isPlaying) yield break;

            string frontDevice = null;
            foreach (var d in WebCamTexture.devices)
            {
                if (d.isFrontFacing) { frontDevice = d.name; break; }
            }

            webCamTexture = !string.IsNullOrEmpty(frontDevice)
                ? new WebCamTexture(frontDevice, cameraRequestWidth, cameraRequestHeight)
                : new WebCamTexture(cameraRequestWidth, cameraRequestHeight);
            webCamTexture.Play();

            float elapsed = 0f;
            while (webCamTexture.width <= 16 && elapsed < cameraInitTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (webCamTexture.width <= 16)
                Debug.LogWarning("[WARN] FaceVerificationService Camera failed to initialize within timeout.");
            else
                Debug.Log($"[DIAG] FaceVerificationService Camera ready ({webCamTexture.width}x{webCamTexture.height}).");
        }

        Texture2D SnapshotFrame(Texture2D reuse)
        {
            if (reuse == null || reuse.width != webCamTexture.width || reuse.height != webCamTexture.height)
            {
                if (reuse != null) Destroy(reuse);
                reuse = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            }
            reuse.SetPixels32(webCamTexture.GetPixels32());
            reuse.Apply(false);
            return reuse;
        }

        // ================================================================
        // SENTIS INFERENCE
        // ================================================================

        /// <summary>
        /// Parses data/anchors.csv (896 rows of "x,y,w,h", w/h always 1 and unused) shipped alongside
        /// unity/inference-engine-blaze-face on Hugging Face. Only the x,y anchor-center columns are
        /// used, per BlazeUtils.LoadAnchors in Unity's own reference sample.
        /// </summary>
        void LoadAnchorsIfNeeded()
        {
            if (anchorsCache != null) return;
            if (detectionAnchorsCsv == null) return;

            var lines = detectionAnchorsCsv.text.Split('\n');
            if (lines.Length < NUM_ANCHORS)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService detectionAnchorsCsv has {lines.Length} lines, expected {NUM_ANCHORS} — refusing to use it.");
                return;
            }

            var anchors = new float[NUM_ANCHORS, 4];
            for (int i = 0; i < NUM_ANCHORS; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 4) { Debug.LogWarning($"[WARN] FaceVerificationService anchors.csv line {i} malformed."); return; }
                for (int j = 0; j < 4; j++)
                    anchors[i, j] = float.Parse(parts[j], CultureInfo.InvariantCulture);
            }
            anchorsCache = anchors;
            Debug.Log("[DIAG] FaceVerificationService Loaded 896 BlazeFace anchors.");
        }

        /// <summary>
        /// Runs BlazeFace and returns confidently-detected faces, most-confident first, after greedy
        /// IoU-based NMS. All returned FaceDetection coordinates are normalized top-left-origin (0..1).
        /// </summary>
        List<FaceDetection> RunFaceDetection(Texture2D frame)
        {
            var results = new List<FaceDetection>();

            using var inputTensor = BuildDetectorInputTensor(frame, DETECTION_INPUT_SIZE);
            detectionWorker.Schedule(inputTensor);

            // Disambiguate by shape rather than trust output index ordering: regressors' last dim is
            // 16, classificators' last dim is 1 (both confirmed against the actual downloaded ONNX
            // graph's declared output shapes: regressors [1,896,16], classificators [1,896,1]).
            Unity.InferenceEngine.Tensor<float> regressorsOut = null, scoresOut = null;
            for (int oi = 0; oi < 2; oi++)
            {
                var t = detectionWorker.PeekOutput(oi) as Unity.InferenceEngine.Tensor<float>;
                if (t == null) continue;
                if (t.shape[2] == REGRESSOR_STRIDE) regressorsOut = t;
                else if (t.shape[2] == 1) scoresOut = t;
            }
            if (regressorsOut == null || scoresOut == null)
            {
                Debug.LogWarning("[WARN] FaceVerificationService Unexpected detector output shapes — check faceDetectionModel is blaze_face_short_range.onnx.");
                return results;
            }

            using var regCpu = regressorsOut.ReadbackAndClone();
            using var scoreCpu = scoresOut.ReadbackAndClone();
            float[] reg = regCpu.DownloadToArray();     // length NUM_ANCHORS * REGRESSOR_STRIDE
            float[] rawScores = scoreCpu.DownloadToArray(); // length NUM_ANCHORS, raw logits

            var candidates = new List<FaceDetection>();
            for (int i = 0; i < NUM_ANCHORS; i++)
            {
                float confidence = Sigmoid(rawScores[i]);
                if (confidence < detectionConfidenceThreshold) continue;

                int b = i * REGRESSOR_STRIDE;
                float anchorX = anchorsCache[i, 0] * DETECTION_INPUT_SIZE;
                float anchorY = anchorsCache[i, 1] * DETECTION_INPUT_SIZE;

                float xCenterPx = reg[b + 0] + anchorX;
                float yCenterPx = reg[b + 1] + anchorY;
                float widthPx = reg[b + 2];
                float heightPx = reg[b + 3];

                candidates.Add(new FaceDetection
                {
                    x = (xCenterPx - 0.5f * widthPx) / DETECTION_INPUT_SIZE,
                    y = (yCenterPx - 0.5f * heightPx) / DETECTION_INPUT_SIZE,
                    width = widthPx / DETECTION_INPUT_SIZE,
                    height = heightPx / DETECTION_INPUT_SIZE,
                    confidence = confidence,
                    rightEye = DecodeKeypoint(reg, b, anchorX, anchorY, RIGHT_EYE_KEYPOINT),
                    leftEye = DecodeKeypoint(reg, b, anchorX, anchorY, LEFT_EYE_KEYPOINT),
                });
            }

            candidates.Sort((a, c) => c.confidence.CompareTo(a.confidence));
            foreach (var candidate in candidates)
            {
                bool overlapsExisting = false;
                foreach (var accepted in results)
                {
                    if (IoU(candidate, accepted) > detectionIouThreshold) { overlapsExisting = true; break; }
                }
                if (!overlapsExisting) results.Add(candidate);
            }
            return results;
        }

        static Vector2 DecodeKeypoint(float[] reg, int regressorBaseIndex, float anchorXPx, float anchorYPx, int keypointIndex)
        {
            int o = regressorBaseIndex + 4 + 2 * keypointIndex;
            float xPx = reg[o + 0] + anchorXPx;
            float yPx = reg[o + 1] + anchorYPx;
            return new Vector2(xPx / DETECTION_INPUT_SIZE, yPx / DETECTION_INPUT_SIZE);
        }

        static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));

        static float IoU(FaceDetection a, FaceDetection b)
        {
            float ax1 = a.x + a.width, ay1 = a.y + a.height;
            float bx1 = b.x + b.width, by1 = b.y + b.height;
            float ix0 = Mathf.Max(a.x, b.x), iy0 = Mathf.Max(a.y, b.y);
            float ix1 = Mathf.Min(ax1, bx1), iy1 = Mathf.Min(ay1, by1);
            float iw = Mathf.Max(0f, ix1 - ix0), ih = Mathf.Max(0f, iy1 - iy0);
            float intersection = iw * ih;
            float union = a.width * a.height + b.width * b.height - intersection;
            return union <= 0f ? 0f : intersection / union;
        }

        /// <summary>
        /// Builds a [1, size, size, 3] NHWC tensor (values rescaled to [-1,1]) from `source`, squashed
        /// (not letterboxed) to size x size — fine for a single still enrollment/verification snapshot,
        /// unlike Unity's own real-time sample this does not need aspect-ratio-preserving letterboxing
        /// or in-plane rotation correction.
        ///
        /// Unity's Texture2D.GetPixels() is bottom-left-origin (row 0 = bottom of image), but BlazeFace
        /// (like virtually every vision model) expects a top-left-origin, row-0-is-top image buffer —
        /// so this flips during sampling. RunFaceDetection's decoded box/keypoint coordinates come out
        /// in that same top-left-origin space as a result; CropAndResize() converts back the other way.
        /// </summary>
        static Unity.InferenceEngine.Tensor<float> BuildDetectorInputTensor(Texture2D source, int outSize)
        {
            Color[] srcPixels = source.GetPixels(); // bottom-left-origin
            int sw = source.width, sh = source.height;
            var data = new float[outSize * outSize * 3];

            int i = 0;
            for (int oy = 0; oy < outSize; oy++)
            {
                float vTopDown = (oy + 0.5f) / outSize * sh;
                float vBottomUp = sh - vTopDown; // flip into Texture2D's native bottom-left-origin v
                for (int ox = 0; ox < outSize; ox++)
                {
                    float u = (ox + 0.5f) / outSize * sw;
                    Color c = BilinearSample(srcPixels, sw, sh, u, vBottomUp);
                    data[i++] = c.r * 2f - 1f;
                    data[i++] = c.g * 2f - 1f;
                    data[i++] = c.b * 2f - 1f;
                }
            }
            return new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, outSize, outSize, 3), data);
        }

        /// <summary>
        /// Runs qualcomm/MobileFaceNet's exported graph, which expects TWO named inputs ("img1","img2")
        /// and returns a combined [2,128] "embeddings" tensor (row 0 = img1's embedding, row 1 = img2's
        /// — see class-level MODEL I/O CONTRACT comment for why this is safe to exploit: the two
        /// branches never interact). Since we only ever have one face crop to embed, the same crop is
        /// fed as both inputs and only row 0 is read.
        /// </summary>
        float[] RunEmbedding(Texture2D face112)
        {
            // NCHW, [0,1] — TextureConverter's default TensorLayout is NCHW and its default CoordOrigin
            // is TopLeft (i.e. it already accounts for Texture2D's native bottom-left storage), which is
            // exactly what this model needs, so both are set explicitly here for clarity/future-proofing
            // rather than left as unstated defaults. NOTE: unlike BuildDetectorInputTensor(), this uses
            // the modern non-obsolete ToTensor(Texture, Tensor, TextureTransform) overload — the
            // (Texture,int,int,int) overload used previously is deprecated in this package version.
            using var inputTensor = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 3, FACE_CROP_SIZE, FACE_CROP_SIZE));
            var transform = new Unity.InferenceEngine.TextureTransform()
                .SetTensorLayout(Unity.InferenceEngine.TensorLayout.NCHW)
                .SetCoordOrigin(Unity.InferenceEngine.CoordOrigin.TopLeft);
            Unity.InferenceEngine.TextureConverter.ToTensor(face112, inputTensor, transform);

            embeddingWorker.SetInput("img1", inputTensor);
            embeddingWorker.SetInput("img2", inputTensor);
            embeddingWorker.Schedule();

            var output = embeddingWorker.PeekOutput("embeddings") as Unity.InferenceEngine.Tensor<float>;
            if (output == null) return null;
            using var cpuCopy = output.ReadbackAndClone();
            float[] both = cpuCopy.DownloadToArray(); // [2,128] flattened -> length 2*embeddingDimensions

            if (both.Length < embeddingDimensions)
            {
                Debug.LogWarning($"[WARN] FaceVerificationService MobileFaceNet output length {both.Length} < expected {embeddingDimensions} — check faceEmbeddingModel/embeddingDimensions match.");
                return null;
            }

            var embedding = new float[embeddingDimensions];
            Array.Copy(both, 0, embedding, 0, embeddingDimensions); // row 0 = img1's embedding

            // L2-normalize — also washes out the ~2x magnitude from this graph's internal flip-TTA sum.
            double normSq = 0;
            for (int i = 0; i < embedding.Length; i++) normSq += embedding[i] * embedding[i];
            float norm = (float)Math.Sqrt(normSq);
            if (norm > 1e-6f)
                for (int i = 0; i < embedding.Length; i++) embedding[i] /= norm;

            return embedding;
        }

        // ================================================================
        // IMAGE UTILITIES
        // ================================================================

        static Rect NormalizedBoxWithMargin(FaceDetection det, float margin = 0.2f)
        {
            float mx = det.width * margin;
            float my = det.height * margin;
            float x = Mathf.Clamp01(det.x - mx * 0.5f);
            float y = Mathf.Clamp01(det.y - my * 0.5f);
            float w = Mathf.Clamp(det.width + mx, 0.01f, 1f - x);
            float h = Mathf.Clamp(det.height + my, 0.01f, 1f - y);
            return new Rect(x, y, w, h);
        }

        /// <summary>
        /// Axis-aligned crop + bilinear resize to outSize x outSize. `normalizedCrop` is top-left-origin
        /// (matching FaceDetection's convention and RunFaceDetection's decoded boxes), but
        /// Texture2D.GetPixels(x,y,w,h) measures y from the BOTTOM of the texture — so only the Y
        /// OFFSET needs converting here (source.height - top - height); the pixel block itself is read
        /// and written in Unity's native bottom-up order throughout (SetPixels expects the same
        /// bottom-up order back), so no per-pixel flip is needed, unlike BuildDetectorInputTensor()
        /// which genuinely resamples the whole frame into the opposite (top-down) orientation.
        ///
        /// NOTE: this is a simplified axis-aligned crop, not a full similarity-transform alignment using
        /// the now-available rightEye/leftEye keypoints (which would also correct in-plane head tilt) —
        /// acceptable for this project's current scope; revisit if verification accuracy suffers with
        /// tilted heads.
        /// </summary>
        static Texture2D CropAndResize(Texture2D source, Rect normalizedCrop, int outSize)
        {
            int srcX0 = Mathf.Clamp(Mathf.RoundToInt(normalizedCrop.x * source.width), 0, source.width - 1);
            int srcY0 = Mathf.Clamp(Mathf.RoundToInt(source.height * (1f - normalizedCrop.y - normalizedCrop.height)), 0, source.height - 1);
            int srcW = Mathf.Clamp(Mathf.RoundToInt(normalizedCrop.width * source.width), 1, source.width - srcX0);
            int srcH = Mathf.Clamp(Mathf.RoundToInt(normalizedCrop.height * source.height), 1, source.height - srcY0);

            Color[] cropped = source.GetPixels(srcX0, srcY0, srcW, srcH);
            var result = new Texture2D(outSize, outSize, TextureFormat.RGB24, false);
            var resized = new Color[outSize * outSize];

            for (int y = 0; y < outSize; y++)
            {
                float v = (y + 0.5f) / outSize * srcH;
                for (int x = 0; x < outSize; x++)
                {
                    float u = (x + 0.5f) / outSize * srcW;
                    resized[y * outSize + x] = BilinearSample(cropped, srcW, srcH, u, v);
                }
            }
            result.SetPixels(resized);
            result.Apply(false);
            return result;
        }

        static Color BilinearSample(Color[] pixels, int w, int h, float u, float v)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1);
            int y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = u - x0;
            float fy = v - y0;

            Color c00 = pixels[y0 * w + x0];
            Color c10 = pixels[y0 * w + x1];
            Color c01 = pixels[y1 * w + x0];
            Color c11 = pixels[y1 * w + x1];

            Color top = Color.Lerp(c00, c10, fx);
            Color bottom = Color.Lerp(c01, c11, fx);
            return Color.Lerp(top, bottom, fy);
        }

        static float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return -1f;
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
                normA += (double)a[i] * a[i];
                normB += (double)b[i] * b[i];
            }
            if (normA <= 0 || normB <= 0) return -1f;
            return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        // ================================================================
        // FIRESTORE FIELD HELPERS
        // ================================================================

        static string NowIso() => DateTime.UtcNow.ToString("o");

        static List<object> FloatArrayToObjectList(float[] arr)
        {
            var list = new List<object>(arr.Length);
            foreach (var f in arr) list.Add((double)f);
            return list;
        }

        /// <summary>
        /// FirestoreService.GetarrayValues() only extracts arrays-of-maps; embeddingVector is a plain
        /// array of numbers, so it needs its own extraction straight from the raw wire-format fields.
        /// </summary>
        static float[] ParseFloatArrayField(Dictionary<string, object> fields, string key)
        {
            if (fields == null || !fields.TryGetValue(key, out var v)) return null;
            if (v is Dictionary<string, object> d && d.TryGetValue("arrayValue", out var av) &&
                av is Dictionary<string, object> arrVal && arrVal.TryGetValue("values", out var valuesObj) &&
                valuesObj is List<object> values)
            {
                var result = new float[values.Count];
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i] is Dictionary<string, object> item)
                    {
                        if (item.TryGetValue("doubleValue", out var dv)) result[i] = Convert.ToSingle(dv);
                        else if (item.TryGetValue("integerValue", out var iv)) result[i] = Convert.ToSingle(iv.ToString());
                    }
                }
                return result;
            }
            return null;
        }

        /// <summary>
        /// Converts a raw Firestore wire-format fields dict (as returned by ParseFirestoreFields) back
        /// into a plain flat Dictionary&lt;string, object&gt; suitable for re-serializing via
        /// MiniJSON.Json.Serialize + FirestoreService's PatchDocument (which itself re-wraps plain
        /// JSON into wire format). Needed because PatchDocument writes are full-document replacements
        /// (no updateMask) — every save must round-trip the fields we're not touching, or they vanish.
        /// </summary>
        static Dictionary<string, object> ToPlainDict(Dictionary<string, object> firestoreFields)
        {
            if (firestoreFields == null) return null;
            var plain = new Dictionary<string, object>();
            foreach (var kv in firestoreFields)
            {
                string key = kv.Key;
                if (key == "embeddingVector")
                {
                    var arr = ParseFloatArrayField(firestoreFields, key);
                    plain[key] = arr != null ? FloatArrayToObjectList(arr) : null;
                    continue;
                }
                if (kv.Value is Dictionary<string, object> wrapped)
                {
                    if (wrapped.TryGetValue("nullValue", out _)) plain[key] = null;
                    else if (wrapped.TryGetValue("booleanValue", out var bv)) plain[key] = bv;
                    else if (wrapped.TryGetValue("integerValue", out var iv)) plain[key] = Convert.ToInt32(iv.ToString());
                    else if (wrapped.TryGetValue("doubleValue", out var dv)) plain[key] = Convert.ToSingle(dv);
                    else if (wrapped.TryGetValue("stringValue", out var sv)) plain[key] = sv;
                    else plain[key] = FirestoreService.GetstringValue(firestoreFields, key);
                }
            }
            return plain;
        }

        void SaveFaceDataFields(string firebaseUid, Dictionary<string, object> fields, Action<bool, string> cb)
        {
            string flatJson = MiniJSON.Json.Serialize(fields);
            FirestoreService.Instance.SaveFaceData(firebaseUid, flatJson, cb);
        }
    }
}
