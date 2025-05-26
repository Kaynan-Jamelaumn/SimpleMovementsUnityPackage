using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System;
using System.Reflection;

public class PauseMenuSettings : MonoBehaviour
{
    [Header("Settings UI")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject settingsMainContent;
    public GameObject audioPanel;
    public GameObject graphicsPanel;
    public GameObject controlsPanel;

    [Header("Audio Settings")]
    public AudioMixerGroup masterMixer;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public TextMeshProUGUI masterVolumeText;
    public TextMeshProUGUI musicVolumeText;
    public TextMeshProUGUI sfxVolumeText;

    [Header("Graphics Settings")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;
    public Slider brightnessSlider;
    public TextMeshProUGUI brightnessText;

    [Header("Controls Settings")]
    public Slider mouseSensitivitySlider;
    public TextMeshProUGUI mouseSensitivityText;
    public Toggle invertMouseToggle;
    public Button resetControlsButton;

    // Cache frequently accessed components and data for better performance
    private Resolution[] availableResolutions;
    private bool isInitialized = false;
    private MonoBehaviour cachedPlayerController;

    // PlayerPrefs keys organized as constants to prevent typos and improve maintainability
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string FULLSCREEN_KEY = "Fullscreen";
    private const string VSYNC_KEY = "VSync";
    private const string BRIGHTNESS_KEY = "Brightness";
    private const string MOUSE_SENSITIVITY_KEY = "MouseSensitivity";
    private const string INVERT_MOUSE_KEY = "InvertMouse";
    private const string QUALITY_LEVEL_KEY = "QualityLevel";
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";

    // AudioMixer parameter names - must match the exposed parameters in the AudioMixer
    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";

    // Convert linear volume (0-1) to decibel scale for AudioMixer
    private const float VOLUME_MULTIPLIER = 20f;

    // Centralized default values for consistency across the application
    private struct DefaultSettings
    {
        public const float Volume = 0.75f;
        public const float Brightness = 1.0f;
        public const float MouseSensitivity = 2.0f;
        public const bool Fullscreen = true;
        public const bool VSync = true;
        public const bool InvertMouse = false;
    }

    // Unity Lifecycle Events
    void Start()
    {
        InitializeSettings();
        LoadSettings();
        isInitialized = true; // Prevent sound effects during initialization
    }

    // Auto-save settings when application loses focus or is paused
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveAllSettings();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveAllSettings();
    }

    void OnDestroy()
    {
        SaveAllSettings();
        RemoveAllListeners(); // Prevent memory leaks
    }

    // Initialization Methods
    private void InitializeSettings()
    {
        InitializeResolutionDropdown();
        InitializeQualityDropdown();
        SetupUIListeners();
        CachePlayerController();
    }

    // Populate resolution dropdown with all available screen resolutions
    private void InitializeResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        int currentResolutionIndex = 0;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            var resolution = availableResolutions[i];
            string option = $"{resolution.width} x {resolution.height} @ {resolution.refreshRateRatio.value:F0}Hz";
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(option));

            // Find and mark the current resolution as selected
            if (IsCurrentResolution(resolution))
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private bool IsCurrentResolution(Resolution resolution)
    {
        return resolution.width == Screen.currentResolution.width &&
               resolution.height == Screen.currentResolution.height;
    }

    // Populate quality dropdown with Unity's built-in quality level names
    private void InitializeQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        string[] qualityNames = QualitySettings.names;

        for (int i = 0; i < qualityNames.Length; i++)
        {
            qualityDropdown.options.Add(new TMP_Dropdown.OptionData(qualityNames[i]));
        }

        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
    }

    // Wire up all UI element event listeners
    private void SetupUIListeners()
    {
        // Audio sliders
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        // Graphics controls
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener(SetVSync);

        // Control settings
        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        if (invertMouseToggle != null)
            invertMouseToggle.onValueChanged.AddListener(SetInvertMouse);
        if (resetControlsButton != null)
            resetControlsButton.onClick.AddListener(ResetControls);
    }

    // Clean up event listeners to prevent memory leaks
    private void RemoveAllListeners()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveAllListeners();
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveAllListeners();
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        if (brightnessSlider != null) brightnessSlider.onValueChanged.RemoveAllListeners();
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveAllListeners();
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveAllListeners();
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveAllListeners();
        if (vsyncToggle != null) vsyncToggle.onValueChanged.RemoveAllListeners();
        if (invertMouseToggle != null) invertMouseToggle.onValueChanged.RemoveAllListeners();
        if (resetControlsButton != null) resetControlsButton.onClick.RemoveAllListeners();
    }

    // Find and cache the player controller for performance optimization
    private void CachePlayerController()
    {
        cachedPlayerController = FindPlayerController();
    }

    // Settings Loading Methods
    private void LoadSettings()
    {
        LoadAudioSettings();
        LoadGraphicsSettings();
        LoadControlSettings();
    }

    private void LoadAudioSettings()
    {
        LoadVolumeSlider(masterVolumeSlider, MASTER_VOLUME_KEY, DefaultSettings.Volume, SetMasterVolume);
        LoadVolumeSlider(musicVolumeSlider, MUSIC_VOLUME_KEY, DefaultSettings.Volume, SetMusicVolume);
        LoadVolumeSlider(sfxVolumeSlider, SFX_VOLUME_KEY, DefaultSettings.Volume, SetSFXVolume);
    }

    // Generic method to load volume settings and apply them consistently
    private void LoadVolumeSlider(Slider slider, string key, float defaultValue, System.Action<float> setter)
    {
        if (slider == null) return;

        float volume = PlayerPrefs.GetFloat(key, defaultValue);
        slider.value = volume;
        setter(volume);
    }

    private void LoadGraphicsSettings()
    {
        // Load fullscreen setting
        if (fullscreenToggle != null)
        {
            bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, DefaultSettings.Fullscreen ? 1 : 0) == 1;
            fullscreenToggle.isOn = isFullscreen;
            Screen.fullScreen = isFullscreen;
        }

        // Load VSync setting
        if (vsyncToggle != null)
        {
            bool vsync = PlayerPrefs.GetInt(VSYNC_KEY, DefaultSettings.VSync ? 1 : 0) == 1;
            vsyncToggle.isOn = vsync;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
        }

        // Load brightness setting
        if (brightnessSlider != null)
        {
            float brightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, DefaultSettings.Brightness);
            brightnessSlider.value = brightness;
            SetBrightness(brightness);
        }

        // Load quality level with bounds checking
        int savedQuality = PlayerPrefs.GetInt(QUALITY_LEVEL_KEY, QualitySettings.GetQualityLevel());
        if (qualityDropdown != null && savedQuality < QualitySettings.names.Length)
        {
            qualityDropdown.value = savedQuality;
            QualitySettings.SetQualityLevel(savedQuality);
        }

        // Load resolution with validation
        int savedResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, -1);
        if (savedResolutionIndex >= 0 && savedResolutionIndex < availableResolutions.Length)
        {
            var resolution = availableResolutions[savedResolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
            if (resolutionDropdown != null)
            {
                resolutionDropdown.value = savedResolutionIndex;
            }
        }
    }

    private void LoadControlSettings()
    {
        // Load mouse sensitivity
        if (mouseSensitivitySlider != null)
        {
            float sensitivity = PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, DefaultSettings.MouseSensitivity);
            mouseSensitivitySlider.value = sensitivity;
            SetMouseSensitivity(sensitivity);
        }

        // Load invert mouse setting
        if (invertMouseToggle != null)
        {
            bool invertMouse = PlayerPrefs.GetInt(INVERT_MOUSE_KEY, DefaultSettings.InvertMouse ? 1 : 0) == 1;
            invertMouseToggle.isOn = invertMouse;
            UpdatePlayerControllerSetting(INVERT_MOUSE_KEY.ToLower(), invertMouse);
        }
    }

    // Audio Setting Methods
    public void SetMasterVolume(float volume)
    {
        if (!ValidateVolumeSlider(masterVolumeSlider, volume)) return;

        SetAudioMixerVolume(MASTER_VOLUME_PARAM, volume);
        UpdateVolumeText(masterVolumeText, volume);
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
        PlayButtonSoundIfInitialized();
    }

    public void SetMusicVolume(float volume)
    {
        if (!ValidateVolumeSlider(musicVolumeSlider, volume)) return;

        SetAudioMixerVolume(MUSIC_VOLUME_PARAM, volume);
        UpdateVolumeText(musicVolumeText, volume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
        PlayButtonSoundIfInitialized();
    }

    public void SetSFXVolume(float volume)
    {
        if (!ValidateVolumeSlider(sfxVolumeSlider, volume)) return;

        SetAudioMixerVolume(SFX_VOLUME_PARAM, volume);
        UpdateVolumeText(sfxVolumeText, volume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
        PlayButtonSoundIfInitialized();
    }

    private bool ValidateVolumeSlider(Slider slider, float volume)
    {
        return slider != null && volume >= 0f && volume <= 1f;
    }

    // Convert linear volume (0-1) to logarithmic decibel scale for AudioMixer
    private void SetAudioMixerVolume(string parameter, float volume)
    {
        if (masterMixer?.audioMixer != null)
        {
            // Handle zero volume case (mute) by setting to -80dB instead of -infinity
            float dbValue = volume > 0 ? Mathf.Log10(volume) * VOLUME_MULTIPLIER : -80f;
            masterMixer.audioMixer.SetFloat(parameter, dbValue);
        }
    }

    // Update volume percentage display text
    private void UpdateVolumeText(TextMeshProUGUI volumeText, float volume)
    {
        if (volumeText != null)
        {
            volumeText.text = $"{Mathf.RoundToInt(volume * 100)}%";
        }
    }

    // Graphics Setting Methods
    public void SetQuality(int qualityIndex)
    {
        if (qualityIndex < 0 || qualityIndex >= QualitySettings.names.Length) return;

        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt(QUALITY_LEVEL_KEY, qualityIndex);
        PlayButtonSoundIfInitialized();
    }

    public void SetResolution(int resolutionIndex)
    {
        if (availableResolutions == null || resolutionIndex < 0 || resolutionIndex >= availableResolutions.Length)
            return;

        Resolution resolution = availableResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, resolutionIndex);
        PlayButtonSoundIfInitialized();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
        PlayButtonSoundIfInitialized();
    }

    public void SetVSync(bool vsync)
    {
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        PlayerPrefs.SetInt(VSYNC_KEY, vsync ? 1 : 0);
        PlayButtonSoundIfInitialized();
    }

    public void SetBrightness(float brightness)
    {
        if (brightnessSlider != null && (brightness < brightnessSlider.minValue || brightness > brightnessSlider.maxValue))
            return;

        // Update brightness percentage display
        if (brightnessText != null)
        {
            brightnessText.text = $"{Mathf.RoundToInt(brightness * 100)}%";
        }

        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, brightness);
        ApplyBrightnessToRenderer(brightness);
        PlayButtonSoundIfInitialized();
    }

    // Apply brightness setting to the game's rendering
    // Note: This is a simple implementation using ambient intensity - replace with post-processing for better results
    private void ApplyBrightnessToRenderer(float brightness)
    {
        RenderSettings.ambientIntensity = brightness;
    }

    // Control Setting Methods
    public void SetMouseSensitivity(float sensitivity)
    {
        if (mouseSensitivitySlider != null &&
            (sensitivity < mouseSensitivitySlider.minValue || sensitivity > mouseSensitivitySlider.maxValue))
            return;

        if (mouseSensitivityText != null)
        {
            mouseSensitivityText.text = sensitivity.ToString("F1");
        }

        PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, sensitivity);
        UpdatePlayerControllerSetting("mouseSensitivity", sensitivity);
        PlayButtonSoundIfInitialized();
    }

    public void SetInvertMouse(bool invert)
    {
        PlayerPrefs.SetInt(INVERT_MOUSE_KEY, invert ? 1 : 0);
        UpdatePlayerControllerSetting("invertMouse", invert);
        PlayButtonSoundIfInitialized();
    }

    public void ResetControls()
    {
        // Reset mouse sensitivity
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.value = DefaultSettings.MouseSensitivity;
            SetMouseSensitivity(DefaultSettings.MouseSensitivity);
        }

        // Reset invert mouse
        if (invertMouseToggle != null)
        {
            invertMouseToggle.isOn = DefaultSettings.InvertMouse;
            SetInvertMouse(DefaultSettings.InvertMouse);
        }

        PlayButtonSoundIfInitialized();
    }

    // Player Controller Integration Methods
    // Attempt to find the player controller using common naming conventions
    private MonoBehaviour FindPlayerController()
    {
        string[] commonControllerNames = { "PlayerController", "FirstPersonController", "FPSController", "MouseLook" };

        foreach (string controllerName in commonControllerNames)
        {
            var controller = FindFirstObjectByType<MonoBehaviour>();
            if (controller != null && controller.GetType().Name.Contains(controllerName))
            {
                return controller;
            }
        }

        return FindFirstObjectByType<MonoBehaviour>();
    }

    // Use reflection to dynamically update player controller settings
    // This allows the settings system to work with different controller implementations
    private void UpdatePlayerControllerSetting(string fieldName, object value)
    {
        if (cachedPlayerController == null) return;

        try
        {
            Type controllerType = cachedPlayerController.GetType();
            FieldInfo field = controllerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null && field.FieldType == value.GetType())
            {
                field.SetValue(cachedPlayerController, value);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to update player controller setting '{fieldName}': {ex.Message}");
        }
    }

    // Panel Navigation Methods
    public void ShowSettingsPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(settingsPanel, true);
        ShowMainSettingsContent();
        PlayButtonSoundIfInitialized();
    }

    public void ShowAudioPanel()
    {
        ShowSubPanel(audioPanel);
    }

    public void ShowGraphicsPanel()
    {
        ShowSubPanel(graphicsPanel);
    }

    public void ShowControlsPanel()
    {
        ShowSubPanel(controlsPanel);
    }

    // Helper method to switch between settings sub-panels
    private void ShowSubPanel(GameObject targetPanel)
    {
        SetPanelActive(settingsMainContent, false);
        SetPanelActive(audioPanel, false);
        SetPanelActive(graphicsPanel, false);
        SetPanelActive(controlsPanel, false);
        SetPanelActive(targetPanel, true);
        PlayButtonSoundIfInitialized();
    }

    public void BackToSettings()
    {
        ShowMainSettingsContent();
        PlayButtonSoundIfInitialized();
    }

    public void BackToMainMenu()
    {
        SetPanelActive(settingsPanel, false);
        SetPanelActive(mainMenuPanel, true);
        PlayButtonSoundIfInitialized();
    }

    private void ShowMainSettingsContent()
    {
        SetPanelActive(settingsMainContent, true);
        SetPanelActive(audioPanel, false);
        SetPanelActive(graphicsPanel, false);
        SetPanelActive(controlsPanel, false);
    }

    // Utility Methods
    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    // Only play button sounds after initialization to avoid sounds during setup
    private void PlayButtonSoundIfInitialized()
    {
        if (!isInitialized) return;
        PauseMenuManager.Instance?.PlayButtonSound();
    }

    // Save all settings to PlayerPrefs with error handling
    private void SaveAllSettings()
    {
        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save settings: {ex.Message}");
        }
    }

    // Public utility methods for external access
    public float GetCurrentMouseSensitivity()
    {
        return PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, DefaultSettings.MouseSensitivity);
    }

    public bool GetInvertMouseSetting()
    {
        return PlayerPrefs.GetInt(INVERT_MOUSE_KEY, DefaultSettings.InvertMouse ? 1 : 0) == 1;
    }

    // Reset all settings to their default values
    public void ResetAllSettings()
    {
        if (masterVolumeSlider != null) { masterVolumeSlider.value = DefaultSettings.Volume; SetMasterVolume(DefaultSettings.Volume); }
        if (musicVolumeSlider != null) { musicVolumeSlider.value = DefaultSettings.Volume; SetMusicVolume(DefaultSettings.Volume); }
        if (sfxVolumeSlider != null) { sfxVolumeSlider.value = DefaultSettings.Volume; SetSFXVolume(DefaultSettings.Volume); }
        if (brightnessSlider != null) { brightnessSlider.value = DefaultSettings.Brightness; SetBrightness(DefaultSettings.Brightness); }
        if (fullscreenToggle != null) { fullscreenToggle.isOn = DefaultSettings.Fullscreen; SetFullscreen(DefaultSettings.Fullscreen); }
        if (vsyncToggle != null) { vsyncToggle.isOn = DefaultSettings.VSync; SetVSync(DefaultSettings.VSync); }

        ResetControls();
        PlayButtonSoundIfInitialized();
    }
}