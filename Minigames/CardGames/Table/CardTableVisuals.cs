using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The cards of a <see cref="CardTable"/> in the world, on every machine, from what that machine may see: each seat's
/// hand held in the sitter's hand (on a Humanoid hand bone; a missing hand holds them in the other, no hands lay them in
/// front of the seat), face up for their owner and face down for everyone else; the played cards in front of each seat
/// (the trick's best card raised); the shared cards (the vira) and the deck in the middle; and bots' bodies.
/// Added by the table (Show Cards In World). Cards are simple quads with the rank and suit written on them.
/// </summary>
[DisallowMultipleComponent]
public class CardTableVisuals : MonoBehaviour
{
    private CardTable table;
    private Transform root;
    private int shownVersion = int.MinValue;
    private readonly List<GameObject> tableCards = new List<GameObject>();
    private readonly List<List<GameObject>> handCards = new List<List<GameObject>>();
    private readonly List<GameObject> botBodies = new List<GameObject>();

    private static Material front, back, highlight;
    private static Mesh quad;

    private const float CardWidth = 0.063f, CardHeight = 0.088f;

    private void Awake() => table = GetComponent<CardTable>();

    private void OnDisable() => Clear();

    private void LateUpdate()
    {
        if (table == null || !table.ShowCardsInWorld)
            return;
        CardTableView v = table.LocalView;
        int version = table.ViewVersion;
        if (version != shownVersion)
        {
            shownVersion = version;
            Rebuild(v);
        }
        PlaceHands(v);
    }

    private void Clear()
    {
        foreach (GameObject g in tableCards) if (g != null) Destroy(g);
        tableCards.Clear();
        foreach (List<GameObject> h in handCards)
            foreach (GameObject g in h) if (g != null) Destroy(g);
        handCards.Clear();
        foreach (GameObject g in botBodies) if (g != null) Destroy(g);
        botBodies.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
        shownVersion = int.MinValue;
    }

    // ------------------------------------------------------------------ building
    private void Rebuild(CardTableView v)
    {
        if (root == null)
        {
            root = new GameObject("Cards (runtime)").transform;
            root.SetParent(transform, false);
        }
        foreach (GameObject g in tableCards) if (g != null) Destroy(g);
        tableCards.Clear();
        foreach (List<GameObject> h in handCards)
            foreach (GameObject g in h) if (g != null) Destroy(g);
        handCards.Clear();
        if (v == null)
        {
            SyncBots(null);
            return;
        }

        Transform c = table.Center;
        float scale = table.CardScale;
        int seats = Mathf.Min(v.seats.Count, table.Seats.Count);
        bool dealt = v.tablePhase != 0;

        // Hands: the viewer's own cards face up, everybody else's face down.
        for (int s = 0; s < seats; s++)
        {
            var list = new List<GameObject>();
            CardSeatView sv = v.seats[s];
            int count = dealt ? sv.cardCount : 0;
            for (int k = 0; k < count; k++)
            {
                byte code = s == v.viewerSeat && k < v.hand.Count ? v.hand[k] : PlayingCard.Hidden;
                list.Add(MakeCard(code, root, scale));
            }
            handCards.Add(list);
        }

        if (dealt)
        {
            // Played cards: in front of each seat, between it and the middle (several of one seat side by side).
            var perSeat = new Dictionary<int, int>();
            var shown = new Dictionary<int, int>();
            foreach (CardPlayView p in v.played)
                perSeat[p.seat] = perSeat.TryGetValue(p.seat, out int cnt) ? cnt + 1 : 1;
            foreach (CardPlayView p in v.played)
            {
                if (p.seat < 0 || p.seat >= seats) continue;
                Vector3 toSeat = Flat(table.Seats[p.seat].SitPoint.position - c.position);
                int k = shown.TryGetValue(p.seat, out int done) ? done : 0;
                shown[p.seat] = k + 1;
                Vector3 side = Vector3.Cross(Vector3.up, toSeat) * ((k - (perSeat[p.seat] - 1) * 0.5f) * CardWidth * 1.1f * scale);
                Vector3 pos = c.position + toSeat * 0.32f + side + Vector3.up * (p.best ? 0.03f : 0.005f + k * 0.001f);
                GameObject card = MakeCard(p.card, root, scale, p.best);
                card.transform.SetPositionAndRotation(pos, FaceUp(-toSeat));
                tableCards.Add(card);
            }
            // The shared cards (the vira; poker's board or the dealer's hand in a row) and the deck beside them.
            for (int i = 0; i < v.shared.Count; i++)
            {
                GameObject card = MakeCard(v.shared[i], root, scale);
                Vector3 at = v.shared.Count >= 2
                    ? c.position + c.right * ((i - (v.shared.Count - 1) * 0.5f) * CardWidth * 1.15f * scale) + Vector3.up * 0.004f
                    : c.position + c.right * (0.05f + i * 0.1f) * scale + Vector3.up * 0.004f;
                card.transform.SetPositionAndRotation(at, v.shared.Count >= 2 ? FaceUp(c.forward) : FaceUp(c.forward) * Quaternion.Euler(0f, 0f, 90f));
                tableCards.Add(card);
            }
            int pile = Mathf.Min(8, Mathf.CeilToInt(v.deckCount / 5f));
            for (int i = 0; i < pile; i++)
            {
                GameObject card = MakeCard(PlayingCard.Hidden, root, scale);
                float off = v.shared.Count >= 2 ? v.shared.Count * 0.5f * CardWidth * 1.15f + 0.06f : 0.06f;
                card.transform.SetPositionAndRotation(c.position - c.right * off * scale + Vector3.up * (0.004f + i * 0.0025f), FaceUp(c.forward));
                tableCards.Add(card);
            }
        }
        SyncBots(v);
    }

    /// <summary>Bots' bodies in their seats (only visuals, on every machine).</summary>
    private void SyncBots(CardTableView v)
    {
        GameObject prefab = table.BotPrefab;
        int seats = table.Seats.Count;
        while (botBodies.Count < seats) botBodies.Add(null);
        for (int s = 0; s < seats; s++)
        {
            bool want = prefab != null && v != null && s < v.seats.Count && v.seats[s].occupied && v.seats[s].bot;
            if (want && botBodies[s] == null)
            {
                Pose p = table.Seats[s].SitPose;
                botBodies[s] = Instantiate(prefab, p.position, p.rotation, root);
                botBodies[s].name = "Bot " + (s + 1);
                Animator a = botBodies[s].GetComponentInChildren<Animator>();
                if (a != null && HasParam(a, "IsSitting")) a.SetBool("IsSitting", true);
            }
            else if (!want && botBodies[s] != null)
            {
                Destroy(botBodies[s]);
                botBodies[s] = null;
            }
        }
    }

    private static bool HasParam(Animator a, string n)
    {
        if (a.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == n) return true;
        return false;
    }

    // ------------------------------------------------------------------ hands follow the bodies
    private void PlaceHands(CardTableView v)
    {
        if (v == null)
            return;
        for (int s = 0; s < handCards.Count && s < table.Seats.Count; s++)
        {
            List<GameObject> cards = handCards[s];
            if (cards.Count == 0) continue;
            CardSeat seat = table.Seats[s];
            Vector3 at;
            Quaternion facing;
            Transform bone = HandBone(table.SeatPlayer(s), out Transform head);
            if (bone != null)
            {
                at = bone.position;
                Vector3 look = head != null ? at - head.position : seat.Facing(at);
                facing = Quaternion.LookRotation(look.sqrMagnitude > 1e-4f ? look.normalized : seat.Facing(at), Vector3.up);
            }
            else
            {
                Pose p = seat.HandPose;
                at = p.position;
                facing = p.rotation;
            }
            float spread = 12f, scale = table.CardScale;
            for (int k = 0; k < cards.Count; k++)
            {
                if (cards[k] == null) continue;
                float t = cards.Count == 1 ? 0f : k / (float)(cards.Count - 1) - 0.5f;
                Quaternion fan = facing * Quaternion.Euler(0f, 0f, -t * spread * (cards.Count - 1));
                Vector3 offset = facing * new Vector3(t * CardWidth * 0.9f * scale, CardHeight * 0.35f * scale, k * 0.002f);
                cards[k].transform.SetPositionAndRotation(at + offset, fan);
            }
        }
    }

    /// <summary>
    /// The hand bone that holds the cards: the right hand, else the left (a lost hand cannot hold them: injury system),
    /// null without a usable hand or a Humanoid rig.
    /// </summary>
    private static Transform HandBone(CombatEntity e, out Transform head)
    {
        head = null;
        if (e == null)
            return null;
        Transform r = AbilitiesStateMachine.PlayerRoot(e);
        Animator a = r != null ? r.GetComponentInChildren<Animator>() : null;
        if (a == null || !a.isHuman)
            return null;
        head = a.GetBoneTransform(HumanBodyBones.Head);
        InjuryController injuries = InjuryController.For(e);
        foreach (bool right in new[] { true, false })
        {
            if (injuries != null && injuries.Settings.enabled)
            {
                InjuryPartState hand = injuries.GetPart(right ? "RightHand" : "LeftHand");
                if (hand != null && hand.Lost) continue;
            }
            Transform b = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (b != null && b.lossyScale.sqrMagnitude > 1e-6f)
                return b;
        }
        return null;
    }

    // ------------------------------------------------------------------ cards
    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    /// <summary>Lying on the table, face up, its top toward <paramref name="top"/>.</summary>
    private static Quaternion FaceUp(Vector3 top) => Quaternion.LookRotation(Vector3.down, Flat(top));

    /// <summary>A card: a front quad with its rank and suit, a back quad. <see cref="PlayingCard.Hidden"/> shows the back both ways.</summary>
    public static GameObject MakeCard(byte code, Transform parent, float scale, bool lit = false)
    {
        EnsureAssets();
        var go = new GameObject(PlayingCard.IsCard(code) ? PlayingCard.FromCode(code).Short : "Card (back)");
        go.transform.SetParent(parent, false);
        bool faceUp = PlayingCard.IsCard(code);
        Face(go.transform, "Front", Quaternion.identity, faceUp ? (lit ? highlight : front) : back, scale);
        Face(go.transform, "Back", Quaternion.Euler(0f, 180f, 0f), back, scale);
        if (faceUp)
        {
            PlayingCard card = PlayingCard.FromCode(code);
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.0008f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = card.RankLabel + "\n" + PlayingCard.SuitSymbol(card.suit);
            tmp.fontSize = 0.32f * scale;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = card.IsRed ? new Color(0.78f, 0.08f, 0.1f) : new Color(0.08f, 0.08f, 0.1f);
            tmp.rectTransform.sizeDelta = new Vector2(CardWidth * scale, CardHeight * scale);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return go;
    }

    private static void Face(Transform parent, string n, Quaternion rot, Material m, float scale)
    {
        var f = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
        f.transform.SetParent(parent, false);
        f.transform.localRotation = rot;
        f.transform.localScale = new Vector3(CardWidth * scale, CardHeight * scale, 1f);
        f.GetComponent<MeshFilter>().sharedMesh = quad;
        var r = f.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void EnsureAssets()
    {
        if (quad == null)
        {
            quad = new Mesh { name = "Card Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
        }
        if (front == null) front = MakeMaterial("Card Front", new Color(0.97f, 0.96f, 0.92f));
        if (back == null) back = MakeMaterial("Card Back", new Color(0.55f, 0.1f, 0.12f));
        if (highlight == null) highlight = MakeMaterial("Card Best", new Color(1f, 0.93f, 0.62f));
    }

    private static Material MakeMaterial(string n, Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = n, color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        front = back = highlight = null;
        quad = null;
    }
}
