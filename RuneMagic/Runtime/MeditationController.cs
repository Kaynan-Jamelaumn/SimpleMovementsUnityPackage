using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The rune user's Meditate input (design spec 15.1): press it to meditate (the efficient way to stabilize the soul),
    /// press it again, move, or get hit to stop. While meditating or performing a ritual the screen shows the soul and how
    /// fast it recovers; messages about the soul (a refused meditation, a finished ritual, a collapse) appear here too.
    /// Only the locally controlled player reads input; in a network session the request goes to the server.
    /// <para>Input: the Input System only - an assigned Input Action, else the action named in the Rune Magic Database
    /// (Settings ▸ Meditate Action, "Meditate") in the player's Input Actions asset. No key is fixed in code: bind the
    /// action in the Input Actions editor (players rebind it in Key Bindings). Players that are not rune users ignore it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SimpleMovements/Rune Magic/Meditation Controller")]
    public class MeditationController : MonoBehaviour
    {
        [Tooltip("Optional: the Meditate action of your Input Actions asset. Empty = the action named in the Rune Magic Database (Settings ▸ Meditate Action).")]
        [SerializeField] private InputActionReference meditateAction;
        [Tooltip("Show the soul while meditating and soul messages (local player).")]
        [SerializeField] private bool showScreen = true;
        [Tooltip("Seconds a soul message stays on screen.")]
        [SerializeField, Min(0.5f)] private float messageSeconds = 2.5f;

        private RuneSoul soul;
        private CombatEntity entity;
        private CombatInputBinding input;
        private float retryInputAt;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim;
        private TextMeshProUGUI text;
        private int seenMessage = -1;
        private string message;
        private float messageUntil;
        private float shown;

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
            if (entity == null) entity = CombatEntity.Resolve(gameObject);
            if (entity == null) entity = GetComponentInParent<CombatEntity>();
            if (entity == null) return false;
            if (!GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)) return GameNet.NetAuthority.HasLocalPlayer;
            return GameNet.NetAuthority.IsLocallyControlled(entity.gameObject);
        }

        private void Update()
        {
            if (soul == null) soul = RuneSoul.For(this);
            if (soul == null || !IsLocal())
                return;
            RuneSoulView v = soul.View;
            if (seenMessage < 0) seenMessage = v.messageSeq; // only messages from now on
            if (!v.runeUser)
            {
                if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
                return;
            }
            // The action may come later (the input actions are set up after this component): looked for again now and then.
            if (input != null && !input.HasAny && Time.unscaledTime >= retryInputAt)
            {
                input.Dispose();
                input = null;
            }
            if (input == null)
            {
                input = RuneMagicInput.Player(this, meditateAction, RuneMagicInput.MeditateAction, "Meditate");
                retryInputAt = Time.unscaledTime + 5f;
            }
            if (input != null && input.Pressed)
                Toggle();
            if (v.messageSeq != seenMessage)
            {
                seenMessage = v.messageSeq;
                if (!string.IsNullOrEmpty(v.message))
                {
                    message = v.message;
                    messageUntil = Time.unscaledTime + messageSeconds;
                }
            }
            if (showScreen) DrawScreen(v);
        }

        /// <summary>Starts meditating, or stops meditating / the ritual.</summary>
        public void Toggle()
        {
            if (soul != null)
                soul.RequestMeditation(!soul.View.IsChannelling);
        }

        private string KeyText => RuneMagicInput.KeyLabel(input != null ? input.ReadAction : null, RuneMagicInput.MeditateAction);

        private void DrawScreen(RuneSoulView v)
        {
            bool focused = v.IsChannelling;
            bool talking = Time.unscaledTime < messageUntil;
            if (!focused && !talking && (canvas == null || !canvas.gameObject.activeSelf))
                return;
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            shown = Mathf.MoveTowards(shown, focused ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
            group.alpha = 1f;
            dim.color = new Color(0.02f, 0.06f, 0.08f, 0.45f * shown);
            if (focused)
            {
                string what = v.channel == SoulChannel.Ritual ? $"Ritual: {v.channelName} {v.channelProgress * 100f:0}%"
                    : v.channelProgress < 1f ? "Settling into meditation…" : "Meditating";
                string rate = v.recoveryPerMinute > 0f ? $"  (+{v.recoveryPerMinute:0.#}/min)" : "";
                string deep = v.deepDamage > 0.05f ? $"\n<size=70%>Deep damage {v.deepDamage:0.#} (needs specialized healing)</size>" : "";
                string line = talking ? $"\n<size=70%>{message}</size>" : "";
                text.text = $"{what}\n<size=75%>Soul {v.integrity:0.#} / {v.maxIntegrity:0.#} · {v.conditionName}{rate}   [{KeyText}] Stop</size>{deep}{line}";
            }
            else if (talking)
                text.text = message;
            else if (shown <= 0f)
                canvas.gameObject.SetActive(false);
        }

        private void Build()
        {
            var p = new HudPalette();
            canvas = UITheme.Canvas("Meditation Screen", 54, null);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            dim = HudStyle.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0f));
            HudStyle.Stretch(dim.rectTransform);
            text = HudStyle.Text("Text", canvas.transform, "", 24f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudStyle.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 120f));
        }
    }
}
