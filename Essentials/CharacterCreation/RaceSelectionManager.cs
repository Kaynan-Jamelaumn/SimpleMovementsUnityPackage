using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional race step of the character creation screen: one button per race (the class button prefab is reused), a
/// description, and a height slider limited to the race's range. Without races configured it does nothing, so games
/// that only use classes keep the old screen.
/// </summary>
public class RaceSelectionManager
{
    public struct RaceUIReferences
    {
        public Transform raceListContainer;
        public GameObject raceButtonPrefab;
        public TMP_Text raceDescription;
        public Slider heightSlider;
        public TMP_Text heightText;
    }

    private readonly CharacterCreationUI mainUI;
    private readonly RaceUIReferences references;
    private readonly Dictionary<GameObject, CharacterArchetype> buttons = new Dictionary<GameObject, CharacterArchetype>();

    public RaceSelectionManager(CharacterCreationUI ui, RaceUIReferences refs)
    {
        mainUI = ui;
        references = refs;
        if (references.heightSlider != null)
        {
            references.heightSlider.onValueChanged.RemoveAllListeners();
            references.heightSlider.onValueChanged.AddListener(v =>
            {
                mainUI.SetSelectedHeight(v);
                UpdateHeightText();
            });
            references.heightSlider.gameObject.SetActive(false);
        }
    }

    public void LoadRaces(List<CharacterArchetype> races)
    {
        Clear();
        if (races == null || references.raceListContainer == null || references.raceButtonPrefab == null)
            return;
        foreach (CharacterArchetype race in races)
        {
            if (race == null || !race.availableAtCreation)
                continue;
            GameObject obj = Object.Instantiate(references.raceButtonPrefab, references.raceListContainer);
            obj.SetActive(true);
            Button button = obj.GetComponent<Button>();
            TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = race.Name;
            buttons[obj] = race;
            if (button != null)
            {
                CharacterArchetype r = race;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    mainUI.PlayButtonClickSound();
                    Select(r);
                });
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(references.raceListContainer as RectTransform);
    }

    public void Select(CharacterArchetype race)
    {
        if (!mainUI.TrySelectRace(race))
            return;
        foreach (KeyValuePair<GameObject, CharacterArchetype> pair in buttons)
        {
            Button b = pair.Key != null ? pair.Key.GetComponent<Button>() : null;
            if (b == null) continue;
            ColorBlock c = b.colors;
            c.normalColor = pair.Value == race ? Color.green : Color.white;
            c.highlightedColor = pair.Value == race ? Color.green * 0.8f : Color.gray;
            b.colors = c;
        }
        if (references.raceDescription != null)
            references.raceDescription.text = race != null ? race.GetFormattedDescription() : "";
        if (references.heightSlider != null)
        {
            bool show = race != null && race.setsHeight;
            references.heightSlider.gameObject.SetActive(show);
            if (show)
            {
                references.heightSlider.minValue = Mathf.Min(race.heightRange.x, race.heightRange.y);
                references.heightSlider.maxValue = Mathf.Max(race.heightRange.x, race.heightRange.y);
                references.heightSlider.SetValueWithoutNotify(race.ClampHeight(race.defaultHeight));
                mainUI.SetSelectedHeight(references.heightSlider.value);
            }
        }
        UpdateHeightText();
    }

    private void UpdateHeightText()
    {
        if (references.heightText == null)
            return;
        float h = mainUI.SelectedHeight;
        references.heightText.text = h > 0f ? $"Height: {h:0.00} m" : "";
    }

    public void Clear()
    {
        if (references.raceListContainer != null)
            for (int i = references.raceListContainer.childCount - 1; i >= 0; i--)
                Object.Destroy(references.raceListContainer.GetChild(i).gameObject);
        buttons.Clear();
    }
}
