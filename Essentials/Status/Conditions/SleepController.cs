using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The player's Sleep input: press it anywhere to lie down and sleep on the ground (a poor sleep), press it again to wake
/// up. Beds and benches are used with Interact instead (<see cref="SleepSpot"/>). While asleep the screen dims and shows
/// the rest and how to wake up. Only the locally controlled player reads input; in a network session the request goes
/// to the server (<see cref="PlayerStatusController.StartSleeping"/>).
/// <para>Input: an assigned action, else an action named Sleep / Rest in the player's input actions, else one is
/// created on the default controls (Z, gamepad D-pad down).</para>
/// </summary>
[DisallowMultipleComponent]
public class SleepController : MonoBehaviour
{
    [Tooltip("The Sleep action (optional).")]
    [SerializeField] private InputActionReference sleepAction;
    [SerializeField] private string[] sleepActionNames = { "Sleep", "Rest" };
    [SerializeField] private string[] sleepDefaultControls = { "<Keyboard>/z", "<Gamepad>/dpad/down" };
    [Tooltip("Pressing the Sleep input anywhere sleeps on the ground (off: only Sleep Spots).")]
    [SerializeField] private bool allowGroundSleep = true;
    [Tooltip("Pressing the Sleep input while asleep wakes up.")]
    [SerializeField] private bool pressAgainToWake = true;
    [Tooltip("Dim the screen and show the rest while asleep (local player).")]
    [SerializeField] private bool showScreen = true;

    private SleepManager sleep;
    private PlayerStatusController status;
    private CombatEntity entity;
    private CombatInputBinding input;
    private Canvas canvas;
    private CanvasGroup group;
    private TextMeshProUGUI text;
    private string message;
    private float messageUntil;
    private float shown;

    private void Awake()
    {
        sleep = GetComponentInChildren<SleepManager>() ?? GetComponentInParent<SleepManager>();
        status = GetComponentInChildren<PlayerStatusController>() ?? GetComponentInParent<PlayerStatusController>();
    }

    private void OnDisable()
    {
        input?.Dispose();
        input = null;
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private bool IsLocal()
    {
        if (entity == null) entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>();
        if (entity == null) return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)) return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(entity.gameObject);
    }

    private void Update()
    {
        if (sleep == null || !IsLocal())
            return;
        if (input == null)
            input = CombatInputBinding.Create(this, sleepAction, sleepActionNames, sleepDefaultControls, "Sleep");
        if (input != null && input.Pressed)
            Toggle();
        if (showScreen) DrawScreen();
    }

    /// <summary>Sleeps on the ground, or wakes up when asleep.</summary>
    public void Toggle()
    {
        if (sleep.IsSleeping)
        {
            if (!pressAgainToWake) return;
            if (status != null) status.StopSleeping();
            else sleep.StopSleeping();
            return;
        }
        if (!allowGroundSleep) return;
        if (!sleep.IsReplica && !sleep.CanSleepNow(out string reason))
        {
            Say(reason);
            return;
        }
        if (status != null) status.StartSleeping();
        else sleep.StartSleeping();
    }

    private void Say(string m)
    {
        if (string.IsNullOrEmpty(m)) return;
        message = m;
        messageUntil = Time.unscaledTime + 2f;
    }

    private string KeyText => input != null && input.ReadAction != null ? HudStyle.ShortKey(input.ReadAction.GetBindingDisplayString(0)) : "Z";

    private void DrawScreen()
    {
        bool asleep = sleep.IsSleeping;
        bool talking = Time.unscaledTime < messageUntil;
        if (!asleep && !talking && (canvas == null || !canvas.gameObject.activeSelf))
            return;
        if (canvas == null) Build();
        canvas.gameObject.SetActive(true);
        shown = Mathf.MoveTowards(shown, asleep ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
        group.alpha = asleep ? shown : 1f;
        Image dim = canvas.transform.GetChild(0).GetComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.08f, 0.7f * shown);
        if (asleep)
        {
            SleepQuality q = sleep.CurrentQuality;
            string where = q != null && !string.IsNullOrEmpty(q.name) ? $" ({q.name})" : "";
            text.text = $"{(sleep.HasCollapsed ? "Collapsed from exhaustion" : "Sleeping")}{where}\n<size=75%>Rest {sleep.Fraction * 100f:0}%" +
                        (pressAgainToWake && !sleep.HasCollapsed ? $"   [{KeyText}] Wake up" : "") + "</size>";
        }
        else if (talking)
            text.text = message;
        else if (shown <= 0f)
            canvas.gameObject.SetActive(false);
    }

    private void Build()
    {
        var p = new HudPalette();
        canvas = UITheme.Canvas("Sleep Screen", 55, null);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image dim = HudStyle.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0f));
        HudStyle.Stretch(dim.rectTransform);
        text = HudStyle.Text("Text", canvas.transform, "", 26f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 80f));
    }
}
