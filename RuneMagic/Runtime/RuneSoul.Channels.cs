using UnityEngine;

namespace RuneMagic
{
    // Meditation and healing rituals: channelled actions of the soul (design spec 15.1). Both run where the rules run; the
    // owner's client asks for meditation through the network link, and rituals start through the interaction system.
    public partial class RuneSoul
    {
        private SoulChannel channel;
        private string channelName = "";
        private float channelStart;
        private Vector3 channelOrigin;
        private SoulRitualSite ritual;
        private float ritualSeconds;
        private float dealtAtStart;
        private bool channelLockHeld;
        private bool replicatedChannelLock;
        private readonly object channelKey = new object();

        private const string NotRuneUser = "Only rune users can work on their soul.";

        /// <summary>The ritual site of the ritual being performed (null when none).</summary>
        public SoulRitualSite CurrentRitual => channel == SoulChannel.Ritual ? ritual : null;

        /// <summary>Seconds the current meditation or ritual has lasted.</summary>
        public float ChannelSeconds => channel != SoulChannel.None ? Time.time - channelStart : 0f;

        // ------------------------------------------------------------------ meditation
        /// <summary>
        /// Can meditation start now? Not in combat (unless allowed), not asleep, downed or incapacitated, and only with
        /// something to recover (unless allowed). On a client: a quick check of the server's view (the server decides).
        /// </summary>
        public bool CanMeditate(out string reason)
        {
            reason = "";
            if (!IsAuthority)
            {
                if (!view.runeUser) { reason = NotRuneUser; return false; }
                if (view.IsChannelling) { reason = "You are already focused on your soul."; return false; }
                return true;
            }
            if (!CanChannel(out reason))
                return false;
            SoulRecoveryBalance r = Balance.soulRecovery;
            CombatEntity e = Entity;
            if (r != null && !r.meditationAllowedInCombat && e != null && CombatActivity.InCombat(e, r.combatWindowSeconds))
            {
                reason = "You cannot meditate in combat.";
                return false;
            }
            if (r != null && !r.meditationAllowedWhenFull)
            {
                float ceiling = SoulRules.Ceiling(state.soul, ComputeMax());
                if (state.soul.integrity >= ceiling - 0.01f)
                {
                    reason = state.soul.deepDamage > 0.01f
                        ? "Meditation cannot reach the deep damage in your soul: it needs specialized healing."
                        : "Your soul is at peace: there is nothing to recover.";
                    return false;
                }
            }
            return true;
        }

        /// <summary>Starts meditating (where the rules run). False with the reason told to the player when it cannot.</summary>
        public bool StartMeditation(out string reason)
        {
            if (!IsAuthority)
            {
                reason = "";
                return RequestMeditation(true);
            }
            if (!CanMeditate(out reason))
            {
                Say(reason);
                return false;
            }
            SoulRecoveryBalance r = Balance.soulRecovery;
            BeginChannel(SoulChannel.Meditation, "Meditation", null, 0f, r != null && r.meditationLocksMovement);
            Say("You close your eyes and turn inward.");
            return true;
        }

        /// <summary>Stops meditating, or breaks off the ritual being performed (where the rules run).</summary>
        public void StopMeditation()
        {
            if (!IsAuthority)
            {
                RequestMeditation(false);
                return;
            }
            if (channel == SoulChannel.Meditation)
                EndChannel("You end your meditation.");
            else if (channel == SoulChannel.Ritual)
                EndChannel("You break off the ritual.");
        }

        /// <summary>
        /// The player's own input (the Meditate key): offline and on the host this starts or stops meditation; on a network
        /// client it asks the server. Returns false when nothing could be done or sent.
        /// </summary>
        public bool RequestMeditation(bool on)
        {
            if (!IsAuthority)
            {
                IRuneMagicNet net = Net;
                return net != null && net.Send(new RuneSoulRequest { kind = RuneRequestKind.Meditate, seq = ++requestSeq, flag = on });
            }
            if (on)
                return StartMeditation(out _);
            StopMeditation();
            return true;
        }

        /// <summary>Starts meditation, or stops meditation or the ritual, whichever applies.</summary>
        public bool ToggleMeditation() => RequestMeditation(!view.IsChannelling);

        /// <summary>A request from the owning client (network layer, on the server). Everything is checked again.</summary>
        public void ServerHandleRequest(RuneSoulRequest request)
        {
            if (!IsAuthority)
                return;
            switch (request.kind)
            {
                case RuneRequestKind.Meditate:
                    if (request.flag) StartMeditation(out _);
                    else StopMeditation();
                    break;
                default:
                    HandleRuneRequest(request);
                    break;
            }
        }

        // ------------------------------------------------------------------ rituals
        /// <summary>
        /// Begins the healing ritual of <paramref name="site"/> (called by the site, where the rules run). The site checks
        /// its own requirements (materials, cooldown, uses); the soul checks that it can focus.
        /// </summary>
        public bool BeginRitual(SoulRitualSite site, out string reason)
        {
            reason = "";
            if (site == null || !IsAuthority)
                return false;
            if (!CanChannel(out reason) || !site.CanBegin(this, out reason))
            {
                Say(reason);
                return false;
            }
            BeginChannel(SoulChannel.Ritual, site.DisplayName, site, site.RitualSeconds, site.LocksMovement);
            Say($"You begin the ritual of {site.DisplayName}.");
            if (site.RitualSeconds <= 0f)
                CompleteRitual();
            return true;
        }

        private void CompleteRitual()
        {
            SoulRitualSite site = ritual;
            if (site == null)
            {
                EndChannel("The ritual site is gone.");
                return;
            }
            bool done = site.Complete(this, out string result);
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: ritual of {site.DisplayName} {(done ? "completed" : "failed")}: {result}", this);
            EndChannel(result);
        }

        // ------------------------------------------------------------------ channel common
        private bool CanChannel(out string reason)
        {
            reason = "";
            if (!CheckRuneUser(true)) { reason = NotRuneUser; return false; }
            CombatEntity e = Entity;
            if (e != null && (e.IsDead || e.IsUnconscious)) { reason = "You cannot do that now."; return false; }
            if (channel == SoulChannel.Meditation) { reason = "You are already meditating."; return false; }
            if (channel == SoulChannel.Ritual) { reason = "You are performing a ritual."; return false; }
            if (sleep != null && sleep.IsSleeping) { reason = "You cannot do that while asleep."; return false; }
            if (collapseHeld) { reason = "Your soul is still reeling from the collapse."; return false; }
            if (availability != null && availability.IsIncapacitated) { reason = "You cannot do that now."; return false; }
            return true;
        }

        private void BeginChannel(SoulChannel kind, string displayName, SoulRitualSite site, float seconds, bool lockMovement)
        {
            CombatEntity e = Entity;
            channel = kind;
            channelName = displayName ?? "";
            channelStart = Time.time;
            channelOrigin = e != null ? e.transform.position : transform.position;
            ritual = site;
            ritualSeconds = Mathf.Max(0f, seconds);
            dealtAtStart = e != null ? e.LastDealtDamageTime : 0f;
            if (lockMovement && availability != null)
            {
                availability.SetIncapacitated(channelKey, true);
                channelLockHeld = true;
            }
            ChannelChanged?.Invoke(this, kind);
            Refresh();
        }

        /// <summary>Ends meditation or the ritual (where the rules run), telling the player <paramref name="why"/>.</summary>
        public void EndChannel(string why)
        {
            if (channel == SoulChannel.None)
                return;
            channel = SoulChannel.None;
            channelName = "";
            ritual = null;
            ritualSeconds = 0f;
            ReleaseChannelLock();
            ChannelChanged?.Invoke(this, SoulChannel.None);
            if (!string.IsNullOrEmpty(why)) Say(why);
            else Refresh();
        }

        private void ReleaseChannelLock()
        {
            if (channelLockHeld && availability != null)
                availability.SetIncapacitated(channelKey, false);
            channelLockHeld = false;
        }

        // The owner's own copy holds still while the server holds the character still (movement is predicted there).
        private void ApplyReplicatedChannelLock()
        {
            SoulRecoveryBalance r = Balance.soulRecovery;
            bool locked = view.channel == SoulChannel.Meditation && r != null && r.meditationLocksMovement;
            if (locked == replicatedChannelLock || availability == null)
                return;
            replicatedChannelLock = locked;
            availability.SetIncapacitated(channelKey, locked);
        }

        private void TickChannel(RuneBalance b)
        {
            if (channel == SoulChannel.None)
                return;
            SoulRecoveryBalance r = b.soulRecovery;
            CombatEntity e = Entity;
            if (e != null && (e.IsDead || e.IsUnconscious))
            {
                EndChannel("");
                return;
            }
            if (sleep != null && sleep.IsSleeping)
            {
                EndChannel("");
                return;
            }
            Vector3 p = e != null ? e.transform.position : transform.position;
            Vector3 d = p - channelOrigin;
            d.y = 0f;
            float tolerance = r != null ? r.channelMoveTolerance : 0.4f;
            if (d.sqrMagnitude > tolerance * tolerance)
            {
                EndChannel(channel == SoulChannel.Meditation ? "You moved: your meditation is broken." : "You moved: the ritual is broken.");
                return;
            }
            if (r != null && r.channelBrokenByAttacking && e != null && e.LastDealtDamageTime > dealtAtStart + 0.0001f)
            {
                EndChannel("You attacked: your focus is broken.");
                return;
            }
            if (channel == SoulChannel.Ritual)
            {
                if (ritual == null || !ritual.isActiveAndEnabled)
                {
                    EndChannel("The ritual site is gone.");
                    return;
                }
                if (!ritual.InRange(p))
                {
                    EndChannel("You left the ritual site: the ritual is broken.");
                    return;
                }
                if (Time.time - channelStart >= ritualSeconds)
                    CompleteRitual();
            }
        }

        private void OnEntityDamaged(DamageInfo info)
        {
            SpellingDamaged(info);
            if (channel == SoulChannel.None || !IsAuthority || !(info.amount > 0f))
                return;
            SoulRecoveryBalance r = Balance.soulRecovery;
            if (r == null || r.channelBrokenByDamage)
                EndChannel("You were hurt: your focus is broken.");
        }

        private void OnEntityControlled(ControlType type, float duration, CombatEntity source)
        {
            SpellingControlled();
            if (channel == SoulChannel.None || !IsAuthority)
                return;
            SoulRecoveryBalance r = Balance.soulRecovery;
            if (r == null || r.channelBrokenByControl)
                EndChannel("Your focus is broken.");
        }
    }
}
