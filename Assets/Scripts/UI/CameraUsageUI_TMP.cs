using System.Text;
using TMPro;
using UnityEngine;

namespace Ezereal
{
    public class CameraUsageUI_TMP : MonoBehaviour
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
                sb.Append(cameraController.GetCameraName(i)); // <- pulled from GameObject name
                sb.Append(" : ");
                sb.AppendLine(FormatTime(t));
            }

            if (!anyShown)
                sb.AppendLine("(No camera usage recorded yet)");

            outputText.text = sb.ToString();
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
    }
}
