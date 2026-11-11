using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RuneMagic
{
    // Concept discovery (design spec 17) of a soul: a knowledge source in a dungeon starts a discovery; the server picks the
    // concept in secret, issues the minigame, validates what the player reports, asks the riddle, evaluates the answer. The
    // owner's client only ever sees a DiscoveryView (no concept, no answer). Runs where the rules run.
    public partial class RuneSoul
    {
        private DiscoverySession discovery;
        private Transform discoveryAnchor;
        private string discoveryResult = "";
        private int discoveryVersion;
        private readonly DiscoveryView discoveryView = new DiscoveryView();
        private DiscoveryView replicatedDiscovery = new DiscoveryView();
        private System.Random discoveryRng = new System.Random();

        /// <summary>A discovery began or ended, or its view changed (any machine).</summary>
        public event Action<RuneSoul> DiscoveryChanged;
        /// <summary>A concept was permanently learned through discovery (where the rules run).</summary>
        public event Action<RuneSoul, string> ConceptLearned;

        /// <summary>Changes whenever <see cref="DiscoveryView"/> does.</summary>
        public int DiscoveryVersion => IsAuthority ? discoveryVersion : replicatedDiscovery.version;

        /// <summary>What the player's UI shows of the discovery in progress (or just ended). Holds no secret.</summary>
        public DiscoveryView DiscoveryView => IsAuthority ? discoveryView : replicatedDiscovery;

        /// <summary>Is a discovery in progress?</summary>
        public bool IsDiscovering => DiscoveryView.Active;

        /// <summary>The discovery log, burned sources and counters of the player (where the rules run; null on a client).</summary>
        public DiscoveryState DiscoveryState => IsAuthority ? state.discovery : null;

        /// <summary>The random generator of discoveries (tests may set a seeded one).</summary>
        public System.Random DiscoveryRandom { get => discoveryRng; set => discoveryRng = value ?? new System.Random(); }

        // ------------------------------------------------------------------ begin
        /// <summary>
        /// Starts a discovery at a source (where the rules run): <paramref name="sourceId"/> names its definition,
        /// <paramref name="dungeonTier"/> the dungeon's tier (<paramref name="tierOverride"/> 0 or more sets the band directly),
        /// <paramref name="fixedConcept"/> forces a concept, <paramref name="anchor"/> is where the source is (the discovery
        /// ends when the player walks away). False, with the reason told to the player, when it cannot start.
        /// </summary>
        public bool BeginDiscovery(string sourceId, int dungeonTier, int tierOverride, string fixedConcept, Transform anchor, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server begins a discovery."; return false; }
            if (!CheckRuneUser(true)) { reason = "Only rune users (Mages) can read what the glyphs say."; return false; }
            if (discovery != null && discovery.Active)
            {
                // The same source again (the window was only hidden): show the discovery in progress once more.
                if (discovery.sourceId == sourceId) { RefreshDiscovery(); return true; }
                reason = "You are already working on another riddle.";
                Say(reason);
                return false;
            }
            CombatEntity e = Entity;
            if (e != null && (e.IsDead || e.IsUnconscious)) { reason = "You cannot do that now."; return false; }
            if (sleep != null && sleep.IsSleeping) { reason = "You cannot do that while asleep."; return false; }
            if (state.soul.collapseLocked) { reason = "Your soul has collapsed: you cannot focus on the glyphs."; Say(reason); return false; }

            var held = new List<string>();
            foreach (RuneInstance r in state.runes)
                if (r != null && !r.IsComposite && !held.Contains(r.runeId)) held.Add(r.runeId); // a composite rune has no concepts to discover
            DiscoveryStart start = DiscoveryFlow.Start(Catalog, state, sourceId, dungeonTier, tierOverride, fixedConcept, held, discoveryRng, Time.time);
            if (!start.ok)
            {
                reason = start.reason;
                Say(reason);
                return false;
            }
            discovery = start.session;
            discoveryAnchor = anchor;
            discoveryResult = "";
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: discovery at {discovery.sourceId}, band {discovery.tier.Name} (concept chosen in secret).", this);
            RefreshDiscovery();
            return true;
        }

        /// <summary>Starts a discovery without a world source (the debug panel, tools): the band is chosen directly.</summary>
        public bool BeginDiscoveryAt(string sourceId, int tierIndex, out string reason) =>
            BeginDiscovery(sourceId, 0, Mathf.Max(0, tierIndex), null, null, out reason);

        // ------------------------------------------------------------------ minigame and answers (where the rules run)
        /// <summary>The minigame's report: the seconds (on the client's clock since it started) at which each glyph was sealed.</summary>
        public MinigameOutcome SubmitDiscoveryMinigame(IList<float> presses)
        {
            if (!IsAuthority || discovery == null) return MinigameOutcome.None;
            MinigameOutcome o = DiscoveryFlow.SubmitMinigame(Catalog, state, discovery, presses, Time.time, DateTime.UtcNow.Ticks, out string message);
            if (o == MinigameOutcome.None) return o;
            FinishDiscoveryStep(message, o == MinigameOutcome.Failed || o == MinigameOutcome.Burned);
            return o;
        }

        /// <summary>The player's answer to the riddle shown (an index into its options).</summary>
        public AnswerOutcome AnswerDiscovery(int option)
        {
            if (!IsAuthority || discovery == null) return AnswerOutcome.None;
            string concept = discovery.conceptId;
            AnswerOutcome o = DiscoveryFlow.SubmitAnswer(Catalog, state, discovery, option, DateTime.UtcNow.Ticks, out string message);
            if (o == AnswerOutcome.None) return o;
            if (o == AnswerOutcome.Learned)
            {
                runesVersion++; // the knowledge list changed
                ConceptLearned?.Invoke(this, concept);
            }
            FinishDiscoveryStep(message, o == AnswerOutcome.Wrong || o == AnswerOutcome.Learned);
            return o;
        }

        /// <summary>The player leaves the riddle or minigame (closes the window). A riddle left unanswered counts as wrong.</summary>
        public void AbandonDiscovery()
        {
            if (!IsAuthority || discovery == null || !discovery.Active) return;
            DiscoveryFlow.Abandon(Catalog, state, discovery, false, DateTime.UtcNow.Ticks, out string message);
            FinishDiscoveryStep(message, true);
        }

        /// <summary>Clears the result of an ended discovery from the player's view (they closed the window).</summary>
        public void DismissDiscovery()
        {
            if (!IsAuthority)
            {
                if (!replicatedDiscovery.Active) { replicatedDiscovery.phase = DiscoveryPhase.None; DiscoveryChanged?.Invoke(this); }
                return;
            }
            if (discovery != null && discovery.Active) return;
            if (discoveryView.phase == DiscoveryPhase.None) return;
            discovery = null;
            discoveryResult = "";
            RefreshDiscovery();
        }

        private void FinishDiscoveryStep(string message, bool ended)
        {
            discoveryResult = message ?? "";
            MarkSaveDirty(true);
            if (ended && discovery != null && !discovery.Active)
                discoveryAnchor = null;
            RefreshDiscovery();
            if (!string.IsNullOrEmpty(message)) Say(message);
        }

        private void RefreshDiscovery()
        {
            if (!IsAuthority) return;
            discoveryVersion++;
            DiscoveryView v = discovery != null
                ? DiscoveryFlow.ViewOf(Catalog, state, discovery, discoveryResult, discoveryVersion)
                : new DiscoveryView { version = discoveryVersion };
            discoveryView.version = v.version; discoveryView.phase = v.phase; discoveryView.sourceName = v.sourceName;
            discoveryView.tierName = v.tierName; discoveryView.tierIndex = v.tierIndex; discoveryView.challenge = v.challenge;
            discoveryView.step = v.step; discoveryView.steps = v.steps; discoveryView.text = v.text;
            discoveryView.options = v.options; discoveryView.attemptsLeft = v.attemptsLeft; discoveryView.result = v.result;
            discoveryView.learnedName = v.learnedName; discoveryView.burned = v.burned;
            DiscoveryChanged?.Invoke(this);
            Changed?.Invoke(this);
        }

        /// <summary>A discovery view from the server (owner's client, network layer).</summary>
        public void ApplyReplicatedDiscovery(DiscoveryView v)
        {
            if (v == null || IsAuthority) return;
            if (v.options == null) v.options = new List<string>();
            replicatedDiscovery = v;
            DiscoveryChanged?.Invoke(this);
            Changed?.Invoke(this);
        }

        // ------------------------------------------------------------------ the server's watch over a discovery
        private void TickDiscovery(RuneBalance b)
        {
            if (discovery == null || !discovery.Active)
                return;
            DiscoveryBalance d = b.discovery;
            CombatEntity e = Entity;
            string interrupted = null;
            if (e != null && (e.IsDead || e.IsUnconscious)) interrupted = "";
            else if (state.soul.collapseLocked) interrupted = "Your soul collapses: the glyphs fade.";
            else if (discoveryAnchor != null && !discoveryAnchor.gameObject.activeInHierarchy) interrupted = "The source is gone.";
            if (interrupted != null)
            {
                // Not the player's mistake: no penalty.
                DiscoveryFlow.Cancel(discovery);
                FinishDiscoveryStep(interrupted, true);
                return;
            }
            bool left = false;
            if (discoveryAnchor != null && d.leaveDistance > 0f && e != null)
            {
                Vector3 delta = e.transform.position - discoveryAnchor.position;
                left = delta.sqrMagnitude > d.leaveDistance * d.leaveDistance;
            }
            bool expired = Time.time - discovery.startedAt > d.sessionSeconds;
            if (left || expired)
            {
                DiscoveryFlow.Abandon(Catalog, state, discovery, expired, DateTime.UtcNow.Ticks, out string message);
                FinishDiscoveryStep(expired ? "Time runs out. " + message : "You step away. " + message, true);
            }
        }

        // ------------------------------------------------------------------ requests (any machine)
        /// <summary>Reports the minigame (offline / host: judged now; client: asks the server).</summary>
        public bool RequestDiscoveryMinigame(IList<float> presses)
        {
            if (presses == null || presses.Count == 0 || presses.Count > RuneTraceMinigame.MaxNodes) return false;
            if (IsAuthority) return SubmitDiscoveryMinigame(presses) != MinigameOutcome.None;
            var sb = new StringBuilder();
            for (int i = 0; i < presses.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(presses[i].ToString("0.###", CultureInfo.InvariantCulture));
            }
            return Send(RuneRequestKind.DiscoveryMinigame, sb.ToString(), 0f, false);
        }

        /// <summary>Answers the riddle (offline / host: judged now; client: asks the server).</summary>
        public bool RequestDiscoveryAnswer(int option)
        {
            if (option < 0) return false;
            if (IsAuthority) return AnswerDiscovery(option) != AnswerOutcome.None;
            return Send(RuneRequestKind.DiscoveryAnswer, "", option, false);
        }

        /// <summary>Leaves the discovery (a riddle left unanswered counts as wrong).</summary>
        public bool RequestDiscoveryAbandon()
        {
            if (IsAuthority) { AbandonDiscovery(); return true; }
            return Send(RuneRequestKind.DiscoveryAbandon, "", 0f, false);
        }

        private void HandleDiscoveryRequest(RuneSoulRequest request)
        {
            switch (request.kind)
            {
                case RuneRequestKind.DiscoveryMinigame:
                    var presses = new List<float>();
                    string[] parts = (request.text ?? "").Split(';');
                    if (parts.Length > RuneTraceMinigame.MaxNodes) break;
                    foreach (string p in parts)
                    {
                        if (!float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) { presses.Clear(); break; }
                        presses.Add(t);
                    }
                    SubmitDiscoveryMinigame(presses); // an unreadable report is judged as an empty one: a failed try
                    break;
                case RuneRequestKind.DiscoveryAnswer:
                    AnswerDiscovery(Mathf.RoundToInt(request.amount));
                    break;
                case RuneRequestKind.DiscoveryAbandon:
                    AbandonDiscovery();
                    break;
            }
        }

        /// <summary>Forgets burned sources, seen riddles, attempts and the log (tools, debugging); concepts already learned are kept.</summary>
        public void ResetDiscovery()
        {
            if (!IsAuthority) return;
            ClearDiscovery();
            state.discovery = new DiscoveryState();
            MarkSaveDirty(true);
            RefreshDiscovery();
        }

        private void ClearDiscovery()
        {
            if (discovery != null && discovery.Active) DiscoveryFlow.Cancel(discovery);
            discovery = null;
            discoveryAnchor = null;
        }
    }
}
