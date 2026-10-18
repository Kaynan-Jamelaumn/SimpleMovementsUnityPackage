using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the local player sees while unconscious: the screen darkens, a countdown to death, how far below 0 health they
/// are (and how far the death threshold is), and a Give Up button / the Give Up key. Built at runtime by
/// <see cref="Unconsciousness"/> (turn Show Screen off there to use your own UI: read <see cref="Unconsciousness.Remaining"/>,
/// <see cref="Unconsciousness.Deficit"/>, <see cref="Unconsciousness.Threshold"/> and call <see cref="Unconsciousness.GiveUp"/>).
/// </summary>
public class UnconsciousScreen : MonoBehaviour
{
    private Unconsciousness owner;
    private CanvasGroup group;
    private Image vignette;
    private TextMeshProUGUI title;
    private TextMeshProUGUI info;
    private TextMeshProUGUI hint;
    private RectTransform timerFill;
    private RectTransform giveUpFill;
    private Button giveUp;
    private float shown;
    private bool cursorFreed;
    private CursorLockMode previousLock;
    private bool previousVisible;

    public static UnconsciousScreen Create(Unconsciousness owner)
    {
        var p = new HudPalette();
        Canvas canvas = UITheme.Canvas("Unconscious Screen", 60, null);
        var screen = canvas.gameObject.AddComponent<UnconsciousScreen>();
        screen.owner = owner;
        screen.group = canvas.gameObject.AddComponent<CanvasGroup>();
        screen.group.alpha = 0f;

        screen.vignette = HudStyle.Image("Darkness", canvas.transform, new Color(0.08f, 0f, 0f, 0.62f));
        HudStyle.Stretch(screen.vignette.rectTransform);
        screen.vignette.raycastTarget = false;

        Image panel = HudStyle.Frame("Panel", canvas.transform, p, 16);
        HudStyle.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(460f, 210f));

        screen.title = HudStyle.Text("Title", panel.transform, "UNCONSCIOUS", 34f, p.warning, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Place(screen.title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(440f, 44f));
        screen.info = HudStyle.Text("Info", panel.transform, "", 16f, p.text);
        HudStyle.Place(screen.info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(440f, 40f));
        screen.info.textWrappingMode = TextWrappingModes.Normal;

        // Countdown bar.
        Image track = HudStyle.Image("Timer", panel.transform, p.track, UITheme.RoundedSprite(6), true);
        HudStyle.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(400f, 14f));
        Image fill = HudStyle.Image("Fill", track.transform, p.health, UITheme.RoundedSprite(5), true);
        screen.timerFill = HudStyle.Stretch(fill.rectTransform, 2f);

        screen.giveUp = UITheme.Button("Give Up", panel.transform, "Give Up", () => owner.GiveUp(), UITheme.ButtonStyle.Danger, 160f, 38f);
        HudStyle.Place((RectTransform)screen.giveUp.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(160f, 38f));
        Image hold = HudStyle.Image("Hold", screen.giveUp.transform, new Color(1f, 1f, 1f, 0.25f));
        screen.giveUpFill = hold.rectTransform;
        screen.giveUpFill.anchorMin = Vector2.zero;
        screen.giveUpFill.anchorMax = new Vector2(0f, 1f);
        screen.giveUpFill.offsetMin = screen.giveUpFill.offsetMax = Vector2.zero;

        screen.hint = HudStyle.Text("Hint", panel.transform, "", 13f, p.muted);
        HudStyle.Place(screen.hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(440f, 20f));
        canvas.gameObject.SetActive(false);
        return screen;
    }

    public void Hide()
    {
        RestoreCursor();
        if (gameObject != null) gameObject.SetActive(false);
        shown = 0f;
    }

    // The Give Up button needs the mouse: the cursor is freed while the screen shows, then put back.
    private void FreeCursor()
    {
        if (cursorFreed) return;
        cursorFreed = true;
        previousLock = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!cursorFreed) return;
        cursorFreed = false;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;
    }

    private void OnDisable() => RestoreCursor();

    /// <summary>Called every frame by the owner.</summary>
    public void Tick()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }
        bool down = owner.IsDown;
        if (!down)
        {
            RestoreCursor();
            if (gameObject.activeSelf)
            {
                shown = Mathf.MoveTowards(shown, 0f, Time.unscaledDeltaTime * 3f);
                group.alpha = shown;
                if (shown <= 0f) gameObject.SetActive(false);
            }
            return;
        }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (owner.Rules.allowGiveUp) FreeCursor();
        shown = Mathf.MoveTowards(shown, 1f, Time.unscaledDeltaTime * 2f);
        group.alpha = shown;
        group.interactable = group.blocksRaycasts = true;

        DownedRules r = owner.Rules;
        float remaining = owner.Remaining;
        float threshold = owner.Threshold;
        string time = float.IsInfinity(remaining) ? "Waiting for help..." : $"Death in {Mathf.CeilToInt(remaining)} s - waiting for help...";
        info.text = $"{time}\n<size=80%>Health {-owner.Deficit:0} (dies at {-threshold:0})</size>";
        float t = float.IsInfinity(remaining) ? 1f : Mathf.Clamp01(remaining / Mathf.Max(1f, owner.Duration));
        timerFill.anchorMax = new Vector2(t, 1f);
        // A slow pulse of the darkness, stronger near the end.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (t < 0.25f ? 5f : 2f));
        vignette.color = new Color(0.08f, 0f, 0f, 0.55f + 0.12f * pulse);

        giveUp.gameObject.SetActive(r.allowGiveUp);
        giveUpFill.anchorMax = new Vector2(owner.GiveUpProgress, 1f);
        hint.text = r.allowGiveUp ? $"Hold [{owner.GiveUpKey}] to give up" : "";
    }
}
