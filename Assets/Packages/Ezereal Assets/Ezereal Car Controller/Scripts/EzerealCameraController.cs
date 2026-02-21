using UnityEngine;
using UnityEngine.InputSystem;

namespace Ezereal
{
    public class EzerealCameraController : MonoBehaviour
    {
        [SerializeField] private GameObject[] cameras;

        [Header("Analytics")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private int cameraChangedCount = 0;

        private float[] cameraUseSeconds;
        private float lastSwitchTimestamp;
        private int currentCameraIndex = -1;

        public int CameraChangedCount => cameraChangedCount;

        private void Awake()
        {
            if (cameras == null || cameras.Length == 0) return;

            cameraUseSeconds = new float[cameras.Length];

            // Pull current camera from active GameObject in array (if any)
            currentCameraIndex = FindActiveCameraIndex();
            if (currentCameraIndex < 0) currentCameraIndex = 0;

            ActivateCameraByIndex(currentCameraIndex);
            lastSwitchTimestamp = Now();
        }

        // Input System will call this when your action fires
        void OnSwitchCamera()
        {
            if (cameras == null || cameras.Length == 0) return;

            AccumulateCurrentCameraTime();

            int next = GetNextValidIndex(currentCameraIndex);
            if (next != currentCameraIndex)
                cameraChangedCount++;

            currentCameraIndex = next;
            ActivateCameraByIndex(currentCameraIndex);

            lastSwitchTimestamp = Now();
        }

        private float Now() => useUnscaledTime ? Time.unscaledTime : Time.time;

        private void AccumulateCurrentCameraTime()
        {
            if (cameraUseSeconds == null || cameraUseSeconds.Length != cameras.Length)
                cameraUseSeconds = new float[cameras.Length];

            // If someone externally changed active camera, sync before accumulating
            int active = FindActiveCameraIndex();
            if (active >= 0) currentCameraIndex = active;

            if (currentCameraIndex < 0 || currentCameraIndex >= cameraUseSeconds.Length) return;

            float delta = Now() - lastSwitchTimestamp;
            if (delta > 0f) cameraUseSeconds[currentCameraIndex] += delta;
        }

        private int FindActiveCameraIndex()
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].activeInHierarchy)
                    return i;
            }
            return -1;
        }

        private int GetNextValidIndex(int from)
        {
            if (cameras == null || cameras.Length == 0) return -1;

            int start = Mathf.Clamp(from, 0, cameras.Length - 1);

            for (int step = 1; step <= cameras.Length; step++)
            {
                int idx = (start + step) % cameras.Length;
                if (cameras[idx] != null) return idx;
            }

            return start; // fallback
        }

        private void ActivateCameraByIndex(int index)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].SetActive(i == index);
            }
        }

        private void OnDisable() { if (cameras != null && cameras.Length > 0) AccumulateCurrentCameraTime(); }
        private void OnDestroy() { if (cameras != null && cameras.Length > 0) AccumulateCurrentCameraTime(); }

        // --------- UI helpers ---------

        public int GetCameraCount() => cameras?.Length ?? 0;

        public string GetCameraName(int index)
        {
            if (cameras == null || index < 0 || index >= cameras.Length) return "(Invalid)";
            return cameras[index] != null ? cameras[index].name : $"Camera {index}";
        }

        public int GetCurrentCameraIndex()
        {
            int active = FindActiveCameraIndex();
            return active >= 0 ? active : currentCameraIndex;
        }

        public float[] GetAllCameraUseSecondsSnapshot()
        {
            int n = cameras?.Length ?? 0;
            var snapshot = new float[n];

            for (int i = 0; i < n; i++)
                snapshot[i] = (cameraUseSeconds != null && i < cameraUseSeconds.Length) ? cameraUseSeconds[i] : 0f;

            int cur = GetCurrentCameraIndex();
            if (cur >= 0 && cur < snapshot.Length)
                snapshot[cur] += Mathf.Max(0f, Now() - lastSwitchTimestamp);

            return snapshot;
        }
    }
}
