using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The veterinarian's window (built from code, one per canvas): information only - what the vet treats (and which
/// illnesses only the vet can cure once they are bad), what a visit costs for each kind of animal, hints about the
/// veterinary medicines (folk words, never recipes), the plain remedies a merchant sells, and how to send for the vet:
/// from the pen, in its window. It asks the server nothing: everything it shows comes from the game's content, the
/// same on every machine.
/// </summary>
[DisallowMultipleComponent]
public class AnimalVetWindow : NPCWindow
{
    private static readonly Vector2 WindowSize = new Vector2(1000f, 720f);

    private AnimalVeterinarian vet;
    private TextMeshProUGUI title, subtitle, purse;
    private RectTransform body;
    private float nextFit;

    public override string PanelName => "Veterinarian";

    // ------------------------------------------------------------------ opening and closing
    /// <summary>Opens the window of <paramref name="vet"/> for <paramref name="session"/> (on the player's canvas).</summary>
    public static AnimalVetWindow Open(AnimalVeterinarian vet, NPCInteractionSession session)
    {
        if (vet == null || session == null) return null;
        AnimalVetWindow w = NPCWindowCache.Get<AnimalVetWindow>(session.Canvas, null, null, Create);
        w.Show(vet, session);
        return w;
    }

    /// <summary>Closes the window shown for <paramref name="session"/>.</summary>
    public static void CloseFor(AnimalVeterinarian vet, NPCInteractionSession session)
    {
        if (session == null) return;
        AnimalVetWindow w = NPCWindowCache.TryGet<AnimalVetWindow>(session.Canvas, null, null);
        if (w != null && ReferenceEquals(w.Session, session)) w.Unbind();
    }

    private static AnimalVetWindow Create(Transform parent)
    {
        Image win = UITheme.Window("Veterinarian Window", parent, WindowSize);
        AnimalVetWindow w = win.gameObject.AddComponent<AnimalVetWindow>();
        w.Build(win.transform);
        return w;
    }

    private void Show(AnimalVeterinarian veterinarian, NPCInteractionSession session)
    {
        Bind(session); // a window still showing another session lets it go first (OnUnbound)
        if (!ReferenceEquals(Session, session)) return;
        vet = veterinarian;
        HousingUI.FitToCanvas((RectTransform)transform, WindowSize);
        Render();
    }

    protected override void OnUnbound()
    {
        vet = null;
    }

    /// <summary>Close button / Escape: back to the NPC's other options (a merchant at the same counter), or the end.</summary>
    public override void RequestClose()
    {
        NPCInteractionSession s = Session;
        if (s != null && vet != null) s.BehaviourFinished(vet);
        if (s == null || ReferenceEquals(Session, s)) base.RequestClose();
    }

    protected override void Update()
    {
        base.Update();
        if (Session == null) return;
        if (vet == null || !vet.isActiveAndEnabled)
        {
            RequestClose();
            return;
        }
        if (Time.unscaledTime >= nextFit)
        {
            nextFit = Time.unscaledTime + 1f;
            HousingUI.FitToCanvas((RectTransform)transform, WindowSize);
        }
    }

    // ------------------------------------------------------------------ building
    private void Build(Transform w)
    {
        HousingUI.Header(w, RequestClose, out title, out subtitle, out purse);
        Image panel = HousingUI.Panel(w, "Information", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -100f));
        body = HousingUI.FillList(panel.transform, "Text", 14, 10f);
    }

    private void Render()
    {
        try
        {
            RenderHeader();
            RenderBody();
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    private void RenderHeader()
    {
        string who = vet != null && vet.Npc != null ? vet.Npc.DisplayName : "";
        HousingUI.Set(title, string.IsNullOrWhiteSpace(who) ? "The veterinarian" : who);
        HousingUI.Set(subtitle, "Veterinarian - sees to sick and injured animals");
        HousingUI.Set(purse, "");
    }

    private void RenderBody()
    {
        for (int i = body.childCount - 1; i >= 0; i--) Destroy(body.GetChild(i).gameObject);
        AnimalDatabase db = AnimalDatabase.Instance;
        AnimalRules rules = db.Rules;

        // What the vet treats.
        UITheme.Header(body, "What I treat");
        HousingUI.Text(body, "I see to any animal you keep: I find what ails it - even what does not show yet - and treat it there and then.", 16f);
        var gated = new List<string>();
        foreach (AnimalConditionData c in db.AllConditions)
            if (c != null && c.needsVetAbove <= 100f) gated.Add(c.DisplayName.ToLowerInvariant());
        if (rules.vetRequired && gated.Count > 0)
            HousingUI.Text(body, $"Some illnesses grow past what a keeper's medicine can turn - {Join(gated)}. Once they are that bad, your medicine only holds them: send for me.", 16f);
        else
            HousingUI.Text(body, "Here a keeper's own medicine can cure everything, if it is the right one; I still catch what you cannot see.", 16f);

        // The fees.
        UITheme.Header(body, "My fees");
        HousingUI.Text(body, "A visit, and more for a bigger or a dearer animal (a young one costs less):", 16f);
        foreach (AnimalSpeciesData sp in db.AllSpecies)
        {
            if (sp == null) continue;
            AnimalBreedData breed = sp.BreedOrDefault(null);
            string kind = breed != null && !string.IsNullOrWhiteSpace(breed.name) ? $"{sp.DisplayName} ({breed.name})" : sp.DisplayName;
            long fee = vet.FeeFor(null, sp);
            HousingUI.Text(body, $"{kind}: {(fee > 0 ? vet.FeeText(fee) : "free")}", 16f, UITheme.Gold);
        }

        // The veterinary medicines, in folk words.
        bool anyHint = false;
        if (vet.hints != null)
            foreach (AnimalVetHint h in vet.hints)
            {
                if (h == null || string.IsNullOrWhiteSpace(h.hint)) continue;
                if (!anyHint)
                {
                    UITheme.Header(body, "Medicines you can make");
                    anyHint = true;
                }
                AnimalTreatmentData tr = db.Treatment(h.treatment);
                string what = tr != null ? tr.DisplayName : h.treatment;
                HousingUI.Text(body, string.IsNullOrWhiteSpace(what) ? h.hint.Trim() : $"{what}: {h.hint.Trim()}", 16f);
            }

        // The plain remedies (no brewing: a merchant sells them).
        var plain = new List<string>();
        foreach (AnimalTreatmentData tr in db.AllTreatments)
            if (tr != null && Plain(tr)) plain.Add(string.IsNullOrWhiteSpace(tr.description) ? tr.DisplayName : $"{tr.DisplayName}: {tr.description.Trim()}");
        if (plain.Count > 0)
        {
            UITheme.Header(body, "Without brewing");
            foreach (string p in plain) HousingUI.Text(body, p, 16f);
        }

        // How to send for the vet.
        UITheme.Header(body, "Sending for me");
        HousingUI.Text(body, "Send for me from your pen: the Health tab of its window shows my fee for that animal. For one at the boarding stable, use any pen's window.", 16f);
        HousingUI.Note(body, "I cannot come more than once every few hours to the same animal.");
    }

    /// <summary>A plain remedy: none of its items is a brewed potion.</summary>
    private static bool Plain(AnimalTreatmentData tr)
    {
        if (tr.items == null || tr.items.Count == 0) return false;
        foreach (string k in tr.items)
            if (!string.IsNullOrEmpty(k) && k.StartsWith("potion:", StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>"a, b and c".</summary>
    private static string Join(List<string> parts)
    {
        if (parts.Count == 0) return "";
        if (parts.Count == 1) return parts[0];
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.Append(i == parts.Count - 1 ? " and " : ", ");
            sb.Append(parts[i]);
        }
        return sb.ToString();
    }
}
