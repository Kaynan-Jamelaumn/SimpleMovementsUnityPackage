using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// Comprehensive pause menu system with smooth transitions, audio management, and state control
// Implements singleton pattern for global access and persistence across scenes
public class PauseMenuManager : MonoBehaviour
{
    // UI References - Core pause menu components
    [Header("Pause Settings")]
    public GameObject pauseMenuUI;          // Main pause menu GameObject
    public CanvasGroup pauseCanvasGroup;    // For smooth alpha transitions and interaction control

    // Visual Effects - Smooth transitions and background effects
    [Header("Visual Effects")]
    public GameObject backgroundBlur;       // Optional blur effect behind pause menu
    [Range(0.1f, 10f)]
    public float fadeSpeed = 3f;           // Speed of UI fade in/out transitions
    [Range(0.1f, 10f)]
    public float blurTransitionSpeed = 2f; // Speed of background blur transitions

    // Audio System - Complete audio management during pause
    [Header("Audio")]
    public AudioMixerGroup masterMixer;     // Reference to main audio mixer for volume control
    public AudioSource pauseSFX;           // Dedicated audio source for pause menu sounds
    public AudioClip pauseOpenSound;       // Sound when opening pause menu
    public AudioClip pauseCloseSound;      // Sound when closing pause menu
    public AudioClip buttonClickSound;     // Sound for button interactions
    [Range(-80f, 0f)]
    public float pausedVolumeLevel = -20f; // Audio volume level when game is paused

    // Context-Aware Pausing - Prevents pausing in inappropriate situations
    [Header("Input Prevention")]
    public bool canPauseDuringCutscenes = false;  // Allow/prevent pausing during cutscenes
    public bool preventPauseInMenus = true;       // Prevent pausing in main menu scenes

    // Resume Countdown Feature - Optional countdown before resuming gameplay
    [Header("Resume Countdown")]
    public bool useResumeCountdown = false;       // Enable 3-2-1 countdown before resume
    public GameObject countdownUI;                // UI container for countdown display
    public TMPro.TextMeshProUGUI countdownText;   // Text component showing countdown numbers

    // Public Properties - External systems can check pause state
    public bool IsPaused { get; private set; }                    // Current pause state
    public static PauseMenuManager Instance { get; private set; } // Singleton instance

    // Private State Management - Internal tracking variables
    private bool isInCutscene = false;      // Track if currently in a cutscene
    private bool isInMainMenu = false;      // Track if in main menu scene
    private Coroutine activeTransitionCoroutine; // Current UI transition coroutine
    private Coroutine activeCountdownCoroutine;  // Current countdown coroutine
    private float originalTimeScale = 1f;        // Store original time scale for restoration
    private float originalAudioVolume = 0f;      // Store original audio volume for restoration
    private bool isInitialized = false;          // Ensure proper initialization before use

    // Configuration Constants - System-wide constants for consistency
    private const string MASTER_VOLUME_PARAM = "MasterVolume"; // Audio mixer parameter name
    private const float TRANSITION_EPSILON = 0.01f;            // Precision threshold for transitions

    // INITIALIZATION SYSTEM
    // Ensures proper setup and singleton pattern implementation

    void Awake()
    {
        InitializeSingleton();   // Set up singleton pattern
        CacheOriginalValues();   // Store original game state values
    }

    void Start()
    {
        InitializeComponents();  // Initialize UI components and states
        EnsureGameIsUnpaused(); // Force game to start in unpaused state
    }

    void OnDestroy()
    {
        CleanupSingleton(); // Clean up singleton reference on destruction
    }

    // Singleton Pattern Implementation - Ensures only one pause manager exists
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scene changes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }

    // Store Original Game State - Cache values that need to be restored after pausing
    private void CacheOriginalValues()
    {
        originalTimeScale = Time.timeScale; // Store normal game speed

        // Cache the current audio mixer volume level
        if (masterMixer?.audioMixer != null)
        {
            masterMixer.audioMixer.GetFloat(MASTER_VOLUME_PARAM, out originalAudioVolume);
        }
    }

    // Component Setup - Initialize all UI components and ensure proper state
    private void InitializeComponents()
    {
        if (Instance != this) return; // Only initialize for the singleton instance

        // Ensure we have a CanvasGroup component for smooth transitions
        EnsureCanvasGroupExists();

        // Force all UI elements to start in inactive/unpaused state
        ForceInitializeUIState();

        isInitialized = true; // Mark as ready for use
    }

    // Canvas Group Setup - Ensure CanvasGroup exists for smooth alpha transitions
    private void EnsureCanvasGroupExists()
    {
        if (pauseMenuUI != null && pauseCanvasGroup == null)
        {
            // Try to get existing CanvasGroup component
            pauseCanvasGroup = pauseMenuUI.GetComponent<CanvasGroup>();
            if (pauseCanvasGroup == null)
            {
                // Add CanvasGroup if it doesn't exist
                pauseCanvasGroup = pauseMenuUI.AddComponent<CanvasGroup>();
            }
        }
    }

    // Force Unpaused State - Override any incorrect initial states
    private void ForceInitializeUIState()
    {
        // Ensure game starts unpaused regardless of inspector settings
        IsPaused = false;
        Time.timeScale = originalTimeScale;

        // Deactivate all pause-related UI elements
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (backgroundBlur != null) backgroundBlur.SetActive(false);
        if (countdownUI != null) countdownUI.SetActive(false);

        // Set up CanvasGroup for smooth transitions (invisible and non-interactive)
        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;              // Completely transparent
            pauseCanvasGroup.interactable = false;    // Cannot interact with UI elements
            pauseCanvasGroup.blocksRaycasts = false;  // UI doesn't block mouse/touch input
        }

        // Set cursor to game mode (hidden and locked)
        SetCursorState(false);

        // Restore normal audio volume
        RestoreAudioVolume();
    }

    // Safety Check - Additional verification that game starts unpaused
    private void EnsureGameIsUnpaused()
    {
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = originalTimeScale;
        }
    }

    // INPUT HANDLING SYSTEM
    // Processes pause input and determines if pausing is allowed

    // New Input System Callback - Handles pause input from Input Action
    public void OnPause(InputAction.CallbackContext context)
    {
        // Only respond to key press (not hold or release)
        if (context.performed && CanPause())
        {
            TogglePause();
        }
    }

    // Pause Permission System - Checks various conditions before allowing pause
    private bool CanPause()
    {
        if (!isInitialized) return false; // Don't allow pause before initialization

        // Check cutscene restrictions
        if (isInCutscene && !canPauseDuringCutscenes) return false;

        // Check main menu restrictions
        if (isInMainMenu && preventPauseInMenus) return false;

        return true; // All conditions passed
    }

    // CORE PAUSE/RESUME LOGIC
    // Main system for toggling between paused and unpaused states

    // Main Toggle Function - Switches between pause and resume
    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // Pause Implementation - Stops game and shows pause menu
    private void PauseGame()
    {
        if (IsPaused) return; // Prevent double-pausing

        IsPaused = true;

        // Stop any ongoing UI transitions to prevent conflicts
        StopActiveTransitions();

        // Freeze game time (affects most game systems but not UI)
        Time.timeScale = 0f;

        // Show and unlock cursor for menu navigation
        SetCursorState(true);

        // Reduce audio volume to indicate paused state
        SetAudioVolume(pausedVolumeLevel);

        // Play pause open sound effect
        PlayPauseSFX(pauseOpenSound);

        // Display pause menu with smooth transition
        ShowPauseUI();
    }

    // Resume Entry Point - Handles countdown or immediate resume
    public void ResumeGame()
    {
        if (!IsPaused) return; // Can't resume if not paused

        // Use countdown if enabled, otherwise resume immediately
        if (useResumeCountdown && countdownUI != null)
        {
            StartResumeCountdown();
        }
        else
        {
            ExecuteResume();
        }
    }

    // Immediate Resume Implementation - Restores game state without countdown
    private void ExecuteResume()
    {
        IsPaused = false;

        // Stop any active transitions
        StopActiveTransitions();

        // Start the smooth resume transition
        activeTransitionCoroutine = StartCoroutine(ResumeTransition());
    }

    // UI MANAGEMENT SYSTEM
    // Handles smooth transitions and visual effects

    // Show Pause UI - Activates pause menu with smooth fade-in
    private void ShowPauseUI()
    {
        // Activate UI GameObjects
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
        if (backgroundBlur != null) backgroundBlur.SetActive(true);

        // Start smooth fade-in animation
        activeTransitionCoroutine = StartCoroutine(FadeInUI());
    }

    // Fade In Animation - Smoothly shows pause menu
    private IEnumerator FadeInUI()
    {
        if (pauseCanvasGroup != null)
        {
            // Fade from transparent to opaque
            yield return StartCoroutine(FadeCanvasGroup(pauseCanvasGroup, 0f, 1f));

            // Enable user interaction after fade completes
            pauseCanvasGroup.interactable = true;
            pauseCanvasGroup.blocksRaycasts = true;
        }
    }

    // Resume Transition - Smoothly hides pause menu and restores game
    private IEnumerator ResumeTransition()
    {
        // Immediately disable user interaction
        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;

            // Fade from opaque to transparent
            yield return StartCoroutine(FadeCanvasGroup(pauseCanvasGroup, 1f, 0f));
        }

        // Hide UI elements after fade completes
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (backgroundBlur != null) backgroundBlur.SetActive(false);

        // Restore normal game state
        Time.timeScale = originalTimeScale; // Resume game time
        SetCursorState(false);              // Hide and lock cursor
        RestoreAudioVolume();               // Restore normal audio volume

        // Play resume sound effect
        PlayPauseSFX(pauseCloseSound);
    }

    // COUNTDOWN SYSTEM
    // Optional 3-2-1 countdown before resuming gameplay

    // Start Countdown - Initiates the resume countdown sequence
    private void StartResumeCountdown()
    {
        StopActiveCountdown(); // Stop any existing countdown
        activeCountdownCoroutine = StartCoroutine(ResumeCountdownCoroutine());
    }

    // Countdown Implementation - Shows 3-2-1 countdown with audio cues
    private IEnumerator ResumeCountdownCoroutine()
    {
        if (countdownUI != null) countdownUI.SetActive(true);

        // Hide main pause menu during countdown
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        // Count down from 3 to 1
        for (int i = 3; i > 0; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            PlayPauseSFX(buttonClickSound); // Audio cue for each number
            yield return new WaitForSecondsRealtime(1f); // Use real-time since game time is paused
        }

        // Show final "RESUME!" message
        if (countdownText != null) countdownText.text = "RESUME!";
        yield return new WaitForSecondsRealtime(0.5f);

        // Hide countdown UI and execute resume
        if (countdownUI != null) countdownUI.SetActive(false);
        ExecuteResume();
    }

    // Smooth Canvas Group Transition - Animates alpha value over time
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha)
    {
        if (canvasGroup == null) yield break;

        // Calculate transition duration based on alpha difference and fade speed
        float duration = Mathf.Abs(endAlpha - startAlpha) / fadeSpeed;
        float elapsed = 0f;

        // Animate alpha value over time
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time since game might be paused
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            // Early exit if close enough to target (performance optimization)
            if (Mathf.Abs(canvasGroup.alpha - endAlpha) < TRANSITION_EPSILON)
            {
                break;
            }

            yield return null; // Wait one frame
        }

        // Ensure exact final value
        canvasGroup.alpha = endAlpha;
    }

    // Cursor Management - Controls cursor visibility and lock state
    private void SetCursorState(bool showCursor)
    {
        Cursor.visible = showCursor;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // Audio Volume Control - Sets master audio volume level
    private void SetAudioVolume(float volumeLevel)
    {
        if (masterMixer?.audioMixer != null)
        {
            masterMixer.audioMixer.SetFloat(MASTER_VOLUME_PARAM, volumeLevel);
        }
    }

    // Audio Restoration - Restores original audio volume
    private void RestoreAudioVolume()
    {
        SetAudioVolume(originalAudioVolume);
    }

    // Sound Effect Player - Plays pause menu sound effects
    private void PlayPauseSFX(AudioClip clip)
    {
        if (pauseSFX != null && clip != null)
        {
            pauseSFX.PlayOneShot(clip); // Play sound without interrupting other sounds
        }
    }

    // Transition Cleanup - Stops active UI transition coroutines
    private void StopActiveTransitions()
    {
        if (activeTransitionCoroutine != null)
        {
            StopCoroutine(activeTransitionCoroutine);
            activeTransitionCoroutine = null;
        }
    }

    // Countdown Cleanup - Stops active countdown coroutine
    private void StopActiveCountdown()
    {
        if (activeCountdownCoroutine != null)
        {
            StopCoroutine(activeCountdownCoroutine);
            activeCountdownCoroutine = null;

            // Hide countdown UI if it was showing
            if (countdownUI != null) countdownUI.SetActive(false);
        }
    }

    // Singleton Cleanup - Clean up singleton reference on destruction
    private void CleanupSingleton()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // PUBLIC BUTTON METHODS
    // Methods called by UI buttons (connected via Unity Inspector)

    // Resume Button Handler - Called by Resume button in pause menu
    public void ResumeButton()
    {
        if (!IsPaused) return;

        PlayPauseSFX(buttonClickSound); // Button click sound
        ResumeGame();
    }

    // Main Menu Button Handler - Returns to main menu scene
    public void ReturnToMainMenu()
    {
        PlayPauseSFX(buttonClickSound);
        Time.timeScale = originalTimeScale; // Restore time scale before scene change
        SceneManager.LoadScene("MainMenu");
    }

    // Exits the application
    public void QuitGame()
    {
        PlayPauseSFX(buttonClickSound);

        // Different behavior for editor vs build
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop play mode in editor
#else
        Application.Quit(); // Quit application in build
#endif
    }

    // Generic Button Sound - For buttons that only need sound feedback
    public void PlayButtonSound()
    {
        PlayPauseSFX(buttonClickSound);
    }

    // PUBLIC STATE MANAGEMENT
    // Methods for external systems to control pause behavior

    // Cutscene State Control - External cutscene system can prevent pausing
    public void SetCutsceneState(bool inCutscene)
    {
        isInCutscene = inCutscene;

        // Auto-resume if paused when cutscene starts and cutscenes don't allow pausing
        if (inCutscene && IsPaused && !canPauseDuringCutscenes)
        {
            ResumeGame();
        }
    }

    // Main Menu State Control - Scene management can prevent pausing in menus
    public void SetMainMenuState(bool inMainMenu)
    {
        isInMainMenu = inMainMenu;
    }
}