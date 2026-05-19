using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float volume = 1f;
    public AudioSource bgmSource;
    public AudioClip bgmClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        bool isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
        bgmSource.volume = isMuted ? 0f : volume;

        bgmSource.Play();
    }

    public void SetMute(bool mute)
    {
        bgmSource.volume = mute ? 0f : volume;
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (PlayerPrefs.GetInt("AudioMuted", 0) == 0)
        {
            bgmSource.volume = volume;
        }
    }
}
