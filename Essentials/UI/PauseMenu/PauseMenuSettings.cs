using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System;
using System.Reflection;

public class PauseMenuSettings : MonoBehaviour
{
    [Header("UI Audio Clips")]
    public AudioClip buttonClickSound;
    public AudioClip sliderChangeSound;

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

    [Header("Advanced Graphics Settings")]
    public Slider fpsLimitSlider;
    public TextMeshProUGUI fpsLimitText;
    public Toggle showFpsToggle;
    public Slider renderDistanceSlider;
    public TextMeshProUGUI renderDistanceText;
    public Toggle motionBlurToggle;
    public Toggle antiAliasingToggle;
    public TMP_Dropdown shadowQualityDropdown;

    [Header("Controls Settings")]
    public Slider mouseSensitivitySlider;
    public TextMeshProUGUI mouseSensitivityText;
    public Toggle invertMouseToggle;
    public Button resetControlsButton;

    [Header("FPS Counter")]
    public GameObject fpsCounterObject; // Reference to FPS counter UI element
    public TextMeshProUGUI fpsCounterText;
    // Cache frequently accessed components and data for better performance
    private Resolution[] availableResolutions;
    private bool isInitialized = false;
    private MonoBehaviour cachedPlayerController;
    private Camera mainCamera;

    // FPS tracking for display
    private float deltaTime = 0.0f;
    private bool showFps = false;

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

    // New Graphics Settings Keys
    private const string FPS_LIMIT_KEY = "FPSLimit";
    private const string SHOW_FPS_KEY = "ShowFPS";
    private const string RENDER_DISTANCE_KEY = "RenderDistance";
    private const string MOTION_BLUR_KEY = "MotionBlur";
    private const string ANTI_ALIASING_KEY = "AntiAliasing";
    private const string SHADOW_QUALITY_KEY = "ShadowQuality";

    // AudioMixer parameter names - must match the exposed parameters in the AudioMixer
    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";

    // Convert linear volume (0-1) to decibel scale for AudioMixer
    private const float VOLUME_MULTIPLIER = 20f;

    // FPS limit options
    private readonly int[] fpsLimitOptions = { 30, 60, 120, -1 }; // -1 represents unlimited

    // Centralized default values for consistency across the application
    private struct DefaultSettings
    {
        public const float Volume = 0.75f;
        public const float Brightness = 1.0f;
        public const float MouseSensitivity = 2.0f;
        public const bool Fullscreen = true;
        public const bool VSync = true;
        public const bool InvertMouse = false;

        // New Graphics Defaults
        public const int FPSLimitIndex = 1; // Default to 60 FPS
        public const bool ShowFPS = false;
        public const float RenderDistance = 1000f;
        public const bool MotionBlur = true;
        public const bool AntiAliasing = true;
        public const int ShadowQuality = 2; // Medium shadows by default
    }

    // Unity Lifecycle Events
    void Start()
    {
        InitializeSettings();
        LoadSettings();
        isInitialized = true; // Prevent sound effects during initialization
    }

    void Update()
    {
        if (showFps)
        {
            UpdateFPSCounter();
        }
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
        InitializeShadowQualityDropdown();
        InitializeFPSLimitSlider();
        SetupUIListeners();
        CachePlayerController();
        CacheMainCamera();
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

    // Initialize shadow quality dropdown
    private void InitializeShadowQualityDropdown()
    {
        if (shadowQualityDropdown == null) return;

        shadowQualityDropdown.ClearOptions();
        string[] shadowQualities = { "Disabled", "Hard Shadows", "Soft Shadows" };

        for (int i = 0; i < shadowQualities.Length; i++)
        {
            shadowQualityDropdown.options.Add(new TMP_Dropdown.OptionData(shadowQualities[i]));
        }

        shadowQualityDropdown.value = DefaultSettings.ShadowQuality;
        shadowQualityDropdown.RefreshShownValue();
    }

    // Initialize FPS limit slider with discrete values
    private void InitializeFPSLimitSlider()
    {
        if (fpsLimitSlider == null) return;

        fpsLimitSlider.minValue = 0;
        fpsLimitSlider.maxValue = fpsLimitOptions.Length - 1;
        fpsLimitSlider.wholeNumbers = true;
        fpsLimitSlider.value = DefaultSettings.FPSLimitIndex;
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

        // New Graphics controls
        if (fpsLimitSlider != null)
            fpsLimitSlider.onValueChanged.AddListener(SetFPSLimit);
        if (showFpsToggle != null)
            showFpsToggle.onValueChanged.AddListener(SetShowFPS);
        if (renderDistanceSlider != null)
            renderDistanceSlider.onValueChanged.AddListener(SetRenderDistance);
        if (motionBlurToggle != null)
            motionBlurToggle.onValueChanged.AddListener(SetMotionBlur);
        if (antiAliasingToggle != null)
            antiAliasingToggle.onValueChanged.AddListener(SetAntiAliasing);
        if (shadowQualityDropdown != null)
            shadowQualityDropdown.onValueChanged.AddListener(SetShadowQuality);

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

        // New Graphics listeners cleanup
        if (fpsLimitSlider != null) fpsLimitSlider.onValueChanged.RemoveAllListeners();
        if (showFpsToggle != null) showFpsToggle.onValueChanged.RemoveAllListeners();
        if (renderDistanceSlider != null) renderDistanceSlider.onValueChanged.RemoveAllListeners();
        if (motionBlurToggle != null) motionBlurToggle.onValueChanged.RemoveAllListeners();
        if (antiAliasingToggle != null) antiAliasingToggle.onValueChanged.RemoveAllListeners();
        if (shadowQualityDropdown != null) shadowQualityDropdown.onValueChanged.RemoveAllListeners();
    }

    // Find and cache the player controller for performance optimization
    private void CachePlayerController()
    {
        cachedPlayerController = FindPlayerController();
    }

    // Cache main camera reference
    private void CacheMainCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
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
        if (SoundManager.Instance != null)
        {
            // Load from SoundManager
            if (masterVolumeSlider != null)
            {
                float volume = SoundManager.Instance.GetMasterVolume();
                masterVolumeSlider.value = volume;
                UpdateVolumeText(masterVolumeText, volume);
            }

            if (musicVolumeSlider != null)
            {
                float volume = SoundManager.Instance.GetMusicVolume();
                musicVolumeSlider.value = volume;
                UpdateVolumeText(musicVolumeText, volume);
            }

            if (sfxVolumeSlider != null)
            {
                float volume = SoundManager.Instance.GetSFXVolume();
                sfxVolumeSlider.value = volume;
                UpdateVolumeText(sfxVolumeText, volume);
            }
        }
        else
        {
            // Fallback to original loading method
            LoadVolumeSlider(masterVolumeSlider, MASTER_VOLUME_KEY, DefaultSettings.Volume, SetMasterVolume);
            LoadVolumeSlider(musicVolumeSlider, MUSIC_VOLUME_KEY, DefaultSettings.Volume, SetMusicVolume);
            LoadVolumeSlider(sfxVolumeSlider, SFX_VOLUME_KEY, DefaultSettings.Volume, SetSFXVolume);
        }
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
        // Load existing graphics settings
        if (fullscreenToggle != null)
        {
            bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, DefaultSettings.Fullscreen ? 1 : 0) == 1;
            fullscreenToggle.isOn = isFullscreen;
            Screen.fullScreen = isFullscreen;
        }

        if (vsyncToggle != null)
        {
            bool vsync = PlayerPrefs.GetInt(VSYNC_KEY, DefaultSettings.VSync ? 1 : 0) == 1;
            vsyncToggle.isOn = vsync;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
        }

        if (brightnessSlider != null)
        {
            float brightness = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, DefaultSettings.Brightness);
            brightnessSlider.value = brightness;
            SetBrightness(brightness);
        }

        int savedQuality = PlayerPrefs.GetInt(QUALITY_LEVEL_KEY, QualitySettings.GetQualityLevel());
        if (qualityDropdown != null && savedQuality < QualitySettings.names.Length)
        {
            qualityDropdown.value = savedQuality;
            QualitySettings.SetQualityLevel(savedQuality);
        }

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

        // Load new graphics settings
        LoadNewGraphicsSettings();
    }

    private void LoadNewGraphicsSettings()
    {
        // Load FPS limit
        if (fpsLimitSlider != null)
        {
            int fpsLimitIndex = PlayerPrefs.GetInt(FPS_LIMIT_KEY, DefaultSettings.FPSLimitIndex);
            fpsLimitSlider.value = fpsLimitIndex;
            SetFPSLimit(fpsLimitIndex);
        }

        // Load show FPS setting
        if (showFpsToggle != null)
        {
            bool showFpsEnabled = PlayerPrefs.GetInt(SHOW_FPS_KEY, DefaultSettings.ShowFPS ? 1 : 0) == 1;
            showFpsToggle.isOn = showFpsEnabled;
            SetShowFPS(showFpsEnabled);
        }

        // Load render distance
        if (renderDistanceSlider != null)
        {
            float renderDistance = PlayerPrefs.GetFloat(RENDER_DISTANCE_KEY, DefaultSettings.RenderDistance);
            renderDistanceSlider.value = renderDistance;
            SetRenderDistance(renderDistance);
        }

        // Load motion blur
        if (motionBlurToggle != null)
        {
            bool motionBlur = PlayerPrefs.GetInt(MOTION_BLUR_KEY, DefaultSettings.MotionBlur ? 1 : 0) == 1;
            motionBlurToggle.isOn = motionBlur;
            SetMotionBlur(motionBlur);
        }

        // Load anti-aliasing
        if (antiAliasingToggle != null)
        {
            bool antiAliasing = PlayerPrefs.GetInt(ANTI_ALIASING_KEY, DefaultSettings.AntiAliasing ? 1 : 0) == 1;
            antiAliasingToggle.isOn = antiAliasing;
            SetAntiAliasing(antiAliasing);
        }

        // Load shadow quality
        if (shadowQualityDropdown != null)
        {
            int shadowQuality = PlayerPrefs.GetInt(SHADOW_QUALITY_KEY, DefaultSettings.ShadowQuality);
            shadowQualityDropdown.value = shadowQuality;
            SetShadowQuality(shadowQuality);
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

        // Update SoundManager if available
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMasterVolume(volume);
        }
        else
        {
            // Fallback to original method if SoundManager not available
            SetAudioMixerVolume(MASTER_VOLUME_PARAM, volume);
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
        }

        UpdateVolumeText(masterVolumeText, volume);
        PlayButtonSoundIfInitialized();
    }


    public void SetMusicVolume(float volume)
    {
        if (!ValidateVolumeSlider(musicVolumeSlider, volume)) return;

        // Update SoundManager if available
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMusicVolume(volume);
        }
        else
        {
            // Fallback to original method
            SetAudioMixerVolume(MUSIC_VOLUME_PARAM, volume);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
        }

        UpdateVolumeText(musicVolumeText, volume);
        PlayButtonSoundIfInitialized();
    }

    public void SetSFXVolume(float volume)
    {
        if (!ValidateVolumeSlider(sfxVolumeSlider, volume)) return;

        // Update SoundManager if available
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXVolume(volume);
        }
        else
        {
            // Fallback to original method
            SetAudioMixerVolume(SFX_VOLUME_PARAM, volume);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
        }

        UpdateVolumeText(sfxVolumeText, volume);
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

    // Graphics Setting Methods (Existing)
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
    private void ApplyBrightnessToRenderer(float brightness)
    {
        RenderSettings.ambientIntensity = brightness;
    }

    // New Graphics Setting Methods
    public void SetFPSLimit(float sliderValue)
    {
        int fpsLimitIndex = Mathf.RoundToInt(sliderValue);
        if (fpsLimitIndex < 0 || fpsLimitIndex >= fpsLimitOptions.Length) return;

        int fpsLimit = fpsLimitOptions[fpsLimitIndex];

        // Apply FPS limit
        if (fpsLimit == -1)
        {
            Application.targetFrameRate = -1; // Unlimited
            if (fpsLimitText != null)
                fpsLimitText.text = "Unlimited";
        }
        else
        {
            Application.targetFrameRate = fpsLimit;
            if (fpsLimitText != null)
                fpsLimitText.text = $"{fpsLimit} FPS";
        }

        PlayerPrefs.SetInt(FPS_LIMIT_KEY, fpsLimitIndex);
        PlayButtonSoundIfInitialized();
    }

    public void SetShowFPS(bool showFpsEnabled)
    {
        showFps = showFpsEnabled;

        if (fpsCounterObject != null)
        {
            fpsCounterObject.SetActive(showFpsEnabled);

            // Cache the text component when enabling for the first time
            if (showFpsEnabled && fpsCounterText == null)
            {
                fpsCounterText = fpsCounterObject.GetComponent<TextMeshProUGUI>();
            }
        }

        PlayerPrefs.SetInt(SHOW_FPS_KEY, showFpsEnabled ? 1 : 0);
        PlayButtonSoundIfInitialized();
    }
    public void SetRenderDistance(float distance)
    {
        if (renderDistanceSlider != null &&
            (distance < renderDistanceSlider.minValue || distance > renderDistanceSlider.maxValue))
            return;

        // Apply render distance to main camera
        if (mainCamera != null)
        {
            mainCamera.farClipPlane = distance;
        }

        // Update display text
        if (renderDistanceText != null)
        {
            renderDistanceText.text = $"{Mathf.RoundToInt(distance)}m";
        }

        PlayerPrefs.SetFloat(RENDER_DISTANCE_KEY, distance);
        PlayButtonSoundIfInitialized();
    }

    public void SetMotionBlur(bool enabled)
    {
        PlayerPrefs.SetInt(MOTION_BLUR_KEY, enabled ? 1 : 0);
        PlayButtonSoundIfInitialized();

    }

    public void SetAntiAliasing(bool enabled)
    {
        // Apply anti-aliasing setting
        if (enabled)
        {
            QualitySettings.antiAliasing = 4; // 4x MSAA
        }
        else
        {
            QualitySettings.antiAliasing = 0; // Disabled
        }

        PlayerPrefs.SetInt(ANTI_ALIASING_KEY, enabled ? 1 : 0);
        PlayButtonSoundIfInitialized();
    }

    public void SetShadowQuality(int qualityIndex)
    {
        if (qualityIndex < 0 || qualityIndex > 2) return;

        switch (qualityIndex)
        {
            case 0: // Disabled
                QualitySettings.shadows = ShadowQuality.Disable;
                break;
            case 1: // Hard Shadows
                QualitySettings.shadows = ShadowQuality.HardOnly;
                break;
            case 2: // Soft Shadows
                QualitySettings.shadows = ShadowQuality.All;
                break;
        }

        PlayerPrefs.SetInt(SHADOW_QUALITY_KEY, qualityIndex);
        PlayButtonSoundIfInitialized();
    }

    // FPS Counter Update
    private void UpdateFPSCounter()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // Use direct reference if available, otherwise try to find it
        TextMeshProUGUI fpsText = fpsCounterText;
        if (fpsText == null && fpsCounterObject != null)
        {
            fpsText = fpsCounterObject.GetComponent<TextMeshProUGUI>();
        }

        if (fpsText != null)
        {
            float fps = 1.0f / deltaTime;
            fpsText.text = $"FPS: {Mathf.Ceil(fps)}";
        }
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

        // Use SoundManager if available
        if (SoundManager.Instance != null)
        {
            // Try to play the assigned clip, otherwise use fallback
            if (buttonClickSound != null)
            {
                SoundManager.Instance.PlaySFX(buttonClickSound);
            }
            else
            {
                SoundManager.Instance.PlayUISound("ButtonClick"); // Fallback to named sound
            }
        }
        else
        {
            // Fallback to original method
            PauseMenuManager.Instance?.PlayButtonSound();
        }
    }
    private void PlaySliderSoundIfInitialized()
    {
        if (!isInitialized) return;

        // Use SoundManager if available
        if (SoundManager.Instance != null)
        {
            if (sliderChangeSound != null)
            {
                SoundManager.Instance.PlaySFX(sliderChangeSound, volume: 0.5f); // Quieter for sliders
            }
            else
            {
                SoundManager.Instance.PlayUISound("SliderChange"); // Fallback to named sound
            }
        }
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

    // New public getters for the new graphics settings
    public int GetCurrentFPSLimit()
    {
        int fpsLimitIndex = PlayerPrefs.GetInt(FPS_LIMIT_KEY, DefaultSettings.FPSLimitIndex);
        return fpsLimitOptions[fpsLimitIndex];
    }

    public bool GetShowFPSSetting()
    {
        return PlayerPrefs.GetInt(SHOW_FPS_KEY, DefaultSettings.ShowFPS ? 1 : 0) == 1;
    }

    public float GetCurrentRenderDistance()
    {
        return PlayerPrefs.GetFloat(RENDER_DISTANCE_KEY, DefaultSettings.RenderDistance);
    }

    public bool GetMotionBlurSetting()
    {
        return PlayerPrefs.GetInt(MOTION_BLUR_KEY, DefaultSettings.MotionBlur ? 1 : 0) == 1;
    }

    public bool GetAntiAliasingSetting()
    {
        return PlayerPrefs.GetInt(ANTI_ALIASING_KEY, DefaultSettings.AntiAliasing ? 1 : 0) == 1;
    }

    public int GetShadowQualitySetting()
    {
        return PlayerPrefs.GetInt(SHADOW_QUALITY_KEY, DefaultSettings.ShadowQuality);
    }

    // Reset all settings to their default values
    public void ResetAllSettings()
    {
        // Audio settings reset
        if (masterVolumeSlider != null) { masterVolumeSlider.value = DefaultSettings.Volume; SetMasterVolume(DefaultSettings.Volume); }
        if (musicVolumeSlider != null) { musicVolumeSlider.value = DefaultSettings.Volume; SetMusicVolume(DefaultSettings.Volume); }
        if (sfxVolumeSlider != null) { sfxVolumeSlider.value = DefaultSettings.Volume; SetSFXVolume(DefaultSettings.Volume); }

        // Original graphics settings reset
        if (brightnessSlider != null) { brightnessSlider.value = DefaultSettings.Brightness; SetBrightness(DefaultSettings.Brightness); }
        if (fullscreenToggle != null) { fullscreenToggle.isOn = DefaultSettings.Fullscreen; SetFullscreen(DefaultSettings.Fullscreen); }
        if (vsyncToggle != null) { vsyncToggle.isOn = DefaultSettings.VSync; SetVSync(DefaultSettings.VSync); }

        // New graphics settings reset
        if (fpsLimitSlider != null) { fpsLimitSlider.value = DefaultSettings.FPSLimitIndex; SetFPSLimit(DefaultSettings.FPSLimitIndex); }
        if (showFpsToggle != null) { showFpsToggle.isOn = DefaultSettings.ShowFPS; SetShowFPS(DefaultSettings.ShowFPS); }
        if (renderDistanceSlider != null) { renderDistanceSlider.value = DefaultSettings.RenderDistance; SetRenderDistance(DefaultSettings.RenderDistance); }
        if (motionBlurToggle != null) { motionBlurToggle.isOn = DefaultSettings.MotionBlur; SetMotionBlur(DefaultSettings.MotionBlur); }
        if (antiAliasingToggle != null) { antiAliasingToggle.isOn = DefaultSettings.AntiAliasing; SetAntiAliasing(DefaultSettings.AntiAliasing); }
        if (shadowQualityDropdown != null) { shadowQualityDropdown.value = DefaultSettings.ShadowQuality; SetShadowQuality(DefaultSettings.ShadowQuality); }

        // Controls reset
        ResetControls();
        PlayButtonSoundIfInitialized();
    }

    // Reset only graphics settings
    public void ResetGraphicsSettings()
    {
        if (brightnessSlider != null) { brightnessSlider.value = DefaultSettings.Brightness; SetBrightness(DefaultSettings.Brightness); }
        if (fullscreenToggle != null) { fullscreenToggle.isOn = DefaultSettings.Fullscreen; SetFullscreen(DefaultSettings.Fullscreen); }
        if (vsyncToggle != null) { vsyncToggle.isOn = DefaultSettings.VSync; SetVSync(DefaultSettings.VSync); }
        if (fpsLimitSlider != null) { fpsLimitSlider.value = DefaultSettings.FPSLimitIndex; SetFPSLimit(DefaultSettings.FPSLimitIndex); }
        if (showFpsToggle != null) { showFpsToggle.isOn = DefaultSettings.ShowFPS; SetShowFPS(DefaultSettings.ShowFPS); }
        if (renderDistanceSlider != null) { renderDistanceSlider.value = DefaultSettings.RenderDistance; SetRenderDistance(DefaultSettings.RenderDistance); }
        if (motionBlurToggle != null) { motionBlurToggle.isOn = DefaultSettings.MotionBlur; SetMotionBlur(DefaultSettings.MotionBlur); }
        if (antiAliasingToggle != null) { antiAliasingToggle.isOn = DefaultSettings.AntiAliasing; SetAntiAliasing(DefaultSettings.AntiAliasing); }
        if (shadowQualityDropdown != null) { shadowQualityDropdown.value = DefaultSettings.ShadowQuality; SetShadowQuality(DefaultSettings.ShadowQuality); }

        PlayButtonSoundIfInitialized();
    }
}