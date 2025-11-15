using UnityEngine;
using UnityEngine.UI;

public class Options : MonoBehaviour
{
    [Header("Screen Buttons")]
    public Button fullButton;

    [Header("Volume UI")]
    public Button muteButton;
    public Button minusButton;
    public Button plusButton;
    public Slider volumeSlider;

    private int volume = 10;        // 0~10
    private int lastVolume = 10;    // mute 해제 시 복구용

    private const string VOLUME_KEY = "master_volume";

    void Start()
    {
        // 초기화 (저장된 볼륨 불러오기)
        volume = PlayerPrefs.GetInt(VOLUME_KEY, 10);
        lastVolume = volume;

        volumeSlider.value = volume / 10f;
        AudioListener.volume = volume / 10f;

        // 버튼 이벤트 연결
        fullButton.onClick.AddListener(SetFullscreen);

        muteButton.onClick.AddListener(ToggleMute);
        minusButton.onClick.AddListener(VolumeDown);
        plusButton.onClick.AddListener(VolumeUp);

        volumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void SaveVolume()
    {
        PlayerPrefs.SetInt(VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    // ===================== 화면 설정 =====================
    private void SetFullscreen()
    {
        Screen.fullScreen = true;
    }

    private void SetWindowMode()
    {
        Screen.fullScreen = false;
    }

    // ===================== 볼륨 설정 =====================
    private void ToggleMute()
    {
        if (volume > 0)
        {
            lastVolume = volume;
            volume = 0;
        }
        else
        {
            volume = lastVolume;
        }

        UpdateVolumeUI();
    }

    private void VolumeDown()
    {
        volume = Mathf.Max(0, volume - 1);
        UpdateVolumeUI();
    }

    private void VolumeUp()
    {
        volume = Mathf.Min(10, volume + 1);
        UpdateVolumeUI();
    }

    private void OnSliderChanged(float value)
    {
        volume = Mathf.RoundToInt(value * 10f);
        UpdateVolumeUI(false);
    }

    private void UpdateVolumeUI(bool updateSlider = true)
    {
        AudioListener.volume = volume / 10f;

        if (updateSlider)
            volumeSlider.value = volume / 10f;

        SaveVolume();
    }
}
