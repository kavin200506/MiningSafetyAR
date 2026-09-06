using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Firebase;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MiningSafetyAR.Data
{
    [Serializable]
    public class LocationDataPayload
    {
        public double latitude;
        public double longitude;
        public float horizontalAccuracy;
        public string locationName;
        public bool hasLocation;
        public bool capturedOffline;

        public LocationDataPayload()
        {
            hasLocation = false;
            capturedOffline = false;
            locationName = "Location Unspecified";
        }

        public LocationDataPayload(double lat, double lng, float accuracy, bool offline, string addressName = null)
        {
            latitude = lat;
            longitude = lng;
            horizontalAccuracy = accuracy;
            hasLocation = true;
            capturedOffline = offline;
            locationName = !string.IsNullOrEmpty(addressName) ? addressName : string.Format(System.Globalization.CultureInfo.InvariantCulture, "Regional Mining Sector ({0:F3}°, {1:F3}°)", lat, lng);
        }
    }

    /// <summary>
    /// Captures GPS location data once at module start for regional training analytics.
    /// Never continuous tracking. Gated by user consent stored in PlayerPrefs.
    /// </summary>
    public class TrainingLocationCapture : MonoBehaviour
    {
        public static TrainingLocationCapture Instance { get; private set; }

        private const string CONSENT_PREF_KEY = "LocationConsentGranted";
        private const string CONSENT_PROMPTED_KEY = "LocationConsentPrompted";
        private const float TIMEOUT_SECONDS = 8.0f;
        private const float POLL_INTERVAL_SECONDS = 0.5f;

        public static bool HasConsentBeenPrompted => PlayerPrefs.GetInt(CONSENT_PROMPTED_KEY, 0) == 1;

        public static bool HasUserConsented
        {
            get => PlayerPrefs.GetInt(CONSENT_PREF_KEY, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(CONSENT_PREF_KEY, value ? 1 : 0);
                PlayerPrefs.SetInt(CONSENT_PROMPTED_KEY, 1);
                PlayerPrefs.Save();
                Debug.Log($"[TrainingLocationCapture] User location consent updated: {value}");
            }
        }

        public LocationDataPayload LatestLocationPayload { get; private set; }
        private bool isCapturing = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Ensures a persistent instance exists in the scene.
        /// </summary>
        public static TrainingLocationCapture EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("TrainingLocationCapture");
                Instance = go.AddComponent<TrainingLocationCapture>();
            }
            return Instance;
        }

        /// <summary>
        /// Captures location as a coroutine before triggering completion callback.
        /// </summary>
        public void CaptureLocation(Action<LocationDataPayload> onComplete, Action<string> onSubStatusUpdate = null)
        {
            StartCoroutine(CaptureLocationCoroutine(onComplete, onSubStatusUpdate));
        }

        private IEnumerator CaptureLocationCoroutine(Action<LocationDataPayload> onComplete, Action<string> onSubStatusUpdate)
        {
            if (isCapturing)
            {
                Debug.LogWarning("WARN TrainingLocationCapture Location capture already in progress!");
                onComplete?.Invoke(LatestLocationPayload);
                yield break;
            }

            isCapturing = true;

            // 1. Check user consent gate
            if (!HasUserConsented)
            {
                string reason = "User location consent not granted";
                Debug.LogWarning("WARN TrainingLocationCapture Location unavailable — proceeding without geo-tagging. Reason: " + reason);
                LatestLocationPayload = new LocationDataPayload();
                isCapturing = false;
                onComplete?.Invoke(LatestLocationPayload);
                yield break;
            }

            onSubStatusUpdate?.Invoke("Capturing location for regional training analytics...");

            // 2. Android Runtime Permission Check
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log("DIAG TrainingLocationCapture Requesting ACCESS_FINE_LOCATION Android permission...");
                Permission.RequestUserPermission(Permission.FineLocation);
                float permWait = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && permWait < 3.0f)
                {
                    permWait += 0.5f;
                    yield return new WaitForSeconds(0.5f);
                }

                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    string reason = "ACCESS_FINE_LOCATION permission denied by user";
                    Debug.LogWarning("WARN TrainingLocationCapture Location unavailable — proceeding without geo-tagging. Reason: " + reason);
                    LatestLocationPayload = new LocationDataPayload();
                    isCapturing = false;
                    onComplete?.Invoke(LatestLocationPayload);
                    yield break;
                }
            }
#endif

            // 3. Verify location service enabled on device
            if (!Input.location.isEnabledByUser)
            {
                string reason = "Location services disabled on device by user";
                Debug.LogWarning("WARN TrainingLocationCapture Location unavailable — proceeding without geo-tagging. Reason: " + reason);
                LatestLocationPayload = new LocationDataPayload();
                isCapturing = false;
                onComplete?.Invoke(LatestLocationPayload);
                yield break;
            }

            // 4. Start Location Service (desiredAccuracyInMeters: 50f, updateDistanceInMeters: 50f)
            Input.location.Start(desiredAccuracyInMeters: 50f, updateDistanceInMeters: 50f);
            Debug.Log("DIAG TrainingLocationCapture Location service started");

            float elapsedTime = 0f;
            while (Input.location.status == LocationServiceStatus.Initializing && elapsedTime < TIMEOUT_SECONDS)
            {
                yield return new WaitForSeconds(POLL_INTERVAL_SECONDS);
                elapsedTime += POLL_INTERVAL_SECONDS;
            }

            // 5. Evaluate result or timeout
            if (Input.location.status == LocationServiceStatus.Running)
            {
                var data = Input.location.lastData;
                bool isOffline = Application.internetReachability == NetworkReachability.NotReachable;

                onSubStatusUpdate?.Invoke("Resolving regional training address...");

                string resolvedAddress = null;
                if (!isOffline)
                {
                    yield return StartCoroutine(ReverseGeocodeCoroutine(data.latitude, data.longitude, (addr) => resolvedAddress = addr));
                }

                if (string.IsNullOrEmpty(resolvedAddress))
                {
                    resolvedAddress = FormatFallbackLocation(data.latitude, data.longitude);
                }

                LatestLocationPayload = new LocationDataPayload(data.latitude, data.longitude, data.horizontalAccuracy, isOffline, resolvedAddress);

                Debug.Log(string.Format("INFO TrainingLocationCapture Location resolved: lat={0}, lng={1}, accuracy={2}m, address='{3}'",
                    data.latitude, data.longitude, data.horizontalAccuracy, resolvedAddress));

                Input.location.Stop();
            }
            else
            {
                string reason;
                if (elapsedTime >= TIMEOUT_SECONDS)
                {
                    reason = "Location capture timed out after 8s";
                    Debug.LogWarning("WARN TrainingLocationCapture Location capture timed out after 8s");
                }
                else
                {
                    reason = $"Location service status: {Input.location.status}";
                }

                Debug.LogWarning("WARN TrainingLocationCapture Location unavailable — proceeding without geo-tagging. Reason: " + reason);
                Input.location.Stop();
                LatestLocationPayload = new LocationDataPayload();
            }

            isCapturing = false;
            onComplete?.Invoke(LatestLocationPayload);
        }

        public IEnumerator ReverseGeocodeCoroutine(double lat, double lng, Action<string> onAddressResolved)
        {
            string urlNominatim = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "https://nominatim.openstreetmap.org/reverse?format=json&lat={0}&lon={1}&zoom=18&addressdetails=1",
                lat, lng);

            string resolvedAddress = null;

            using (UnityEngine.Networking.UnityWebRequest req = UnityEngine.Networking.UnityWebRequest.Get(urlNominatim))
            {
                req.SetRequestHeader("User-Agent", "MiningSafetyAR/1.0 (contact@miningsafety.app)");
                req.timeout = 4;
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    string json = req.downloadHandler.text;
                    resolvedAddress = ParseNominatimAddress(json, lat, lng);
                }
            }

            // Fallback to BigDataCloud Reverse Geocoding API if Nominatim returned empty/failed
            if (string.IsNullOrEmpty(resolvedAddress) || resolvedAddress.StartsWith("Regional Mining Sector"))
            {
                string urlBDC = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={0}&longitude={1}&localityLanguage=en",
                    lat, lng);

                using (UnityEngine.Networking.UnityWebRequest reqBDC = UnityEngine.Networking.UnityWebRequest.Get(urlBDC))
                {
                    reqBDC.timeout = 4;
                    yield return reqBDC.SendWebRequest();

                    if (reqBDC.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        string jsonBDC = reqBDC.downloadHandler.text;
                        resolvedAddress = ParseBigDataCloudAddress(jsonBDC, lat, lng);
                    }
                }
            }

            if (string.IsNullOrEmpty(resolvedAddress))
            {
                resolvedAddress = FormatFallbackLocation(lat, lng);
            }

            onAddressResolved?.Invoke(resolvedAddress);
        }

        private string ParseNominatimAddress(string json, double lat, double lng)
        {
            try
            {
                var dict = Firebase.MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return null;

                if (dict.ContainsKey("address") && dict["address"] is Dictionary<string, object> addr)
                {
                    List<string> parts = new List<string>();

                    string place = null;
                    if (addr.ContainsKey("village")) place = addr["village"] as string;
                    else if (addr.ContainsKey("suburb")) place = addr["suburb"] as string;
                    else if (addr.ContainsKey("neighbourhood")) place = addr["neighbourhood"] as string;
                    else if (addr.ContainsKey("town")) place = addr["town"] as string;
                    else if (addr.ContainsKey("city")) place = addr["city"] as string;

                    if (!string.IsNullOrEmpty(place)) parts.Add(place);

                    string district = null;
                    if (addr.ContainsKey("state_district")) district = addr["state_district"] as string;
                    else if (addr.ContainsKey("county")) district = addr["county"] as string;
                    else if (addr.ContainsKey("city") && place != addr["city"] as string) district = addr["city"] as string;

                    if (!string.IsNullOrEmpty(district) && !parts.Contains(district)) parts.Add(district);

                    if (addr.ContainsKey("state") && addr["state"] is string st && !string.IsNullOrEmpty(st))
                        parts.Add(st);
                    if (addr.ContainsKey("country") && addr["country"] is string country && !string.IsNullOrEmpty(country))
                        parts.Add(country);

                    if (parts.Count > 0)
                        return string.Join(", ", parts);
                }

                if (dict.ContainsKey("display_name") && dict["display_name"] is string displayName && !string.IsNullOrEmpty(displayName))
                {
                    var splits = displayName.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length > 4)
                        return string.Join(", ", new[] { splits[0], splits[1], splits[splits.Length - 2], splits[splits.Length - 1] });
                    return displayName;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TrainingLocationCapture] Parse address error: {e.Message}");
            }
            return null;
        }

        private string ParseBigDataCloudAddress(string json, double lat, double lng)
        {
            try
            {
                var dict = Firebase.MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return null;

                List<string> parts = new List<string>();

                if (dict.ContainsKey("locality") && dict["locality"] is string loc && !string.IsNullOrEmpty(loc))
                    parts.Add(loc);
                else if (dict.ContainsKey("city") && dict["city"] is string city && !string.IsNullOrEmpty(city))
                    parts.Add(city);

                if (dict.ContainsKey("principalSubdivision") && dict["principalSubdivision"] is string state && !string.IsNullOrEmpty(state))
                    parts.Add(state);

                if (dict.ContainsKey("countryName") && dict["countryName"] is string country && !string.IsNullOrEmpty(country))
                    parts.Add(country);

                if (parts.Count > 0)
                    return string.Join(", ", parts);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TrainingLocationCapture] Parse BDC address error: {e.Message}");
            }
            return null;
        }

        private string FormatFallbackLocation(double lat, double lng)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "Mining Safety Sector ({0:F2}°, {1:F2}°)", lat, lng);
        }

        /// <summary>
        /// Displays a clean UI Toolkit modal dialog requesting user consent for location analytics.
        /// </summary>
        public static void ShowConsentModal(VisualElement root, Action<bool> onResult)
        {
            if (root == null)
            {
                onResult?.Invoke(false);
                return;
            }

            // Check if modal already exists
            var existingModal = root.Q("location-consent-modal");
            if (existingModal != null) root.Remove(existingModal);

            var modalOverlay = new VisualElement();
            modalOverlay.name = "location-consent-modal";
            modalOverlay.style.position = Position.Absolute;
            modalOverlay.style.left = 0;
            modalOverlay.style.top = 0;
            modalOverlay.style.right = 0;
            modalOverlay.style.bottom = 0;
            modalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.65f));
            modalOverlay.style.alignItems = Align.Center;
            modalOverlay.style.justifyContent = Justify.Center;

            var dialogBox = new VisualElement();
            dialogBox.style.backgroundColor = new StyleColor(Color.white);
            SetBorderRadius(dialogBox, 16);
            dialogBox.style.paddingLeft = 24;
            dialogBox.style.paddingRight = 24;
            dialogBox.style.paddingTop = 24;
            dialogBox.style.paddingBottom = 24;
            dialogBox.style.width = 480;
            dialogBox.style.maxWidth = Length.Percent(90);

            var titleLabel = new Label("📍 Regional Analytics Location Access");
            titleLabel.style.fontSize = 20;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            titleLabel.style.marginBottom = 12;

            var descLabel = new Label(
                "Mining Safety AR captures your coarse location ONCE when starting a training module strictly for regional safety analytics.\n\n" +
                "• Captured ONLY at module start (never continuous tracking).\n" +
                "• Used to evaluate regional safety compliance.\n" +
                "• Training will proceed even if location is declined.");
            descLabel.style.fontSize = 14;
            descLabel.style.color = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 20;

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;

            Debug.Log("[TrainingLocationCapture] Location consent modal displayed on screen. Awaiting user button click...");

            var declineBtn = new Button(() =>
            {
                Debug.Log("[TrainingLocationCapture] User clicked 'Skip / Decline' button.");
                HasUserConsented = false;
                root.Remove(modalOverlay);
                onResult?.Invoke(false);
            }) { text = "Skip / Decline" };
            declineBtn.style.backgroundColor = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            declineBtn.style.color = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            declineBtn.style.marginRight = 12;
            SetBorderRadius(declineBtn, 8);
            declineBtn.style.paddingLeft = 16;
            declineBtn.style.paddingRight = 16;
            declineBtn.style.paddingTop = 10;
            declineBtn.style.paddingBottom = 10;

            var allowBtn = new Button(() =>
            {
                Debug.Log("[TrainingLocationCapture] User clicked 'Allow Location' button.");
                HasUserConsented = true;
                root.Remove(modalOverlay);
                onResult?.Invoke(true);
            }) { text = "Allow Location" };
            allowBtn.style.backgroundColor = new StyleColor(new Color(1f, 0.42f, 0f)); // Industrial Orange
            allowBtn.style.color = new StyleColor(Color.white);
            SetBorderRadius(allowBtn, 8);
            allowBtn.style.paddingLeft = 16;
            allowBtn.style.paddingRight = 16;
            allowBtn.style.paddingTop = 10;
            allowBtn.style.paddingBottom = 10;

            btnRow.Add(declineBtn);
            btnRow.Add(allowBtn);

            dialogBox.Add(titleLabel);
            dialogBox.Add(descLabel);
            dialogBox.Add(btnRow);
            modalOverlay.Add(dialogBox);

            root.Add(modalOverlay);
            modalOverlay.BringToFront();
        }

        private static void SetBorderRadius(VisualElement el, float radius)
        {
            el.style.borderTopLeftRadius = radius;
            el.style.borderTopRightRadius = radius;
            el.style.borderBottomLeftRadius = radius;
            el.style.borderBottomRightRadius = radius;
        }
    }
}
