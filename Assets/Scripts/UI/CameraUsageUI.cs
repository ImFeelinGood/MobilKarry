using System.Text;
using TMPro;
using UnityEngine;

namespace Ezereal
{
    public class CameraUsageUI : MonoBehaviour
    {
        [SerializeField] private EzerealCameraController cameraController;
        [SerializeField] private TMP_Text outputText;

        [Header("Display")]
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private bool showOnlyUsedCameras = true;
        [SerializeField] private float usedThresholdSeconds = 0.05f;

        private float timer;
        private readonly StringBuilder sb = new StringBuilder(256);

        private void Reset()
        {
            outputText = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (cameraController == null || outputText == null) return;

            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;

            Refresh();
        }

        private void Refresh()
        {
            int count = cameraController.GetCameraCount();
            var times = cameraController.GetAllCameraUseSecondsSnapshot();
            int current = cameraController.GetCurrentCameraIndex();

            sb.Clear();
            sb.AppendLine("Camera Usage:");

            bool anyShown = false;

            for (int i = 0; i < count; i++)
            {
                float t = (times != null && i < times.Length) ? times[i] : 0f;
                bool isCurrent = (i == current);
                bool isUsed = t >= usedThresholdSeconds || isCurrent;

                if (showOnlyUsedCameras && !isUsed) continue;

                anyShown = true;

                sb.Append(isCurrent ? "> " : "- ");
                sb.Append(cameraController.GetCameraName(i));
                sb.Append(" : ");
                sb.AppendLine(FormatTime(t));
            }

            if (!anyShown)
                sb.AppendLine("(No camera usage recorded yet)");

            outputText.text = sb.ToString();
        }

        public string GetCameraUsageSummaryForCsv()
        {
            if (cameraController == null) return "";

            int count = cameraController.GetCameraCount();
            var times = cameraController.GetAllCameraUseSecondsSnapshot();

            StringBuilder csv = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                float t = (times != null && i < times.Length) ? times[i] : 0f;
                if (i > 0) csv.Append(" | ");
                csv.Append(cameraController.GetCameraName(i));
                csv.Append(": ");
                csv.Append(FormatTime(t));
            }

            return csv.ToString();
        }

        public float GetTotalCameraUsageSeconds()
        {
            if (cameraController == null) return 0f;

            var times = cameraController.GetAllCameraUseSecondsSnapshot();
            if (times == null) return 0f;

            float total = 0f;
            for (int i = 0; i < times.Length; i++)
                total += times[i];

            return total;
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);

            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;

            return (h > 0) ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }

        public int GetCameraCount()
        {
            return cameraController != null ? cameraController.GetCameraCount() : 0;
        }

        public string GetCameraNameAt(int index)
        {
            if (cameraController == null) return "";
            return cameraController.GetCameraName(index);
        }

        public float GetCameraUsageSecondsAt(int index)
        {
            if (cameraController == null) return 0f;

            var times = cameraController.GetAllCameraUseSecondsSnapshot();
            if (times == null || index < 0 || index >= times.Length) return 0f;

            return times[index];
        }

        public string GetCameraUsageFormattedAt(int index)
        {
            return FormatTime(GetCameraUsageSecondsAt(index));
        }
    }
}