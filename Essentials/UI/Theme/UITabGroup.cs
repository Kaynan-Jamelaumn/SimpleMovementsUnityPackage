using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Highlights the tab whose page is open: each tab button is paired with the page it shows, and the pair whose page
/// is active is drawn as chosen. Works with any code that opens the pages (e.g. <see cref="PauseMenuSettings"/>).
/// </summary>
public class UITabGroup : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public GameObject page;
    }

    public List<Tab> tabs = new List<Tab>();

    private readonly List<bool> shown = new List<bool>();

    public void Add(Button button, GameObject page)
    {
        if (button != null && page != null)
            tabs.Add(new Tab { button = button, page = page });
    }

    private void OnEnable()
    {
        shown.Clear();
        Refresh(true);
    }

    private void LateUpdate() => Refresh(false);

    private void Refresh(bool force)
    {
        while (shown.Count < tabs.Count) shown.Add(false);
        for (int i = 0; i < tabs.Count; i++)
        {
            Tab t = tabs[i];
            if (t == null || t.button == null)
                continue;
            bool on = t.page != null && t.page.activeSelf;
            if (!force && shown[i] == on)
                continue;
            shown[i] = on;
            UITheme.SetSelected(t.button, on);
        }
    }
}
