using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The NPC's conversation window: portrait, name and title, what the NPC says, and either its options (Trade, Talk...,
/// Goodbye) or a Continue button while reading dialogue lines. Built from code when the NPC has no Dialogue Window
/// Prefab; a prefab of your own needs this component with its fields assigned - every field is optional, and the option
/// buttons are copies of <see cref="optionButtonTemplate"/> (or generated).
/// </summary>
[DisallowMultipleComponent]
public class NPCDialogueWindow : NPCWindow
{
    [SerializeField] private Image portrait;
    [Tooltip("Hidden when the NPC has no portrait (empty = the portrait image itself is hidden).")]
    [SerializeField] private GameObject portraitFrame;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [Tooltip("Parent of the option buttons.")]
    [SerializeField] private RectTransform optionsContainer;
    [Tooltip("Button copied for each option (a child Text Mesh Pro label is set). Empty = generated buttons.")]
    [SerializeField] private Button optionButtonTemplate;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    [Tooltip("Text of the option that ends the conversation.")]
    [SerializeField] private string goodbyeLabel = "Goodbye";

    private readonly List<Button> optionButtons = new List<Button>();
    private IReadOnlyList<string> lines;
    private int lineIndex;
    private Action linesFinished;
    private bool showingLines;

    /// <summary>The dialogue window for <paramref name="session"/>'s canvas (made once per canvas and prefab).</summary>
    public static NPCDialogueWindow For(NPCInteractionSession session, NPCDialogueWindow prefab)
    {
        NPCDialogueWindow w = NPCWindowCache.Get(session.Canvas, prefab, null, Create);
        w.WireButtons();
        return w;
    }

    /// <summary>Hides the dialogue window shown for <paramref name="session"/> (if any).</summary>
    public static void HideFor(NPCInteractionSession session)
    {
        if (session?.Npc == null)
            return;
        NPCDialogueWindow w = NPCWindowCache.TryGet(session.Canvas, session.Npc.DialogueWindowPrefab, null);
        if (w != null && ReferenceEquals(w.Session, session))
            w.Unbind();
    }

    /// <summary>Shows the NPC's greeting and its options; <paramref name="pick"/> is called with the chosen option.</summary>
    public void ShowMenu(NPCInteractionSession session, string text, IReadOnlyList<NPCBehaviour> options, Action<NPCBehaviour> pick)
    {
        Bind(session);
        showingLines = false;
        lines = null;
        linesFinished = null;
        FillHeader(session.Npc);
        SetBody(string.IsNullOrWhiteSpace(text) ? "" : text);
        ClearOptions();
        if (options != null)
            foreach (NPCBehaviour b in options)
            {
                if (b == null) continue;
                NPCBehaviour option = b;
                bool available = option.IsAvailable(session, out string reason);
                string label = available || string.IsNullOrEmpty(reason) ? option.OptionLabel : $"{option.OptionLabel}  <size=80%><color=#bbbbbb>({reason})</color></size>";
                AddOption(label, () => pick?.Invoke(option), available);
            }
        AddOption(goodbyeLabel, RequestClose, true);
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
        Relayout();
    }

    /// <summary>Shows <paramref name="dialogueLines"/> one after another (Continue / Enter / Space), then calls <paramref name="finished"/>.</summary>
    public void ShowLines(NPCInteractionSession session, IReadOnlyList<string> dialogueLines, Action finished)
    {
        Bind(session);
        FillHeader(session.Npc);
        ClearOptions();
        lines = dialogueLines;
        lineIndex = 0;
        linesFinished = finished;
        showingLines = true;
        ShowLine();
    }

    private void ShowLine()
    {
        if (lines == null || lineIndex >= lines.Count)
        {
            showingLines = false;
            Action done = linesFinished;
            linesFinished = null;
            done?.Invoke();
            return;
        }
        SetBody(lines[lineIndex]);
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            TextMeshProUGUI label = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = lineIndex >= lines.Count - 1 ? "Done" : "Continue  ▶";
        }
        Relayout();
    }

    /// <summary>Next line (Continue button, Enter, Space, gamepad A).</summary>
    public void Continue()
    {
        if (!showingLines)
            return;
        lineIndex++;
        ShowLine();
    }

    public override void RequestClose()
    {
        NPCInteractionSession s = Session;
        if (s != null && s.ActiveBehaviour != null && showingLines)
        {
            showingLines = false; // reading lines of an option: the option is finished (back to the menu or the end)
            s.BehaviourFinished(s.ActiveBehaviour);
            return;
        }
        base.RequestClose();
    }

    protected override void Update()
    {
        base.Update();
        if (Session == null || !showingLines)
            return;
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
            (pad != null && pad.buttonSouth.wasPressedThisFrame))
            Continue();
    }

    protected override void OnUnbound()
    {
        showingLines = false;
        lines = null;
        linesFinished = null;
    }

    // ------------------------------------------------------------------ parts
    private void FillHeader(NPC npc)
    {
        if (npc == null)
            return;
        if (nameText != null) nameText.text = npc.DisplayName;
        if (titleText != null)
        {
            titleText.text = npc.Title ?? "";
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(npc.Title));
        }
        if (portrait != null)
        {
            portrait.sprite = npc.Portrait;
            (portraitFrame != null ? portraitFrame : portrait.gameObject).SetActive(npc.Portrait != null);
        }
    }

    private void SetBody(string text)
    {
        if (bodyText == null)
            return;
        bodyText.text = text ?? "";
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void ClearOptions()
    {
        foreach (Button b in optionButtons)
            if (b != null)
                Destroy(b.gameObject);
        optionButtons.Clear();
    }

    private void AddOption(string label, UnityEngine.Events.UnityAction onClick, bool interactable)
    {
        if (optionsContainer == null)
            return;
        Button button;
        if (optionButtonTemplate != null)
        {
            button = Instantiate(optionButtonTemplate, optionsContainer, false);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            TextMeshProUGUI t = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.text = label;
        }
        else
        {
            button = InventoryUIFactory.Button("Option", optionsContainer, label, onClick);
            InventoryUIFactory.Size(button, -1f, 38f, 1f);
            TextMeshProUGUI t = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
            {
                t.alignment = TextAlignmentOptions.Left;
                t.margin = new Vector4(14f, 0f, 8f, 0f);
                t.richText = true;
            }
        }
        button.interactable = interactable;
        optionButtons.Add(button);
    }

    private void Relayout()
    {
        var rt = (RectTransform)transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private bool wired;

    private void WireButtons()
    {
        if (wired)
            return;
        wired = true;
        if (optionButtonTemplate != null)
            optionButtonTemplate.gameObject.SetActive(false);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Continue);
            continueButton.gameObject.SetActive(false);
        }
        if (closeButton != null)
            closeButton.onClick.AddListener(RequestClose);
    }

    /// <summary>Builds the default window under <paramref name="parent"/> (bottom centre of the screen).</summary>
    public static NPCDialogueWindow Create(Transform parent)
    {
        Image bg = InventoryUIFactory.Panel("NPCDialogueWindow", parent, InventoryUIFactory.PanelColor);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(820f, 0f);
        InventoryUIFactory.Vertical(bg.gameObject, 18, 10f, true);
        var w = bg.gameObject.AddComponent<NPCDialogueWindow>();

        RectTransform header = InventoryUIFactory.Rect("Header", rt);
        InventoryUIFactory.Horizontal(header.gameObject, 12f);
        InventoryUIFactory.Size(header, -1f, 64f);
        Image frame = InventoryUIFactory.Panel("PortraitFrame", header, InventoryUIFactory.FieldColor);
        frame.raycastTarget = false;
        InventoryUIFactory.Size(frame, 64f, 64f, 0f);
        w.portrait = InventoryUIFactory.Panel("Portrait", frame.transform, Color.white);
        w.portrait.preserveAspect = true;
        w.portrait.raycastTarget = false;
        InventoryUIFactory.Stretch(w.portrait.rectTransform, 3f, 3f);
        w.portraitFrame = frame.gameObject;
        RectTransform names = InventoryUIFactory.Rect("Names", header);
        InventoryUIFactory.Vertical(names.gameObject, 0, 2f, false).childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(names, -1f, -1f, 1f);
        w.nameText = InventoryUIFactory.Text("Name", names, "NPC", 24f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        w.titleText = InventoryUIFactory.Text("Title", names, "", 16f, InventoryUIFactory.MutedColor);
        w.closeButton = InventoryUIFactory.Button("Close", header, "✕", null, 36f);

        w.bodyText = InventoryUIFactory.Text("Body", rt, "", 19f, InventoryUIFactory.TextColor);
        w.bodyText.richText = true;

        RectTransform options = InventoryUIFactory.Rect("Options", rt);
        InventoryUIFactory.Vertical(options.gameObject, 0, 6f, false);
        w.optionsContainer = options;

        RectTransform footer = InventoryUIFactory.Rect("Footer", rt);
        HorizontalLayoutGroup fh = InventoryUIFactory.Horizontal(footer.gameObject, 8f);
        fh.childAlignment = TextAnchor.MiddleRight;
        InventoryUIFactory.Size(footer, -1f, 36f);
        RectTransform spacer = InventoryUIFactory.Rect("Spacer", footer);
        InventoryUIFactory.Size(spacer, -1f, -1f, 1f);
        w.continueButton = InventoryUIFactory.Button("Continue", footer, "Continue  ▶", null, 160f);
        bg.gameObject.SetActive(false);
        return w;
    }
}
