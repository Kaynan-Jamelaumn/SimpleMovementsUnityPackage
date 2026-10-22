#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools ▸ SimpleMovements ▸ Alchemy ▸ Open Alchemy Lab: runs preparations without a scene, on the project's rules.
/// <list type="bullet">
/// <item><b>Procedure</b>: a list of timed steps (pour water, add an ingredient, set the fire, stir, bottle, set a
/// surroundings variable) on a chosen vessel, ambient temperature and seed.</item>
/// <item><b>Timeline</b>: temperature, the amounts of every substance and what the liquid would be at each moment
/// (outcome bands with their quality) - how a preparation passes from one result to the next.</item>
/// <item><b>Outcome map</b>: the same procedure with the fire at 0 … 100 %, against time: which results exist where,
/// for tuning content and checking that the rules stay consistent.</item>
/// </list>
/// A preparation running in Play Mode can be opened here from its station's inspector (its history is replayed).
/// </summary>
public class AlchemyLabWindow : EditorWindow
{
    private enum LabAction
    {
        PourWater, AddIngredient, SetHeat, Stir, Bottle, SetVariable, CollectDistillate, SetAmbient,
        AddFuel, Bellows, AirVent, Stirring, Bath, Filter, Decant, Skim, PourSubstance,
    }

    [Serializable]
    private class LabStep
    {
        public float time;
        public LabAction action = LabAction.AddIngredient;
        public string ingredient = "Sunpetal";
        public int count = 1;
        public float value = 0.35f;
        public string variable = "night";
        [Tooltip("The form it goes in as (\"coarse\", \"ground\", \"powder\"); empty = its own.")]
        public string form = "";
        [Tooltip("How evenly it was ground (1 = all at that form).")]
        public float evenness = 1f;
        [NonSerialized] public AlchemyIngredientProfile profile;
    }

    private struct Sample
    {
        public double time, temperature, quality;
        public double[] amounts;
        public string outcome, label;
    }

    [SerializeField] private AlchemyDatabase database;
    [SerializeField] private AlchemyEquipment equipmentAsset;
    [SerializeField] private int equipmentPreset;
    [SerializeField] private float ambient = 20f;
    [SerializeField] private bool night;
    [SerializeField] private int seed = 1;
    [SerializeField] private float duration = 600f;
    [SerializeField] private List<LabStep> steps = new List<LabStep>();
    [SerializeField] private int tab;

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<string> results = new List<string>();
    private string[,] map;
    private double[,] mapQuality;
    private Vector2 scroll;
    private AlchemyRules rules;

    public static void Open(AlchemyDatabase db = null, AlchemyBrew from = null)
    {
        var w = GetWindow<AlchemyLabWindow>("Alchemy Lab");
        w.minSize = new Vector2(820f, 560f);
        if (db != null) w.database = db;
        if (w.steps.Count == 0) w.DefaultProcedure();
        if (from != null) w.FromHistory(from);
        w.Run();
    }

    private void DefaultProcedure()
    {
        steps.Clear();
        steps.Add(new LabStep { time = 0f, action = LabAction.PourWater, value = 20f });
        steps.Add(new LabStep { time = 0f, action = LabAction.AddIngredient, ingredient = "Sunpetal", count = 3 });
        steps.Add(new LabStep { time = 0f, action = LabAction.SetHeat, value = 0.35f });
    }

    private static readonly string[] GrindIds = { "", "coarse", "ground", "powder" };
    private static readonly string[] GrindNames = { "as it is", "partially ground", "finely ground", "pulverised" };

    private void FromHistory(AlchemyBrew brew)
    {
        steps.Clear();
        double dt = brew.Rules.Settings.stepSeconds;
        foreach (AlchemyOperation op in brew.History)
        {
            var s = new LabStep { time = (float)(op.step * dt) };
            switch (op.kind)
            {
                case AlchemyOpKind.AddIngredient: s.action = LabAction.AddIngredient; s.ingredient = op.label; s.count = op.count; s.form = op.form; s.evenness = op.evenness; s.profile = op.profile?.Clone(); break;
                case AlchemyOpKind.AddSubstance:
                    s.action = op.id == AlchemyDefaults.Water ? LabAction.PourWater : LabAction.PourSubstance;
                    s.ingredient = op.id;
                    s.value = op.value;
                    break;
                case AlchemyOpKind.AddFuel: s.action = LabAction.AddFuel; s.ingredient = op.id; s.count = op.count; break;
                case AlchemyOpKind.Bellows: s.action = LabAction.Bellows; s.value = op.value; break;
                case AlchemyOpKind.SetDamper: s.action = LabAction.AirVent; s.value = op.value; break;
                case AlchemyOpKind.SetStirring: s.action = LabAction.Stirring; s.value = op.value; break;
                case AlchemyOpKind.SetBath: s.action = LabAction.Bath; s.value = op.value; break;
                case AlchemyOpKind.Filter: s.action = LabAction.Filter; s.ingredient = op.id; break;
                case AlchemyOpKind.Decant: s.action = LabAction.Decant; break;
                case AlchemyOpKind.Skim: s.action = LabAction.Skim; break;
                case AlchemyOpKind.SetHeat: s.action = LabAction.SetHeat; s.value = op.value; break;
                case AlchemyOpKind.Stir: s.action = LabAction.Stir; s.value = op.value; break;
                case AlchemyOpKind.Bottle: s.action = LabAction.Bottle; s.count = op.count; break;
                case AlchemyOpKind.SetVariable: s.action = LabAction.SetVariable; s.variable = op.id; s.value = op.value; break;
                case AlchemyOpKind.CollectDistillate: s.action = LabAction.CollectDistillate; break;
                case AlchemyOpKind.SetAmbient:
                    if (op.step == 0) { ambient = op.value; continue; } // the starting surroundings
                    s.action = LabAction.SetAmbient; s.value = op.value; break;
                default: continue;
            }
            steps.Add(s);
        }
        equipmentAsset = null;
        seed = brew.Seed;
        duration = Mathf.Max(60f, (float)brew.Elapsed + 60f);
    }

    // ------------------------------------------------------------------ running
    private AlchemyEquipmentProfile Equipment()
    {
        if (equipmentAsset != null)
            return equipmentAsset.Profile;
        List<AlchemyEquipmentProfile> presets = AlchemyDefaults.Equipment();
        return presets[Mathf.Clamp(equipmentPreset, 0, presets.Count - 1)];
    }

    private AlchemyBrew Simulate(float heatOverride, List<Sample> into, float sampleEvery, List<string> log)
    {
        AlchemyDatabase db = database != null ? database : AlchemyDatabase.Instance;
        rules = db.Rules;
        var brew = new AlchemyBrew(rules, Equipment(), ambient, seed);
        if (night) brew.SetVariable(AlchemyVariables.Night, 1, 0);
        var ordered = new List<LabStep>(steps);
        ordered.Sort((a, b) => a.time.CompareTo(b.time));
        int next = 0;
        for (double t = 0; t <= duration + 1e-6; t += sampleEvery)
        {
            while (next < ordered.Count && ordered[next].time <= t + 1e-6)
            {
                brew.RunUntil(ordered[next].time);
                Apply(brew, db, ordered[next], heatOverride, log);
                next++;
            }
            brew.RunUntil(t);
            if (into != null)
            {
                AlchemyResult r = brew.Evaluate();
                into.Add(new Sample
                {
                    time = brew.Elapsed, temperature = brew.Mixture.Temperature, quality = r.Quality, amounts = brew.Mixture.CopyAmounts(),
                    outcome = r.LiquidUnits > rules.Settings.traceAmount ? r.OutcomeId : "", label = r.DisplayName,
                });
            }
        }
        return brew;
    }

    private static void Apply(AlchemyBrew brew, AlchemyDatabase db, LabStep s, float heatOverride, List<string> log)
    {
        string why = "";
        switch (s.action)
        {
            case LabAction.PourWater:
                brew.AddSubstance(AlchemyDefaults.Water, s.value, 15, out why);
                break;
            case LabAction.AddIngredient:
            {
                AlchemyIngredientProfile p = s.profile ?? FindProfile(db, s.ingredient);
                if (p == null) { why = $"Unknown ingredient '{s.ingredient}'."; break; }
                brew.AddIngredient(s.ingredient, s.ingredient, p, Mathf.Max(1, s.count), out why, string.IsNullOrEmpty(s.form) ? null : s.form,
                    string.IsNullOrEmpty(s.form) ? 1f : s.evenness);
                break;
            }
            case LabAction.PourSubstance: brew.AddSubstance(s.ingredient, s.value, brew.Mixture.Ambient, out why); break;
            case LabAction.AddFuel: brew.AddFuel(string.IsNullOrEmpty(s.ingredient) ? "firewood" : s.ingredient, Mathf.Max(1, s.count), out why); break;
            case LabAction.Bellows: brew.Bellows(s.value > 0f ? s.value : 1f); break;
            case LabAction.AirVent: brew.SetDamper(s.value); break;
            case LabAction.Stirring: brew.SetStirring(s.value, 0); break;
            case LabAction.Bath: brew.SetBath((AlchemyBath)Mathf.Clamp(Mathf.RoundToInt(s.value), 0, 2)); break;
            case LabAction.Filter:
            {
                AlchemyHarvest h = brew.Filter(string.IsNullOrEmpty(s.ingredient) ? "paper" : s.ingredient, out why);
                if (h != null) log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  Filtered: caught {Caught(brew.Rules, h)}");
                break;
            }
            case LabAction.Decant:
            {
                AlchemyHarvest h = brew.Decant(out why);
                if (h != null) log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  Poured off the top layer: {h.Liquid.DisplayName} × {h.Liquid.Bottles} ({Caught(brew.Rules, h)})");
                break;
            }
            case LabAction.Skim:
            {
                AlchemyHarvest h = brew.Skim(out why);
                if (h != null) log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  Skimmed: {Caught(brew.Rules, h)}");
                break;
            }
            case LabAction.SetHeat: brew.SetHeat(heatOverride >= 0f ? heatOverride : s.value); break;
            case LabAction.Stir: brew.Stir(s.value > 0f ? s.value : 1f); break;
            case LabAction.Bottle:
            {
                AlchemyResult r = brew.Bottle(s.count > 0 ? s.count : int.MaxValue);
                log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  Bottled {r.Bottles} × {r.DisplayName} (quality {r.Quality * 100:0}%)");
                break;
            }
            case LabAction.SetVariable: brew.SetVariable(s.variable, s.value, 0); break;
            case LabAction.SetAmbient: brew.SetAmbient(s.value); break;
            case LabAction.CollectDistillate:
            {
                AlchemyHarvest h = brew.CollectDistillate();
                log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  Distillate: {h.Liquid.DisplayName} × {h.Liquid.Bottles}");
                break;
            }
        }
        if (!string.IsNullOrEmpty(why))
            log?.Add($"{AlchemyText.Clock(brew.Elapsed)}  <refused> {why}");
    }

    /// <summary>"1.8 u Alum Crystals, 0.6 u Silt" (what a filter, decant or skim took out).</summary>
    private static string Caught(AlchemyRules rules, AlchemyHarvest h)
    {
        var parts = new List<string>();
        if (h.Taken != null)
            for (int i = 0; i < h.Taken.Length; i++)
                if (h.Taken[i] >= 0.05) parts.Add($"{AlchemyText.Units(h.Taken[i])} {rules.Substances[i].Name}");
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    /// <summary>An ingredient's profile by name: an ingredient asset in the project, a database item ingredient, a preset.</summary>
    private static AlchemyIngredientProfile FindProfile(AlchemyDatabase db, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO " + name))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || item.Name != name && item.name != name) continue;
            IAlchemyIngredient ing = db.IngredientOf(item);
            if (ing != null) return ing.AlchemyProfile;
        }
        return AlchemyDefaults.Ingredient(name)?.profile;
    }

    private void Run()
    {
        samples.Clear();
        results.Clear();
        AlchemyBrew brew = Simulate(-1f, samples, Mathf.Max(0.2f, duration / 400f), results);
        AlchemyResult end = brew.Evaluate();
        results.Add($"{AlchemyText.Clock(brew.Elapsed)}  At the end: {end.DisplayName} · quality {end.Quality * 100:0}% · {end.Bottles} bottle(s){(end.Notes.Count > 0 ? " · " + string.Join(" ", end.Notes) : "")}");
        if (brew.Mixture.DistillateVolume > 0.05)
        {
            AlchemyResult d = brew.EvaluateDistillate();
            results.Add($"Distillate: {d.DisplayName} · {AlchemyText.Units(brew.Mixture.DistillateVolume)}");
        }
        Repaint();
    }

    private void RunMap()
    {
        const int heats = 11, times = 40;
        map = new string[heats, times];
        mapQuality = new double[heats, times];
        for (int h = 0; h < heats; h++)
        {
            var list = new List<Sample>();
            Simulate(h / (float)(heats - 1), list, duration / times, null);
            for (int t = 0; t < times && t + 1 < list.Count; t++)
            {
                map[h, t] = list[t + 1].outcome;
                mapQuality[h, t] = list[t + 1].quality;
            }
        }
        Repaint();
    }

    // ------------------------------------------------------------------ GUI
    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            database = (AlchemyDatabase)EditorGUILayout.ObjectField("Database", database, typeof(AlchemyDatabase), false);
            if (database == null) EditorGUILayout.LabelField("(project / built-in)", GUILayout.Width(130f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            equipmentAsset = (AlchemyEquipment)EditorGUILayout.ObjectField("Vessel", equipmentAsset, typeof(AlchemyEquipment), false);
            if (equipmentAsset == null)
            {
                List<AlchemyEquipmentProfile> presets = AlchemyDefaults.Equipment();
                var names = new string[presets.Count];
                for (int i = 0; i < names.Length; i++) names[i] = presets[i].displayName;
                equipmentPreset = EditorGUILayout.Popup(equipmentPreset, names, GUILayout.Width(160f));
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            ambient = EditorGUILayout.FloatField("Ambient °C", ambient);
            night = EditorGUILayout.ToggleLeft("Night", night, GUILayout.Width(60f));
            seed = EditorGUILayout.IntField("Seed", seed);
            duration = Mathf.Max(10f, EditorGUILayout.FloatField("Duration (s)", duration));
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Procedure", EditorStyles.boldLabel);
        int remove = -1;
        for (int i = 0; i < steps.Count; i++)
        {
            LabStep s = steps[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                s.time = EditorGUILayout.FloatField(s.time, GUILayout.Width(50f));
                s.action = (LabAction)EditorGUILayout.EnumPopup(s.action, GUILayout.Width(130f));
                switch (s.action)
                {
                    case LabAction.AddIngredient:
                        s.ingredient = EditorGUILayout.TextField(s.ingredient);
                        if (GUILayout.Button("▾", GUILayout.Width(22f))) IngredientMenu(s);
                        s.count = Mathf.Max(1, EditorGUILayout.IntField(s.count, GUILayout.Width(40f)));
                        int g = Array.IndexOf(GrindIds, s.form ?? "");
                        g = EditorGUILayout.Popup(Mathf.Max(0, g), GrindNames, GUILayout.Width(92f));
                        s.form = GrindIds[g];
                        if (g > 0) s.evenness = EditorGUILayout.Slider(s.evenness, 0f, 1f, GUILayout.Width(120f));
                        break;
                    case LabAction.PourSubstance:
                        s.ingredient = EditorGUILayout.TextField(s.ingredient);
                        s.value = EditorGUILayout.FloatField(s.value, GUILayout.Width(60f));
                        break;
                    case LabAction.AddFuel:
                        s.ingredient = EditorGUILayout.TextField(string.IsNullOrEmpty(s.ingredient) ? "firewood" : s.ingredient);
                        s.count = Mathf.Max(1, EditorGUILayout.IntField(s.count, GUILayout.Width(40f)));
                        break;
                    case LabAction.Bellows: s.value = EditorGUILayout.Slider(s.value <= 0f ? 1f : s.value, 0f, 1f); break;
                    case LabAction.AirVent:
                    case LabAction.Stirring: s.value = EditorGUILayout.Slider(s.value, 0f, 1f); break;
                    case LabAction.Bath: s.value = EditorGUILayout.Popup(Mathf.Clamp(Mathf.RoundToInt(s.value), 0, 2), new[] { "On the fire", "Water bath", "Ice bath" }); break;
                    case LabAction.Filter: s.ingredient = EditorGUILayout.TextField(string.IsNullOrEmpty(s.ingredient) ? "paper" : s.ingredient); break;
                    case LabAction.PourWater: s.value = EditorGUILayout.FloatField("units", s.value); break;
                    case LabAction.SetHeat: s.value = EditorGUILayout.Slider(s.value, -1f, 1f); break;
                    case LabAction.Stir: s.value = EditorGUILayout.Slider(s.value <= 0f ? 1f : s.value, 0f, 1f); break;
                    case LabAction.Bottle: s.count = EditorGUILayout.IntField("bottles (0 = all)", s.count); break;
                    case LabAction.SetVariable:
                        s.variable = EditorGUILayout.TextField(s.variable);
                        s.value = EditorGUILayout.FloatField(s.value, GUILayout.Width(60f));
                        break;
                    case LabAction.SetAmbient: s.value = EditorGUILayout.FloatField("°C", s.value); break;
                    default: GUILayout.FlexibleSpace(); break;
                }
                if (GUILayout.Button("✕", GUILayout.Width(24f))) remove = i;
            }
        }
        if (remove >= 0) steps.RemoveAt(remove);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Step", GUILayout.Width(80f)))
                steps.Add(new LabStep { time = steps.Count > 0 ? steps[steps.Count - 1].time : 0f });
            if (GUILayout.Button("Default", GUILayout.Width(70f))) DefaultProcedure();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Run", GUILayout.Width(90f))) Run();
            if (GUILayout.Button("Outcome Map", GUILayout.Width(110f))) { RunMap(); tab = 1; }
        }

        tab = GUILayout.Toolbar(tab, new[] { "Timeline", "Outcome map" });
        Rect chart = GUILayoutUtility.GetRect(200f, 4000f, 220f, 340f);
        if (tab == 0) DrawTimeline(chart);
        else DrawMap(chart);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(80f));
        foreach (string r in results)
            EditorGUILayout.LabelField(r, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void IngredientMenu(LabStep s)
    {
        var menu = new GenericMenu();
        foreach (AlchemyDefaults.IngredientPreset p in AlchemyDefaults.Ingredients())
        {
            string n = p.name;
            menu.AddItem(new GUIContent(n), s.ingredient == n, () => { s.ingredient = n; s.profile = null; });
        }
        menu.ShowAsContext();
    }

    private void DrawTimeline(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.14f));
        if (samples.Count < 2 || rules == null)
            return;
        Rect band = new Rect(r.x, r.yMax - 34f, r.width, 34f);
        Rect plot = new Rect(r.x + 4f, r.y + 6f, r.width - 8f, r.height - 46f);
        double tMax = samples[samples.Count - 1].time;
        if (tMax <= 0) return;
        double aMax = 0.5, tempMax = 120;
        foreach (Sample s in samples)
        {
            tempMax = Math.Max(tempMax, s.temperature);
            for (int i = 0; i < s.amounts.Length; i++)
                if (rules.Substances[i].role != SubstanceRole.Solvent) aMax = Math.Max(aMax, s.amounts[i]);
        }
        float X(double t) => plot.x + (float)(t / tMax) * plot.width;

        // Outcome bands.
        int start = 0;
        for (int k = 1; k <= samples.Count; k++)
        {
            if (k < samples.Count && samples[k].outcome == samples[start].outcome) continue;
            Rect seg = new Rect(X(samples[start].time), band.y, X(samples[k - 1].time) - X(samples[start].time) + 1f, band.height);
            EditorGUI.DrawRect(seg, OutcomeColor(samples[start].outcome));
            if (seg.width > 60f)
                GUI.Label(seg, samples[start].label, EditorStyles.miniBoldLabel);
            start = k;
        }

        Handles.BeginGUI();
        // Substances (not solvents) by amount.
        for (int i = 0; i < rules.SubstanceCount; i++)
        {
            if (rules.Substances[i].role == SubstanceRole.Solvent) continue;
            double peak = 0;
            foreach (Sample s in samples) peak = Math.Max(peak, s.amounts[i]);
            if (peak < 0.05) continue;
            var pts = new Vector3[samples.Count];
            for (int k = 0; k < samples.Count; k++)
                pts[k] = new Vector3(X(samples[k].time), plot.yMax - (float)(samples[k].amounts[i] / aMax) * plot.height, 0f);
            Handles.color = rules.Substances[i].color;
            Handles.DrawAAPolyLine(2.5f, pts);
            int at = 0;
            for (int k = 1; k < samples.Count; k++) if (samples[k].amounts[i] > samples[at].amounts[i]) at = k;
            GUI.color = rules.Substances[i].color;
            GUI.Label(new Rect(pts[at].x + 3f, pts[at].y - 14f, 160f, 16f), rules.Substances[i].Name, EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        // Temperature (dashed white) and quality (green, 0-1).
        var temp = new Vector3[samples.Count];
        var qual = new Vector3[samples.Count];
        for (int k = 0; k < samples.Count; k++)
        {
            temp[k] = new Vector3(X(samples[k].time), plot.yMax - (float)(samples[k].temperature / tempMax) * plot.height, 0f);
            qual[k] = new Vector3(X(samples[k].time), plot.yMax - (float)samples[k].quality * plot.height, 0f);
        }
        Handles.color = new Color(1f, 1f, 1f, 0.6f);
        Handles.DrawDottedLines(Pairs(temp), 3f);
        Handles.color = new Color(0.45f, 0.9f, 0.5f, 0.5f);
        Handles.DrawAAPolyLine(1.5f, qual);
        Handles.EndGUI();
        GUI.Label(new Rect(plot.x, plot.y, 400f, 16f), $"amounts (max {aMax:0.#} u) · ··· temperature (max {tempMax:0} °C) · green: quality", EditorStyles.miniLabel);
        GUI.Label(new Rect(plot.xMax - 60f, band.y - 16f, 60f, 16f), AlchemyText.Clock(tMax), EditorStyles.miniLabel);
    }

    private static Vector3[] Pairs(Vector3[] line)
    {
        var p = new Vector3[Math.Max(0, (line.Length - 1) * 2)];
        for (int i = 0; i + 1 < line.Length; i++)
        {
            p[i * 2] = line[i];
            p[i * 2 + 1] = line[i + 1];
        }
        return p;
    }

    private void DrawMap(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.14f));
        if (map == null)
        {
            GUI.Label(r, "Press Outcome Map: the procedure is run with the fire at 0 … 100 % (rows) against time (columns).", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        int heats = map.GetLength(0), times = map.GetLength(1);
        Rect grid = new Rect(r.x + 44f, r.y + 6f, r.width - 220f, r.height - 26f);
        float cw = grid.width / times, ch = grid.height / heats;
        var legend = new Dictionary<string, Color>();
        for (int h = 0; h < heats; h++)
            for (int t = 0; t < times; t++)
            {
                string id = map[h, t] ?? "";
                Color c = OutcomeColor(id);
                c *= 0.55f + 0.45f * (float)mapQuality[h, t];
                c.a = 1f;
                EditorGUI.DrawRect(new Rect(grid.x + t * cw, grid.yMax - (h + 1) * ch, cw + 0.5f, ch + 0.5f), c);
                if (!legend.ContainsKey(id)) legend[id] = OutcomeColor(id);
            }
        for (int h = 0; h < heats; h += 2)
            GUI.Label(new Rect(r.x, grid.yMax - (h + 1) * ch, 44f, ch), $"{h * 100 / (heats - 1)}%", EditorStyles.miniLabel);
        GUI.Label(new Rect(grid.x, grid.yMax + 2f, 200f, 16f), "0:00", EditorStyles.miniLabel);
        GUI.Label(new Rect(grid.xMax - 40f, grid.yMax + 2f, 60f, 16f), AlchemyText.Clock(duration), EditorStyles.miniLabel);
        float y = r.y + 6f;
        foreach (KeyValuePair<string, Color> kv in legend)
        {
            EditorGUI.DrawRect(new Rect(grid.xMax + 12f, y + 3f, 12f, 12f), kv.Value);
            AlchemyOutcome o = rules != null ? rules.Outcome(kv.Key) : null;
            GUI.Label(new Rect(grid.xMax + 28f, y, 180f, 18f), o != null ? o.Name : string.IsNullOrEmpty(kv.Key) ? "(no liquid)" : kv.Key, EditorStyles.miniLabel);
            y += 18f;
        }
    }

    /// <summary>A stable colour per outcome id (its potency substance's colour when it has one).</summary>
    private Color OutcomeColor(string id)
    {
        if (string.IsNullOrEmpty(id))
            return new Color(0.2f, 0.2f, 0.22f);
        AlchemyOutcome o = rules?.Outcome(id);
        if (o != null && o.HasPotency && o.potency.mode == SubstanceSelectorMode.Substance && rules.Substance(o.potency.id) != null)
            return rules.Substance(o.potency.id).color * 0.85f;
        int hash = id.GetHashCode();
        return Color.HSVToRGB(((hash & 0xffff) / 65535f), 0.35f, 0.55f);
    }
}
#endif
