using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The character creation screen, built from code: four steps (Race, Class, Traits, Appearance) on the left, a live
/// 3D preview in the middle (drag to turn, scroll or Face to look closer, height slider), and the name with a summary
/// of everything the character gets on the right: resources, attributes, race and class bonuses, traits and abilities
/// with their descriptions. The rules are the same ones the server checks (<see cref="CharacterBuild"/>).
/// Used by the <see cref="FrontEndMenu"/>; <see cref="Created"/> gives the saved character.
/// </summary>
public class CharacterCreatorPanel
{
    public enum Step { Race, Class, Traits, Appearance }

    /// <summary>The character was created and saved.</summary>
    public event Action<CharacterSave> Created;

    /// <summary>Back was pressed.</summary>
    public event Action Cancelled;

    private readonly CharacterCatalog catalog;
    private readonly CharacterPreviewStage stage;
    private readonly RectTransform root;
    private readonly CharacterBuild build = new CharacterBuild();
    private CharacterAppearanceData look = new CharacterAppearanceData();
    private Step step;
    private Trait focusedTrait;
    private TraitType? traitFilter;

    // header / footer
    private readonly Button[] stepTabs = new Button[4];
    private TextMeshProUGUI pointsText;
    private TextMeshProUGUI messageText;
    private Button prevButton, nextButton, createButton;

    // pages
    private readonly RectTransform[] pages = new RectTransform[4];
    private RectTransform raceList, classList, traitList, traitFilterRow;
    private TextMeshProUGUI raceDetail, classDetail, traitDetail, traitRulesText;
    private Button traitAction;
    private TextMeshProUGUI traitActionLabel;
    private readonly Dictionary<CharacterArchetype, Button> raceButtons = new Dictionary<CharacterArchetype, Button>();
    private readonly Dictionary<PlayerClass, Button> classButtons = new Dictionary<PlayerClass, Button>();
    private readonly Dictionary<Trait, TraitRow> traitRows = new Dictionary<Trait, TraitRow>();
    private readonly List<GameObject> filterButtons = new List<GameObject>();

    // appearance controls
    private Button maleButton, femaleButton;
    private readonly List<Button> skinSwatches = new List<Button>(), hairSwatches = new List<Button>(), eyeSwatches = new List<Button>();
    private readonly List<Button> topSwatches = new List<Button>(), bottomSwatches = new List<Button>();
    private GameObject clothesCard, bodyModelCard;
    private readonly Dictionary<AppearancePart, TextMeshProUGUI> styleLabels = new Dictionary<AppearancePart, TextMeshProUGUI>();
    private readonly Dictionary<AppearancePart, GameObject> styleRows = new Dictionary<AppearancePart, GameObject>();
    private Slider noseSlider, eyeSlider;
    private RectTransform skinGrid;
    private AppearanceRules rules = AppearanceRules.None;
    private CharacterArchetype rulesRace;

    // preview and summary
    private CharacterPreviewDrag drag;
    private Slider heightSlider;
    private TextMeshProUGUI heightText;
    private GameObject heightRow;
    private TMP_InputField nameInput;
    private TextMeshProUGUI summaryText;
    private bool refreshing;

    private sealed class TraitRow
    {
        public Button button;
        public TextMeshProUGUI name, cost;
    }

    public GameObject GameObject => root.gameObject;

    public CharacterCreatorPanel(Transform parent, CharacterCatalog catalog, CharacterPreviewStage stage)
    {
        this.catalog = catalog != null ? catalog : CharacterCatalog.Instance;
        this.stage = stage;
        root = UITheme.Area("Character Creation", parent, Vector2.zero, Vector2.one);
        BuildHeader();
        BuildFooter();
        BuildLeft();
        BuildCenter();
        BuildRight();
        root.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ open
    /// <summary>Shows the screen with a fresh character (the first race and class picked, a random look).</summary>
    public void Open()
    {
        build.race = null;
        build.playerClass = null;
        build.traits.Clear();
        build.height = 0f;
        List<CharacterArchetype> races = catalog.Races;
        List<PlayerClass> classes = catalog.Classes;
        if (races.Count > 0) build.race = races[0];
        if (classes.Count > 0) build.playerClass = classes.Find(c => build.race == null || c.archetype == null || build.race.IsCompatibleWith(c.archetype)) ?? classes[0];
        build.height = build.race != null && build.race.setsHeight ? build.race.defaultHeight : 0f;
        rulesRace = null;
        UpdateRules(false);
        look = Validate(CharacterAppearanceData.Random(stage != null ? stage.Appearance : null));
        UpdateRules(true);
        focusedTrait = null;
        traitFilter = null;
        if (nameInput != null) nameInput.text = "";
        root.gameObject.SetActive(true);
        if (stage != null) stage.ResetView();
        if (drag != null) drag.SetFace(0f);
        BuildRaceList();
        BuildClassList();
        BuildTraitList();
        GoTo(catalog.HasRaces ? Step.Race : Step.Class);
        RefreshAll();
    }

    public void Close() => root.gameObject.SetActive(false);

    // ------------------------------------------------------------------ layout
    private void BuildHeader()
    {
        RectTransform header = UITheme.Area("Header", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -20f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(header.gameObject, 16f);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        RectTransform titles = UITheme.Column("Titles", header, 0, 0f);
        InventoryUIFactory.Size(titles, 420f, 76f, 0f);
        UITheme.Label(titles, "CREATE CHARACTER", 14f, UITheme.Accent, TextAlignmentOptions.BottomLeft, FontStyles.Bold).characterSpacing = 4f;
        TextMeshProUGUI t = UITheme.Label(titles, "Who will you be?", 34f, null, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        InventoryUIFactory.Size(t, -1f, 44f);

        RectTransform tabs = UITheme.Row("Steps", header, 52f, 8f);
        InventoryUIFactory.Size(tabs, -1f, 52f, 1f);
        string[] names = { "1   Race", "2   Class", "3   Traits", "4   Appearance" };
        for (int i = 0; i < 4; i++)
        {
            Step s = (Step)i;
            stepTabs[i] = UITheme.Button(names[i], tabs, names[i], () => GoTo(s), UITheme.ButtonStyle.Tab, 190f, 50f);
        }

        Image pill = UITheme.Panel("Points", header, UITheme.Card, 22);
        InventoryUIFactory.Size(pill, 300f, 48f, 0f);
        pointsText = UITheme.Label(pill.transform, "", 17f, null, TextAlignmentOptions.Center);
        InventoryUIFactory.Stretch(pointsText.rectTransform, 12f, 0f);
    }

    private void BuildFooter()
    {
        Image bar = UITheme.Panel("Footer", root, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 20f), new Vector2(-40f, 96f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bar.gameObject, 12f);
        h.padding = new RectOffset(14, 14, 12, 12);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        UITheme.Button("Back", bar.transform, "Back", () => Cancelled?.Invoke(), UITheme.ButtonStyle.Ghost, 140f, 52f);
        messageText = UITheme.Label(bar.transform, "", 16f, UITheme.Muted, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(messageText, -1f, 52f, 1f);
        UITheme.Button("Randomize", bar.transform, "Randomize Look", Randomize, UITheme.ButtonStyle.Normal, 200f, 52f);
        prevButton = UITheme.Button("Previous", bar.transform, "Previous", () => GoTo(step - 1), UITheme.ButtonStyle.Normal, 150f, 52f);
        nextButton = UITheme.Button("Next", bar.transform, "Next", () => GoTo(step + 1), UITheme.ButtonStyle.Normal, 150f, 52f);
        createButton = UITheme.Button("Create", bar.transform, "Create Character", Create, UITheme.ButtonStyle.Primary, 250f, 52f);
    }

    private void BuildLeft()
    {
        Image panel = UITheme.Panel("Steps Panel", root, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 116f), new Vector2(620f, -116f));

        // Race
        pages[0] = Page(panel.transform, "Race", "Your people shape your body, your gifts and the traits you can learn.");
        raceList = UITheme.ScrollList("Races", pages[0], 0f, out _);
        raceDetail = DetailText(pages[0], 250f);

        // Class
        pages[1] = Page(panel.transform, "Class", "Your calling: base resources, attributes, starting traits and trait points.");
        classList = UITheme.ScrollList("Classes", pages[1], 0f, out _);
        classDetail = DetailText(pages[1], 250f);

        // Traits
        pages[2] = Page(panel.transform, "Traits", "Spend trait points on strengths. Drawbacks give points back.");
        traitRulesText = UITheme.Label(pages[2], "", 15f, UITheme.Muted);
        InventoryUIFactory.Size(traitRulesText, -1f, 22f);
        traitFilterRow = UITheme.Row("Filters", pages[2], 36f, 6f);
        traitList = UITheme.ScrollList("Traits", pages[2], 0f, out _);
        RectTransform card = UITheme.CardColumn("Trait Detail", pages[2], 14, 8f, UITheme.SurfaceRaised);
        InventoryUIFactory.Size(card, -1f, 270f);
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        RectTransform detailScroll = UITheme.ScrollList("Text", card, 0f, out _, new Color(0f, 0f, 0f, 0f), 2, 2f);
        traitDetail = UITheme.Label(detailScroll, "Select a trait to see what it does.", 15f, UITheme.Muted, TextAlignmentOptions.TopLeft);
        traitAction = UITheme.Button("Action", card, "Add Trait", ToggleFocusedTrait, UITheme.ButtonStyle.Primary, 0f, 46f);
        traitActionLabel = traitAction.GetComponentInChildren<TextMeshProUGUI>(true);

        // Appearance
        pages[3] = Page(panel.transform, "Appearance", "Body, face and colours. The preview updates as you choose.");
        RectTransform lookTabs = UITheme.Row("Look Tabs", pages[3], 40f, 4f);
        BuildAppearance(lookTabs, UITheme.ScrollList("Options", pages[3], 0f, out _, null, 6, 10f));
        RectTransform lookActions = UITheme.Row("Actions", pages[3], 46f, 10f);
        UITheme.Button("Random", lookActions, "Randomize Look", Randomize, UITheme.ButtonStyle.Normal, 0f, 44f);
        UITheme.Button("Reset View", lookActions, "Reset View", () => { stage?.ResetView(); drag?.SetFace(lookFace[(int)lookTab]); }, UITheme.ButtonStyle.Ghost, 0f, 44f);
    }

    private static RectTransform Page(Transform parent, string title, string subtitle)
    {
        RectTransform page = UITheme.Column(title + " Page", parent, 22, 10f);
        InventoryUIFactory.Stretch(page);
        UITheme.Title(page, title, 28f);
        TextMeshProUGUI sub = UITheme.Label(page, subtitle, 15f, UITheme.Muted);
        InventoryUIFactory.Size(sub, -1f, 40f);
        return page;
    }

    private static TextMeshProUGUI DetailText(Transform page, float height)
    {
        RectTransform content = UITheme.ScrollList("Details", page, height, out _, UITheme.SurfaceRaised, 12, 4f);
        return UITheme.Label(content, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
    }

    private void BuildCenter()
    {
        RectTransform center = UITheme.Area("Preview Area", root, Vector2.zero, Vector2.one, new Vector2(648f, 116f), new Vector2(-548f, -116f));
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(center.gameObject, 0, 10f, false);
        v.childForceExpandHeight = false;

        Image frame = UITheme.Panel("Preview", center, new Color(0.06f, 0.07f, 0.095f, 1f), UITheme.RadiusLarge);
        InventoryUIFactory.Size(frame, -1f, 200f).flexibleHeight = 1f;
        var mask = frame.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform fit = UITheme.Area("Fit", frame.transform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        RawImage picture = InventoryUIFactory.Rect("Picture", fit).gameObject.AddComponent<RawImage>();
        picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var aspect = picture.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (stage != null && stage.Texture != null)
        {
            picture.texture = stage.Texture;
            aspect.aspectRatio = stage.Texture.width / (float)stage.Texture.height;
            drag = picture.gameObject.AddComponent<CharacterPreviewDrag>();
            drag.stage = stage;
        }
        else
        {
            picture.color = new Color(0f, 0f, 0f, 0f);
            TextMeshProUGUI none = UITheme.Label(frame.transform, "No player model to preview.\nSet the Preview Model of the Character Catalog, or the Net Player Spawner's player prefab.",
                16f, UITheme.Muted, TextAlignmentOptions.Center);
            InventoryUIFactory.Stretch(none.rectTransform, 30f, 30f);
        }
        if (stage != null && !stage.HasModel)
        {
            TextMeshProUGUI none = UITheme.Label(frame.transform, "No player model found to preview.", 16f, UITheme.Muted, TextAlignmentOptions.Center);
            InventoryUIFactory.Stretch(none.rectTransform, 30f, 30f);
        }
        TextMeshProUGUI hint = UITheme.Label(frame.transform, "Drag to turn  ·  scroll to zoom", 14f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Bottom);
        UITheme.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0f, 40f));

        RectTransform tools = UITheme.Row("View", center, 46f, 8f);
        UITheme.Spacer(tools);
        UITheme.Button("Body", tools, "Full Body", () => drag?.SetFace(0f), UITheme.ButtonStyle.Normal, 150f, 44f);
        UITheme.Button("Face", tools, "Face", () => drag?.SetFace(1f), UITheme.ButtonStyle.Normal, 120f, 44f);
        UITheme.Button("Turn", tools, "Turn", () => stage?.Rotate(90f), UITheme.ButtonStyle.Normal, 120f, 44f);
        UITheme.Spacer(tools);

        heightSlider = UITheme.Slider(center, "Height", 1.5f, 2f, 1.78f, false, out heightText);
        heightRow = heightSlider.transform.parent.gameObject;
        heightSlider.onValueChanged.AddListener(v =>
        {
            if (refreshing) return;
            build.height = v;
            RefreshPreview();
            RefreshSummary();
        });
    }

    private void BuildRight()
    {
        Image panel = UITheme.Panel("Summary Panel", root, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-520f, 116f), new Vector2(-40f, -116f));
        RectTransform col = UITheme.Column("Content", panel.transform, 22, 10f);
        InventoryUIFactory.Stretch(col);
        UITheme.Header(col, "Name");
        nameInput = UITheme.Input("Name", col, "Name your character…", catalog.maxNameLength);
        nameInput.onValueChanged.AddListener(_ => { RefreshSummary(); RefreshButtons(); });
        UITheme.Header(col, "Summary");
        RectTransform content = UITheme.ScrollList("Summary", col, 0f, out _, null, 12, 4f);
        summaryText = UITheme.Label(content, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
        summaryText.richText = true;
    }

    // ------------------------------------------------------------------ race and class lists
    private void BuildRaceList()
    {
        Clear(raceList);
        raceButtons.Clear();
        List<CharacterArchetype> races = catalog.Races;
        if (races.Count == 0)
        {
            UITheme.Label(raceList, "This game has no races: go on to the class.", 16f, UITheme.Muted);
            return;
        }
        foreach (CharacterArchetype r in races)
        {
            CharacterArchetype race = r;
            raceButtons[r] = ChoiceCard(raceList, r.Name, FirstLine(r.description), r.icon, r.color, () => PickRace(race));
        }
    }

    private void BuildClassList()
    {
        Clear(classList);
        classButtons.Clear();
        List<PlayerClass> classes = catalog.Classes;
        if (classes.Count == 0)
        {
            UITheme.Label(classList, "No class found. Create a Player Class asset and add it to the Character Catalog.", 16f, UITheme.Bad);
            return;
        }
        foreach (PlayerClass c in classes)
        {
            PlayerClass cls = c;
            classButtons[c] = ChoiceCard(classList, c.GetClassName(), FirstLine(c.classDescription), c.classIcon, c.classColor, () => PickClass(cls));
        }
    }

    /// <summary>A list card: icon (or a coloured initial), name and one line of description.</summary>
    private static Button ChoiceCard(Transform parent, string title, string subtitle, Sprite icon, Color tint, UnityAction onClick)
    {
        Image bg = InventoryUIFactory.Panel(title, parent, Color.white);
        var b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        UITheme.Colors(b, UITheme.ButtonStyle.ListItem);
        InventoryUIFactory.Size(bg, -1f, 84f);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 14f);
        h.padding = new RectOffset(18, 14, 10, 10);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        if (icon != null)
        {
            Image ic = InventoryUIFactory.Rect("Icon", bg.transform).gameObject.AddComponent<Image>();
            ic.sprite = icon;
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            InventoryUIFactory.Size(ic, 56f, 56f, 0f);
        }
        else
        {
            Color chip = tint.a > 0.01f && tint != Color.white ? tint : UITheme.AccentDark;
            chip.a = 1f;
            Image ic = UITheme.Panel("Initial", bg.transform, chip * 0.75f + new Color(0f, 0f, 0f, 0.25f), 12);
            ic.raycastTarget = false;
            InventoryUIFactory.Size(ic, 56f, 56f, 0f);
            TextMeshProUGUI letter = UITheme.Label(ic.transform, string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1).ToUpperInvariant(), 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Stretch(letter.rectTransform);
        }
        RectTransform col = UITheme.Column("Text", bg.transform, 0, 2f);
        InventoryUIFactory.Size(col, -1f, 62f, 1f);
        col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        TextMeshProUGUI name = UITheme.Label(col, title, 20f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(name, -1f, 26f);
        TextMeshProUGUI sub = UITheme.Label(col, subtitle, 14f, UITheme.Muted, TextAlignmentOptions.TopLeft);
        sub.overflowMode = TextOverflowModes.Ellipsis;
        sub.maxVisibleLines = 2;
        InventoryUIFactory.Size(sub, -1f, 34f);
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    private void PickRace(CharacterArchetype race)
    {
        if (race != null && build.playerClass != null && build.playerClass.archetype != null && !race.IsCompatibleWith(build.playerClass.archetype))
        {
            // Keep the race the player wants and pick a class that fits it.
            PlayerClass fits = catalog.Classes.Find(c => c.archetype == null || race.IsCompatibleWith(c.archetype));
            Say($"{race.Name} cannot be a {build.playerClass.GetClassName()}" + (fits != null ? $": switched to {fits.GetClassName()}." : "."), true);
            if (fits != null) build.playerClass = fits;
        }
        build.race = race;
        build.height = race != null && race.setsHeight ? race.ClampHeight(build.height > 0f ? build.height : race.defaultHeight) : 0f;
        AfterIdentityChanged();
    }

    private void PickClass(PlayerClass cls)
    {
        if (cls != null && build.race != null && cls.archetype != null && !build.race.IsCompatibleWith(cls.archetype))
        {
            Say($"{build.race.Name} cannot be a {cls.GetClassName()}.", true);
            return;
        }
        build.playerClass = cls;
        AfterIdentityChanged();
    }

    private void AfterIdentityChanged()
    {
        UpdateRules(build.race != rulesRace);
        List<Trait> dropped = build.DropInvalidTraits(catalog);
        if (dropped.Count > 0)
            Say("Removed traits that no longer fit: " + string.Join(", ", dropped.ConvertAll(t => t.Name)) + ".", true);
        if (focusedTrait != null && !build.Selectable(catalog).Contains(focusedTrait))
            focusedTrait = null;
        BuildTraitList();
        RefreshAll();
    }

    // ------------------------------------------------------------------ traits
    private void BuildTraitList()
    {
        Clear(traitList);
        traitRows.Clear();
        foreach (GameObject go in filterButtons)
            if (go != null) UnityEngine.Object.Destroy(go);
        filterButtons.Clear();

        List<Trait> selectable = build.Selectable(catalog);
        // Filters: All + every type present.
        var types = new List<TraitType>();
        foreach (Trait t in selectable)
            if (t != null && !types.Contains(t.type)) types.Add(t.type);
        types.Sort();
        if (traitFilter.HasValue && !types.Contains(traitFilter.Value))
            traitFilter = null;
        AddFilter("All", null);
        foreach (TraitType type in types)
            AddFilter(type.ToString(), type);

        if (build.playerClass == null)
        {
            UITheme.Label(traitList, "Choose a class first.", 16f, UITheme.Muted);
            return;
        }
        if (selectable.Count == 0)
        {
            UITheme.Label(traitList, "No traits to choose for this race and class.", 16f, UITheme.Muted);
            return;
        }
        // Positive traits first (by cost), then drawbacks.
        selectable.Sort((a, b) =>
        {
            int ca = build.Cost(a), cb = build.Cost(b);
            if ((ca < 0) != (cb < 0)) return ca < 0 ? 1 : -1;
            return ca != cb ? ca.CompareTo(cb) : string.CompareOrdinal(a.Name, b.Name);
        });
        foreach (Trait t in selectable)
        {
            if (t == null || (traitFilter.HasValue && t.type != traitFilter.Value)) continue;
            Trait trait = t;
            traitRows[t] = TraitRowFor(trait);
        }
    }

    private void AddFilter(string label, TraitType? type)
    {
        Button b = UITheme.Button(label, traitFilterRow, label, () =>
        {
            traitFilter = type;
            BuildTraitList();
            RefreshTraits();
        }, UITheme.ButtonStyle.Tab, 0f, 34f);
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            t.fontSize = 13f;
            t.alignment = TextAlignmentOptions.Center;
            InventoryUIFactory.Stretch(t.rectTransform, 4f, 0f);
        }
        InventoryUIFactory.Size(b, Mathf.Max(52f, label.Length * 9f + 20f), 34f, 0f);
        UITheme.SetSelected(b, traitFilter == type);
        filterButtons.Add(b.gameObject);
    }

    private TraitRow TraitRowFor(Trait trait)
    {
        Image bg = InventoryUIFactory.Panel(trait.Name, traitList, Color.white);
        var b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        UITheme.Colors(b, UITheme.ButtonStyle.ListItem);
        InventoryUIFactory.Size(bg, -1f, 52f);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 10f);
        h.padding = new RectOffset(18, 16, 4, 4);
        var row = new TraitRow { button = b };
        row.name = UITheme.Label(bg.transform, trait.Name, 17f, null, TextAlignmentOptions.Left);
        row.name.textWrappingMode = TextWrappingModes.NoWrap;
        row.name.overflowMode = TextOverflowModes.Ellipsis;
        InventoryUIFactory.Size(row.name, -1f, 44f, 1f);
        row.cost = UITheme.Label(bg.transform, "", 16f, UITheme.Muted, TextAlignmentOptions.Right, FontStyles.Bold);
        InventoryUIFactory.Size(row.cost, 110f, 44f, 0f);
        b.onClick.AddListener(() =>
        {
            focusedTrait = trait;
            RefreshTraits();
        });
        return row;
    }

    private void ToggleFocusedTrait()
    {
        Trait t = focusedTrait;
        if (t == null) return;
        if (build.traits.Contains(t))
        {
            string why = build.WhyCannotRemove(t);
            if (why != null) { Say(why, true); return; }
            build.traits.Remove(t);
        }
        else
        {
            string why = build.WhyCannotAdd(t, catalog);
            if (why != null) { Say(why, true); return; }
            build.traits.Add(t);
        }
        Say("", false);
        RefreshAll();
    }

    // ------------------------------------------------------------------ appearance
    private enum LookTab { Body, Head, Hair, FacialHair, Eyes, Nose }
    private static readonly string[] LookTabNames = { "Body", "Head", "Hair", "Facial Hair", "Eyes", "Nose" };
    // How close the preview goes for each tab (0 = whole body, 1 = the face).
    private static readonly float[] lookFace = { 0f, 1f, 0.85f, 1f, 1f, 1f };
    private LookTab lookTab;
    private readonly Button[] lookTabButtons = new Button[6];
    private readonly RectTransform[] lookSections = new RectTransform[6];
    private readonly Dictionary<AppearancePart, RectTransform> chipGrids = new Dictionary<AppearancePart, RectTransform>();
    private TextMeshProUGUI beardNote;

    /// <summary>The Appearance step: a tab per part of the body, each with its choices as named cards and its colours.</summary>
    private void BuildAppearance(Transform tabs, Transform content)
    {
        for (int i = 0; i < LookTabNames.Length; i++)
        {
            LookTab t = (LookTab)i;
            Button b = UITheme.Button(LookTabNames[i], tabs, LookTabNames[i], () => ShowLookTab(t), UITheme.ButtonStyle.Tab, 0f, 38f);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = 14f;
                label.alignment = TextAlignmentOptions.Center;
                InventoryUIFactory.Stretch(label.rectTransform, 2f, 0f);
            }
            lookTabButtons[i] = b;
            lookSections[i] = UITheme.Column(LookTabNames[i], content, 0, 10f, false);
        }

        // Body
        RectTransform body = UITheme.CardColumn("Body", lookSections[(int)LookTab.Body], 14, 8f);
        UITheme.Label(body, "Body type", 16f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform bodyRow = UITheme.Row("Types", body, 46f, 10f);
        maleButton = UITheme.Button("Male", bodyRow, "Male", () => SetBody(CharacterBodyType.Male), UITheme.ButtonStyle.Normal, 0f, 44f);
        femaleButton = UITheme.Button("Female", bodyRow, "Female", () => SetBody(CharacterBodyType.Female), UITheme.ButtonStyle.Normal, 0f, 44f);
        // Body models of your own (Character Appearance > Body Models), shown when there are some.
        bodyModelCard = ChipCard(lookSections[(int)LookTab.Body], AppearancePart.Body, "Body model").gameObject;
        RectTransform skin = UITheme.CardColumn("Skin", lookSections[(int)LookTab.Body], 14, 8f);
        skinGrid = SwatchGrid(skin, "Skin tone", CharacterAppearanceData.SkinTones, skinSwatches, c => { look.skinColor = c; AfterLook(); });
        // Clothes of the placeholder body (shown only when the model uses it).
        RectTransform clothes = UITheme.CardColumn("Clothes", lookSections[(int)LookTab.Body], 14, 8f);
        UITheme.Label(clothes, "Clothes", 16f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        SwatchGrid(clothes, "Shirt", CharacterAppearanceData.ClothColors, topSwatches, c => { look.topColor = c; AfterLook(); });
        SwatchGrid(clothes, "Trousers", CharacterAppearanceData.ClothColors, bottomSwatches, c => { look.bottomColor = c; AfterLook(); });
        clothesCard = clothes.gameObject;

        // Head
        ChipCard(lookSections[(int)LookTab.Head], AppearancePart.Head, "Head shape");

        // Hair
        ChipCard(lookSections[(int)LookTab.Hair], AppearancePart.Hair, "Hairstyle");
        RectTransform hairColour = UITheme.CardColumn("Hair Colour", lookSections[(int)LookTab.Hair], 14, 8f);
        SwatchGrid(hairColour, "Hair colour (hair, beard and eyebrows)", CharacterAppearanceData.HairColors, hairSwatches, c => { look.hairColor = c; AfterLook(); });
        ChipCard(lookSections[(int)LookTab.Hair], AppearancePart.Eyebrows, "Eyebrows");

        // Facial hair
        RectTransform beard = ChipCard(lookSections[(int)LookTab.FacialHair], AppearancePart.Beard, "Beard");
        beardNote = UITheme.Label(beard, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(beardNote, -1f, 20f);
        styleRows[AppearancePart.Beard] = beard.gameObject;

        // Eyes
        RectTransform eyes = ChipCard(lookSections[(int)LookTab.Eyes], AppearancePart.Eyes, "Eye shape");
        eyeSlider = SliderRow(eyes, "Eye size", v => { look.eyeSize = v; AfterLook(); });
        RectTransform eyeColour = UITheme.CardColumn("Eye Colour", lookSections[(int)LookTab.Eyes], 14, 8f);
        SwatchGrid(eyeColour, "Eye colour", CharacterAppearanceData.EyeColors, eyeSwatches, c => { look.eyeColor = c; AfterLook(); });

        // Nose
        RectTransform nose = ChipCard(lookSections[(int)LookTab.Nose], AppearancePart.Nose, "Nose shape");
        noseSlider = SliderRow(nose, "Nose size", v => { look.noseSize = v; AfterLook(); });

        ShowLookTab(LookTab.Body);
    }

    /// <summary>A card with a title and a grid of the part's choices (filled by <see cref="RebuildChips"/>).</summary>
    private RectTransform ChipCard(Transform parent, AppearancePart part, string title)
    {
        RectTransform card = UITheme.CardColumn(title, parent, 14, 8f);
        UITheme.Label(card, title, 16f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform grid = InventoryUIFactory.Rect("Choices", card);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(160f, 46f);
        g.spacing = new Vector2(8f, 8f);
        g.constraint = GridLayoutGroup.Constraint.Flexible;
        chipGrids[part] = grid;
        return card;
    }

    private void ShowLookTab(LookTab t)
    {
        lookTab = t;
        for (int i = 0; i < lookSections.Length; i++)
        {
            if (lookSections[i] != null) lookSections[i].gameObject.SetActive(i == (int)t);
            if (lookTabButtons[i] != null) UITheme.SetSelected(lookTabButtons[i], i == (int)t);
        }
        if (drag != null && step == Step.Appearance) drag.SetFace(lookFace[(int)t]);
    }

    /// <summary>The choices of a part as cards: the chosen one highlighted, the ones this body / race cannot take greyed out.</summary>
    private void RebuildChips(AppearancePart part)
    {
        if (!chipGrids.TryGetValue(part, out RectTransform grid) || grid == null)
            return;
        for (int i = grid.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(grid.GetChild(i).gameObject);
        CharacterAppearance a = stage != null ? stage.Appearance : null;
        int count = a != null ? a.OptionCount(part) : CharacterAppearance.PlaceholderNames(part).Length;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            string name = a != null ? a.OptionName(part, i) : CharacterAppearance.PlaceholderNames(part)[i];
            bool allowed = a != null ? a.IsAllowed(part, i, look.bodyType)
                : part != AppearancePart.Beard || (i == 0 ? !rules.alwaysBeard : look.bodyType == CharacterBodyType.Male || rules.beardOnAnyBody);
            Button b = UITheme.Button(name, grid, name, () =>
            {
                look.SetStyle(part, index);
                AfterLook();
            }, UITheme.ButtonStyle.ListItem, 0f, 46f);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = 15f;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                InventoryUIFactory.Stretch(label.rectTransform, 8f, 0f);
                label.alpha = allowed ? 1f : 0.35f;
            }
            b.interactable = allowed;
            UITheme.SetSelected(b, look.Style(part) == i);
        }
    }

    private static RectTransform SwatchGrid(Transform parent, string label, Color[] colors, List<Button> into, Action<Color> pick)
    {
        TextMeshProUGUI l = UITheme.Label(parent, label, 14f, UITheme.Muted);
        InventoryUIFactory.Size(l, -1f, 20f);
        RectTransform grid = InventoryUIFactory.Rect("Swatches", parent);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(38f, 38f);
        g.spacing = new Vector2(8f, 8f);
        g.constraint = GridLayoutGroup.Constraint.Flexible;
        FillSwatches(grid, colors, into, pick);
        return grid;
    }

    private static void FillSwatches(RectTransform grid, Color[] colors, List<Button> into, Action<Color> pick)
    {
        foreach (Button b in into)
            if (b != null) UnityEngine.Object.Destroy(b.gameObject);
        into.Clear();
        foreach (Color c in colors)
        {
            Color col = c;
            into.Add(UITheme.Swatch(grid, col, () => pick(col), 38f));
        }
    }

    /// <summary>
    /// The race's and class's appearance rules: build, ears, beards, skin tones. A new race also brings its defaults
    /// (a dwarf gets its beard, an orc a skin of its palette).
    /// </summary>
    private void UpdateRules(bool raceChanged)
    {
        Color[] oldPalette = rules.SkinPalette;
        rules = AppearanceRules.From(build.Archetypes);
        rulesRace = build.race;
        if (stage != null) stage.ShowRules(rules);
        if (raceChanged)
        {
            if (rules.defaultBeard >= 0) look.beardStyle = rules.defaultBeard;
            if (rules.defaultHair >= 0) look.hairStyle = rules.defaultHair;
            Color[] palette = rules.SkinPalette;
            if (Array.FindIndex(palette, c => Close(c, look.skinColor)) < 0)
                look.skinColor = palette[Mathf.Min(palette.Length - 1, palette.Length / 2)];
            look = Validate(look);
        }
        if (skinGrid != null && (raceChanged || oldPalette != rules.SkinPalette))
            FillSwatches(skinGrid, rules.SkinPalette, skinSwatches, c => { look.skinColor = c; AfterLook(); });
    }

    private static Slider SliderRow(Transform parent, string label, Action<float> changed)
    {
        RectTransform row = UITheme.Row(label, parent, 40f, 10f);
        TextMeshProUGUI l = UITheme.Label(row, label, 15f, UITheme.Muted);
        InventoryUIFactory.Size(l, 110f, 40f, 0f);
        Slider s = UITheme.BareSlider("Slider", row, 0f, 1f, 0.5f, false);
        s.onValueChanged.AddListener(v => changed(v));
        return s;
    }

    private void SetBody(CharacterBodyType body)
    {
        look.bodyType = body;
        look = Validate(look);
        AfterLook();
    }

    private void StepStyle(AppearancePart part, int dir)
    {
        CharacterAppearance a = stage != null ? stage.Appearance : null;
        int count = a != null ? a.OptionCount(part) : CharacterAppearance.PlaceholderNames(part).Length;
        if (count <= 0) return;
        int i = look.Style(part);
        if (a != null) i = a.Step(part, i, dir, look.bodyType);
        else
        {
            for (int n = 0; n < count; n++)
            {
                i = ((i + dir) % count + count) % count;
                if (part != AppearancePart.Beard) break;
                if (i == 0 ? !rules.alwaysBeard : look.bodyType == CharacterBodyType.Male || rules.beardOnAnyBody) break;
            }
        }
        look.SetStyle(part, i);
        AfterLook();
    }

    private void Randomize()
    {
        CharacterBodyType keep = look.bodyType;
        int keepModel = look.bodyStyle;
        look = CharacterAppearanceData.Random(stage != null ? stage.Appearance : null);
        look.bodyType = keep;
        look.bodyStyle = keepModel;
        Color[] palette = rules.SkinPalette;
        look.skinColor = palette[UnityEngine.Random.Range(0, palette.Length)];
        look = Validate(look);
        AfterLook();
    }

    private CharacterAppearanceData Validate(CharacterAppearanceData d)
    {
        CharacterAppearance a = stage != null ? stage.Appearance : null;
        if (a != null) return a.Validated(d);
        if (d.bodyType == CharacterBodyType.Female && !rules.beardOnAnyBody) d.beardStyle = 0;
        if (rules.alwaysBeard && d.beardStyle == 0) d.beardStyle = rules.defaultBeard > 0 ? rules.defaultBeard : 3;
        return d;
    }

    private void AfterLook()
    {
        if (refreshing) return;
        RefreshPreview();
        RefreshAppearance();
        RefreshSummary();
    }

    // ------------------------------------------------------------------ steps
    private void GoTo(Step s)
    {
        if ((int)s < 0) s = Step.Race;
        if ((int)s > 3) s = Step.Appearance;
        if (s == Step.Race && !catalog.HasRaces) s = Step.Class;
        step = s;
        for (int i = 0; i < 4; i++)
        {
            pages[i].gameObject.SetActive(i == (int)s);
            UITheme.SetSelected(stepTabs[i], i == (int)s);
        }
        if (drag != null) drag.SetFace(s == Step.Appearance ? lookFace[(int)lookTab] : 0f);
        RefreshButtons();
    }

    // ------------------------------------------------------------------ refresh
    private void RefreshAll()
    {
        RefreshLists();
        RefreshTraits();
        RefreshAppearance();
        RefreshHeight();
        RefreshPreview();
        RefreshSummary();
        RefreshButtons();
    }

    private void RefreshLists()
    {
        foreach (KeyValuePair<CharacterArchetype, Button> kv in raceButtons)
        {
            UITheme.SetSelected(kv.Value, kv.Key == build.race);
            bool fits = build.playerClass == null || build.playerClass.archetype == null || kv.Key.IsCompatibleWith(build.playerClass.archetype);
            SetDim(kv.Value, !fits);
        }
        foreach (KeyValuePair<PlayerClass, Button> kv in classButtons)
        {
            UITheme.SetSelected(kv.Value, kv.Key == build.playerClass);
            bool fits = build.race == null || kv.Key.archetype == null || build.race.IsCompatibleWith(kv.Key.archetype);
            kv.Value.interactable = fits;
            SetDim(kv.Value, !fits);
        }
        if (stepTabs[0] != null) stepTabs[0].gameObject.SetActive(catalog.HasRaces);
        raceDetail.text = build.race != null ? ArchetypeText(build.race, null) : StatText.Paint("Pick a race.", 0);
        classDetail.text = build.playerClass != null ? ClassText(build.playerClass) : StatText.Paint("Pick a class.", 0);
    }

    private static void SetDim(Button b, bool dim)
    {
        foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
            t.alpha = dim ? 0.4f : 1f;
    }

    private void RefreshTraits()
    {
        foreach (KeyValuePair<Trait, TraitRow> kv in traitRows)
        {
            Trait t = kv.Key;
            TraitRow row = kv.Value;
            bool chosen = build.traits.Contains(t);
            string why = chosen ? null : build.WhyCannotAdd(t, catalog);
            int cost = build.Cost(t);
            string tag = t.HasActiveSkill ? "  <color=#7FB2FF><size=13>ACTIVE</size></color>" : "";
            row.name.text = t.Name + tag;
            row.name.alpha = chosen || why == null ? 1f : 0.45f;
            row.cost.text = chosen ? StatText.Paint("Chosen", 1) : cost < 0 ? StatText.Paint($"+{-cost} pts", 1) : $"{cost} pts";
            UITheme.SetSelected(row.button, chosen || t == focusedTrait);
        }
        CharacterBuild b = build;
        traitRulesText.text = b.playerClass != null
            ? $"{b.RemainingPoints} of {b.StartingPoints} points left  ·  up to {catalog.maxActiveTraits} active trait{(catalog.maxActiveTraits == 1 ? "" : "s")}"
            : "Choose a class to see its trait points.";

        Trait f = focusedTrait;
        if (f == null)
        {
            traitDetail.text = StatText.Paint("Select a trait to see what it does.", 0);
            traitAction.gameObject.SetActive(false);
            return;
        }
        bool has = build.traits.Contains(f);
        string problem = has ? build.WhyCannotRemove(f) : build.WhyCannotAdd(f, catalog);
        int c = build.Cost(f);
        var sb = new StringBuilder();
        sb.Append("<size=20><b>").Append(f.Name).Append("</b></size>");
        sb.Append("   <color=").Append(StatText.NeutralColor).Append('>').Append(f.type).Append(" · ").Append(f.rarity).Append("</color>\n");
        sb.Append(c < 0 ? StatText.Paint($"Drawback: gives {-c} points", 1) : $"Costs {c} point{(c == 1 ? "" : "s")}");
        if (c != f.cost) sb.Append(StatText.Paint($"  (normally {f.cost})", 0));
        sb.Append("\n\n").Append(StatText.RichDescription(f));
        if (problem != null) sb.Append("\n\n").Append(StatText.Paint(problem, -1));
        traitDetail.text = sb.ToString();
        traitAction.gameObject.SetActive(true);
        traitAction.interactable = problem == null;
        if (traitActionLabel != null) traitActionLabel.text = has ? "Remove Trait" : "Add Trait";
        UITheme.Colors(traitAction, has ? UITheme.ButtonStyle.Danger : UITheme.ButtonStyle.Primary);
    }

    private void RefreshAppearance()
    {
        refreshing = true;
        UITheme.SetSelected(maleButton, look.bodyType == CharacterBodyType.Male);
        UITheme.SetSelected(femaleButton, look.bodyType == CharacterBodyType.Female);
        Mark(skinSwatches, rules.SkinPalette, look.skinColor);
        Mark(hairSwatches, CharacterAppearanceData.HairColors, look.hairColor);
        Mark(eyeSwatches, CharacterAppearanceData.EyeColors, look.eyeColor);
        Mark(topSwatches, CharacterAppearanceData.ClothColors, look.topColor);
        Mark(bottomSwatches, CharacterAppearanceData.ClothColors, look.bottomColor);
        CharacterAppearance a = stage != null ? stage.Appearance : null;
        if (clothesCard != null) clothesCard.SetActive(a == null || a.Body != null);
        if (bodyModelCard != null) bodyModelCard.SetActive(a != null && a.OptionCount(AppearancePart.Body) > 1);
        foreach (AppearancePart part in chipGrids.Keys)
            RebuildChips(part);
        if (beardNote != null)
        {
            bool canBeard = false;
            int count = a != null ? a.OptionCount(AppearancePart.Beard) : CharacterAppearance.PlaceholderNames(AppearancePart.Beard).Length;
            for (int i = 1; i < count && !canBeard; i++)
                canBeard = a != null ? a.IsAllowed(AppearancePart.Beard, i, look.bodyType) : look.bodyType == CharacterBodyType.Male || rules.beardOnAnyBody;
            beardNote.text = !canBeard ? StatText.Paint("No beards for this body type and race.", 0)
                : rules.alwaysBeard ? StatText.Paint("Your race always wears a beard.", 0) : "";
        }
        if (noseSlider != null) noseSlider.SetValueWithoutNotify(look.noseSize);
        if (eyeSlider != null) eyeSlider.SetValueWithoutNotify(look.eyeSize);
        refreshing = false;
    }

    private static void Mark(List<Button> swatches, Color[] colors, Color current)
    {
        for (int i = 0; i < swatches.Count && i < colors.Length; i++)
            UITheme.SetSwatchSelected(swatches[i], Close(colors[i], current));
    }

    private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;

    private void RefreshHeight()
    {
        refreshing = true;
        CharacterArchetype r = build.race;
        bool show = r != null && r.setsHeight;
        heightRow.SetActive(show);
        if (show)
        {
            heightSlider.minValue = Mathf.Min(r.heightRange.x, r.heightRange.y);
            heightSlider.maxValue = Mathf.Max(r.heightRange.x, r.heightRange.y);
            heightSlider.SetValueWithoutNotify(build.ClampedHeight);
        }
        refreshing = false;
        if (show) heightText.text = $"{build.ClampedHeight:0.00} m";
    }

    private void RefreshPreview()
    {
        if (stage == null) return;
        stage.ShowAppearance(look);
        stage.ShowHeight(build.ClampedHeight);
        if (heightText != null && build.race != null && build.race.setsHeight)
            heightText.text = $"{build.ClampedHeight:0.00} m";
    }

    private void RefreshButtons()
    {
        string problem = build.Problem(catalog, CleanedName());
        if (problem == null && catalog.uniqueNames && CharacterSaveStore.NameTaken(CleanedName()))
            problem = "You already have a character with that name.";
        createButton.interactable = problem == null;
        prevButton.interactable = step > (catalog.HasRaces ? Step.Race : Step.Class);
        nextButton.gameObject.SetActive(step < Step.Appearance);
        int start = build.StartingPoints;
        pointsText.text = build.playerClass != null
            ? $"Trait points  <b><color={(build.RemainingPoints > 0 ? StatText.GoodColor : StatText.NeutralColor)}>{build.RemainingPoints}</color></b> / {start}"
            : "Trait points  -";
        if (!sticky)
            messageText.text = problem != null ? StatText.Paint(problem, 0) : StatText.Paint("Ready. Create your character when you are happy with it.", 1);
        sticky = false;
    }

    private bool sticky;

    /// <summary>A message on the footer (an error stays until the next change).</summary>
    private void Say(string text, bool error)
    {
        if (string.IsNullOrEmpty(text)) return;
        messageText.text = StatText.Paint(text, error ? -1 : 0);
        sticky = true;
    }

    private string CleanedName() => CharacterBuild.CleanName(nameInput != null ? nameInput.text : "", catalog.maxNameLength);

    // ------------------------------------------------------------------ summary
    private void RefreshSummary()
    {
        var sb = new StringBuilder();
        string name = CleanedName();
        sb.Append("<size=26><b>").Append(string.IsNullOrEmpty(name) ? "Unnamed" : name).Append("</b></size>\n");
        var who = new List<string>();
        if (build.race != null) who.Add(build.race.Name);
        if (build.playerClass != null) who.Add(build.playerClass.GetClassName());
        who.Add(look.bodyType.ToString());
        if (build.ClampedHeight > 0f) who.Add($"{build.ClampedHeight:0.00} m");
        sb.Append(StatText.Paint(string.Join("  ·  ", who), 0)).Append('\n');

        PlayerClass c = build.playerClass;
        if (c != null)
        {
            Section(sb, "Resources");
            sb.Append($"Health <b>{c.health:0}</b>    Stamina <b>{c.stamina:0}</b>    Mana <b>{c.mana:0}</b>\n");
            sb.Append($"Speed <b>{c.speed:0.#}</b> m/s    Carry <b>{c.weight:0}</b>\n");

            Section(sb, "Attributes");
            var totals = new Dictionary<CombatStatType, float>();
            var order = new List<CombatStatType>();
            void Add(IList<CombatStatModifier> mods)
            {
                if (mods == null) return;
                foreach (CombatStatModifier m in mods)
                {
                    if (m == null || m.IsFiltered || m.onlyWithWeapon != WeaponCategory.None) continue;
                    if (!totals.ContainsKey(m.stat)) { totals[m.stat] = 0f; order.Add(m.stat); }
                    totals[m.stat] += m.value;
                }
            }
            Add(c.GetCombatStatModifiers());
            foreach (CharacterArchetype a in build.Archetypes) if (a != null) Add(a.combatStats);
            foreach (Trait t in build.AllTraits()) if (t != null) Add(t.combatStats);
            int col = 0;
            foreach (CombatStatType s in order)
            {
                float v = totals[s];
                if (Mathf.Approximately(v, 0f)) continue;
                CombatStatInfo info = CombatStatInfo.Get(s);
                sb.Append(info.name).Append(" <b>").Append($"{v:0.##}{(info.isPercent ? "%" : "")}").Append("</b>");
                sb.Append(++col % 2 == 0 ? "\n" : "    ");
            }
            if (col % 2 != 0) sb.Append('\n');
        }

        foreach (CharacterArchetype a in build.Archetypes)
        {
            if (a == null) continue;
            List<StatText.Line> lines = StatText.Of(a);
            if (lines.Count == 0) continue;
            Section(sb, a.kind == ArchetypeKind.Race ? "Race: " + a.Name : a.Name);
            string lookText = a.appearance != null ? a.appearance.Describe() : "";
            if (!string.IsNullOrEmpty(lookText)) sb.Append("• ").Append(StatText.Paint("Look: " + lookText, 0)).Append('\n');
            foreach (StatText.Line l in lines) sb.Append("• ").Append(l.Rich).Append('\n');
        }

        Section(sb, "Traits");
        List<Trait> granted = build.GrantedTraits();
        if (build.traits.Count == 0 && granted.Count == 0)
            sb.Append(StatText.Paint("None yet.", 0)).Append('\n');
        foreach (Trait t in build.traits) TraitLine(sb, t, build.Cost(t) < 0 ? "Drawback" : "Chosen");
        foreach (Trait t in granted) if (!build.traits.Contains(t)) TraitLine(sb, t, "Granted");

        Section(sb, "Abilities");
        int abilities = 0;
        foreach (CharacterArchetype a in build.Archetypes)
        {
            if (a == null) continue;
            foreach (EquipmentEffect e in a.effects)
            {
                if (e == null) continue;
                abilities++;
                sb.Append("<b>").Append(e.MenuName).Append("</b> ").Append(StatText.Paint("(" + a.Name + ")", 0)).Append('\n');
                sb.Append(StatText.Paint(e.Describe(), 1)).Append('\n');
            }
        }
        foreach (Trait t in build.AllTraits())
        {
            if (t == null) continue;
            foreach (TraitBehaviour b in t.behaviours)
            {
                if (b == null) continue;
                abilities++;
                sb.Append("<b>").Append(b.MenuName).Append("</b> ").Append(StatText.Paint("(" + t.Name + (b.IsActive ? ", active" : "") + ")", 0)).Append('\n');
                sb.Append(StatText.Paint(b.Describe(), b.IsActive ? 0 : 1)).Append('\n');
            }
        }
        if (c != null && c.progression != null && c.progression.passiveAbilities != null)
        {
            foreach (PassiveAbility p in c.progression.passiveAbilities)
            {
                if (p == null || string.IsNullOrEmpty(p.abilityName)) continue;
                abilities++;
                sb.Append("<b>").Append(p.abilityName).Append("</b> ").Append(StatText.Paint("(class passive)", 0)).Append('\n');
                if (!string.IsNullOrEmpty(p.description)) sb.Append(StatText.Paint(p.description, 1)).Append('\n');
            }
        }
        if (c != null && c.startingSkills != null)
            foreach (string skill in c.startingSkills)
                if (!string.IsNullOrEmpty(skill)) { abilities++; sb.Append("<b>").Append(skill).Append("</b> ").Append(StatText.Paint("(starting skill)", 0)).Append('\n'); }
        if (abilities == 0)
            sb.Append(StatText.Paint("No abilities from this race, class and traits.", 0)).Append('\n');

        summaryText.text = sb.ToString().TrimEnd();
    }

    private static void TraitLine(StringBuilder sb, Trait t, string tag)
    {
        if (t == null) return;
        sb.Append("<b>").Append(t.Name).Append("</b> ").Append(StatText.Paint("(" + tag + ")", 0)).Append('\n');
        string d = FirstLine(t.description);
        if (!string.IsNullOrEmpty(d)) sb.Append(StatText.Paint(d, 0)).Append('\n');
        foreach (StatText.Line l in StatText.Of(t)) sb.Append("   • ").Append(l.Rich).Append('\n');
    }

    private static void Section(StringBuilder sb, string title)
    {
        sb.Append("\n<color=").Append(StatText.HeaderColor).Append("><size=13><b>").Append(title.ToUpperInvariant()).Append("</b></size></color>\n");
    }

    private static string ArchetypeText(CharacterArchetype a, string extra)
    {
        var sb = new StringBuilder();
        sb.Append("<size=20><b>").Append(a.Name).Append("</b></size>\n");
        if (!string.IsNullOrEmpty(a.description)) sb.Append(a.description).Append('\n');
        if (a.setsHeight) sb.Append(StatText.Paint($"Height {Mathf.Min(a.heightRange.x, a.heightRange.y):0.00} - {Mathf.Max(a.heightRange.x, a.heightRange.y):0.00} m", 0)).Append('\n');
        string lookText = a.appearance != null ? a.appearance.Describe() : "";
        if (!string.IsNullOrEmpty(lookText)) sb.Append(StatText.Paint("Look: " + lookText, 0)).Append('\n');
        if (a.bonusTraitPoints != 0) sb.Append(StatText.Paint($"{a.bonusTraitPoints:+0;-0} trait points", a.bonusTraitPoints > 0 ? 1 : -1)).Append('\n');
        List<StatText.Line> lines = StatText.Of(a);
        if (lines.Count > 0) sb.Append('\n');
        foreach (StatText.Line l in lines) sb.Append("• ").Append(l.Rich).Append('\n');
        foreach (TraitAffinity aff in a.traitAffinities) if (aff != null) sb.Append("• ").Append(StatText.Paint(aff.Describe(), 0)).Append('\n');
        if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
        return sb.ToString().TrimEnd();
    }

    private static string ClassText(PlayerClass c)
    {
        var sb = new StringBuilder();
        sb.Append("<size=20><b>").Append(c.GetClassName()).Append("</b></size>\n");
        if (!string.IsNullOrEmpty(c.classDescription)) sb.Append(c.classDescription).Append('\n');
        sb.Append(StatText.Paint($"Health {c.health:0} · Stamina {c.stamina:0} · Mana {c.mana:0} · {c.traitPoints} trait points", 0)).Append("\n\n");
        var lines = new List<StatText.Line>();
        StatText.Add(lines, c.GetCombatStatModifiers());
        if (c.archetype != null) lines.AddRange(StatText.Of(c.archetype));
        foreach (StatText.Line l in lines) sb.Append("• ").Append(l.Rich).Append('\n');
        List<Trait> start = c.GetStartingTraits();
        if (start.Count > 0) sb.Append("\n").Append(StatText.Paint("Starts with: " + string.Join(", ", start.ConvertAll(t => t.Name)), 1)).Append('\n');
        if (c.preferredTraitTypes.Count > 0) sb.Append(StatText.Paint("Cheaper traits: " + string.Join(", ", c.preferredTraitTypes), 1)).Append('\n');
        if (c.difficultTraitTypes.Count > 0) sb.Append(StatText.Paint("Dearer traits: " + string.Join(", ", c.difficultTraitTypes), -1)).Append('\n');
        return sb.ToString().TrimEnd();
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string t = text.Trim();
        int nl = t.IndexOf('\n');
        if (nl >= 0) t = t.Substring(0, nl);
        return t.Length > 140 ? t.Substring(0, 137) + "..." : t;
    }

    private static void Clear(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }

    // ------------------------------------------------------------------ create
    private void Create()
    {
        string name = CleanedName();
        string problem = build.Problem(catalog, name);
        if (problem == null && catalog.uniqueNames && CharacterSaveStore.NameTaken(name))
            problem = "You already have a character with that name.";
        if (problem != null)
        {
            Say(problem, true);
            return;
        }
        CharacterSave save = CharacterSave.New(name);
        build.WriteTo(save);
        save.appearance = look.Clone();
        if (!CharacterSaveStore.Save(save))
        {
            Say("The character could not be saved (see the Console).", true);
            return;
        }
        Created?.Invoke(save);
    }
}
