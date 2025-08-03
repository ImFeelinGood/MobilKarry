using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    public Toggle muteToggle;

    private void Start()
    {
        // Load saved preference
        bool isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
        SetMute(isMuted);
        muteToggle.isOn = isMuted;

        muteToggle.onValueChanged.AddListener(SetMute);
    }

    public void SetMute(bool mute)
    {
        AudioListener.volume = mute ? 0f : 1f;
        PlayerPrefs.SetInt("AudioMuted", mute ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        muteToggle.onValueChanged.RemoveListener(SetMute);
    }
}
