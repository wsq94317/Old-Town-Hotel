using UnityEngine;

public static class HotelSessionState
{
    public static HotelPresentationState Current = new HotelPresentationState();
}

// Original synthesized audio: no external downloads or missing clip references.
public sealed class HotelAudio : MonoBehaviour
{
    private static HotelAudio _instance;
    private AudioSource _effects, _music;
    private AudioClip _click, _coin, _work, _bell;
    private int _cash = int.MinValue, _construction = -1, _day = -1;

    public static void Ensure()
    {
        if (_instance != null) return;
        new GameObject("HotelSoundscape").AddComponent<HotelAudio>();
    }

    private void Awake()
    {
        _instance = this;
        _effects = gameObject.AddComponent<AudioSource>(); _effects.volume = .18f;
        _music = gameObject.AddComponent<AudioSource>(); _music.volume = .055f; _music.loop = true;
        _click = Tone("Wooden button", new[] { 420f, 310f }, .045f);
        _coin = Tone("Receipt coins", new[] { 659f, 880f, 1318f }, .08f);
        _work = Tone("Construction starts", new[] { 180f, 240f, 360f }, .08f);
        _bell = Tone("Front desk bell", new[] { 880f, 1108f, 1318f }, .18f);
        _music.clip = Tone("Old town evening", new[] { 261.63f, 329.63f, 392f, 329.63f,
            220f, 261.63f, 329.63f, 261.63f, 174.61f, 220f, 261.63f, 220f,
            196f, 246.94f, 293.66f, 246.94f }, .5f);
        _music.Play();
    }

    public static void Click()
    {
        if (_instance != null && HotelSessionState.Current.soundEnabled)
            _instance._effects.PlayOneShot(_instance._click);
    }

    private void Update()
    {
        _music.mute = !HotelSessionState.Current.musicEnabled;
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge == null || bridge.Sim == null) return;
        var sim = bridge.Sim;
        if (HotelSessionState.Current.soundEnabled)
        {
            if (_cash != int.MinValue && sim.Cash > _cash) _effects.PlayOneShot(_coin);
            if (_construction >= 0 && sim.Renovations.RoomsUnderRenovation > _construction) _effects.PlayOneShot(_work);
            if (_day >= 0 && sim.Clock.CurrentDay > _day) _effects.PlayOneShot(_bell);
        }
        _cash = sim.Cash; _construction = sim.Renovations.RoomsUnderRenovation; _day = sim.Clock.CurrentDay;
    }

    private static AudioClip Tone(string name, float[] notes, float secondsPerNote)
    {
        const int rate = 22050;
        int count = Mathf.RoundToInt(rate * secondsPerNote);
        var data = new float[count * notes.Length];
        for (int n = 0; n < notes.Length; n++)
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 6f / secondsPerNote);
                data[n * count + i] = (Mathf.Sin(t * notes[n] * Mathf.PI * 2)
                    + .18f * Mathf.Sin(t * notes[n] * Mathf.PI * 4)) * envelope * .45f;
            }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0); return clip;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        foreach (var clip in new[] { _click, _coin, _work, _bell, _music != null ? _music.clip : null })
            if (clip != null) Destroy(clip);
    }
}
