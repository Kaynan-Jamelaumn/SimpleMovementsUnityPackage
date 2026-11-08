#if FISHNET
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    // The character made on the creation screen: race, class, traits, height and appearance.
    public partial class NetPlayer
    {
        [Tooltip("Accept the character the owner sends only once per spawn (a client cannot rebuild itself mid-game).")]
        public bool lockCharacterAfterFirst = true;

        [Tooltip("A character plays in one session at a time. When it logs in again while another session plays it, that " +
                 "session is closed if nothing came from it for this many real seconds (a crash and a quick rejoin); " +
                 "otherwise the new login waits until that session ends.")]
        [Range(2f, 60f)] public float takeOverAfterSeconds = 6f;

        /// <summary>The player's character as the server accepted it (encoded <see cref="CharacterSave"/>), the same on every machine.</summary>
        public readonly SyncVar<string> CharacterCode = new SyncVar<string>("");

        /// <summary>The character shown on this machine (null until it arrived).</summary>
        public CharacterSave Character { get; private set; }

        /// <summary>Server: is <see cref="Character"/> the one the server accepted for this session (its id makes the player's key)?</summary>
        private bool characterAccepted;
        private string appliedCode;
        private Coroutine applyRoutine;

        // Server: a character the owner sent while another session plays it. It is theirs as soon as that session is gone.
        private string waitingCode;
        private Coroutine waitingRoutine;
        private float nextCharacterAt;
        // Server: since when this player's connection has been leaving while it still holds its character (-1: it is not).
        private float leavingSince = -1f;

        /// <summary>Real seconds a player whose connection is leaving may keep its character before it is let go anyway.</summary>
        private const float LeavingGraceSeconds = 5f;

        /// <summary>Owner: sends this machine's character (<see cref="CharacterSession.Current"/>) to the server.</summary>
        private void SendCharacter()
        {
            CharacterSave save = CharacterSession.Current;
            if (save == null)
                return; // no start screens (e.g. the City scene played directly): the prefab's own class and race stay
            string code = save.Encode();
            if (code.Length > CharacterSave.MaxEncodedLength)
            {
                Debug.LogWarning("[Net] The character is too large to send.", this);
                return;
            }
            ServerSetCharacter(code);
        }

        /// <summary>The owner's character: checked against the creation rules, applied with the rules here, then synced.</summary>
        [ServerRpc]
        private void ServerSetCharacter(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length > CharacterSave.MaxEncodedLength)
                return;
            if (characterAccepted && lockCharacterAfterFirst)
                return;
            // A client sends its character once: a flood is dropped.
            float now = Time.unscaledTime;
            if (now < nextCharacterAt)
                return;
            nextCharacterAt = now + 0.5f;
            TrySetCharacter(code, true);
        }

        /// <summary>
        /// Server: accepts a character, or keeps it waiting while another session plays it. One live session per character:
        /// a second window (or a copied character file) playing the same character would share its lasting key - its money,
        /// its house - so it waits until that session has ended. A session that stopped answering (its game crashed and the
        /// player came back before the server noticed) is closed for the new login, which then gets the character.
        /// </summary>
        private void TrySetCharacter(string code, bool fromOwner)
        {
            CharacterSave wanted = CharacterSave.Decode(code);
            if (wanted == null)
            {
                StopWaiting();
                return;
            }
            NetPlayer holder = HolderOf(wanted.id);
            if (holder != null)
            {
                holder.EndIfStale(this);
                holder = HolderOf(wanted.id);
            }
            if (holder != null)
            {
                bool told = waitingCode != null;
                waitingCode = code;
                if (fromOwner && !told)
                {
                    Debug.LogWarning($"[Net] Client {ClientIdOf(Owner)} sent a character client {ClientIdOf(holder.Owner)} plays: it waits until that session ends.", this);
                    if (Entity != null)
                        HubWorld.HubWorldMessages.Send(Entity, "That character is already in the world (in another window?). You get it as soon as that session ends; until then you play without it.", HubWorld.HubMessageKind.Warning);
                }
                if (waitingRoutine == null && isActiveAndEnabled)
                    waitingRoutine = StartCoroutine(WaitForCharacter());
                return;
            }
            bool waited = waitingCode != null;
            StopWaiting();
            Accept(wanted);
            if (waited && Entity != null)
                HubWorld.HubWorldMessages.Send(Entity, "The other session with your character has ended: you play it now.");
        }

        /// <summary>Server: looks twice a second whether the session holding the waiting character has ended (or stopped answering).</summary>
        private IEnumerator WaitForCharacter()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (waitingCode != null && !characterAccepted && IsServerInitialized)
            {
                yield return wait;
                if (waitingCode != null && !characterAccepted && IsServerInitialized)
                    TrySetCharacter(waitingCode, false);
            }
            waitingRoutine = null;
        }

        private void StopWaiting()
        {
            waitingCode = null;
            if (waitingRoutine != null)
                StopCoroutine(waitingRoutine);
            waitingRoutine = null;
        }

        private void Accept(CharacterSave wanted)
        {
            var problems = new List<string>();
            CharacterSave clean = CharacterBuild.Sanitize(wanted, CharacterCatalog.Instance, problems);
            if (problems.Count > 0)
                Debug.LogWarning($"[Net] Character of client {ClientIdOf(Owner)} adjusted: {string.Join(" ", problems)}", this);
            characterAccepted = true;
            leavingSince = -1f;
            if (!string.IsNullOrWhiteSpace(clean.characterName))
                DisplayName.Value = CleanName(clean.characterName, Owner != null ? Owner.ClientId : 0);
            // Every machine sees the character - without its id: the id is the player's lasting key (PlayerKeys), known
            // only to its owner and the server, so no other client can claim to be them.
            CharacterSave shown = clean.Clone();
            shown.id = "";
            string synced = shown.Encode();
            CharacterCode.Value = synced;
            ApplyCharacter(clean, synced, true);
        }

        /// <summary>Server: the other player whose accepted character has this id (null: none, or no id).</summary>
        private NetPlayer HolderOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < all.Count; i++)
            {
                NetPlayer other = all[i];
                if (other != null && other != this && other.characterAccepted && other.Character != null && other.Character.id == id)
                    return other;
            }
            return null;
        }

        /// <summary>
        /// Server: this player holds a character another login (<paramref name="claimant"/>) wants. A session that is over
        /// lets it go: its connection is gone (the player object stayed behind), it has been leaving for too long, or it
        /// sent nothing for <see cref="takeOverAfterSeconds"/> (a crash the server has not noticed yet: it is closed, and
        /// its player leaves the world a moment later). A live session keeps it - the host's own player always.
        /// </summary>
        private void EndIfStale(NetPlayer claimant)
        {
            NetworkConnection c = Owner;
            if (c == null || !c.IsValid)
            {
                Debug.LogWarning($"[Net] A player object stayed in the world after its client left: its character goes to client {ClientIdOf(claimant.Owner)}.", this);
                ReleaseCharacter();
                return;
            }
            if (!c.IsActive)
            {
                // It is leaving: its player is removed in a moment, which frees the character.
                float now = Time.unscaledTime;
                if (leavingSince < 0f)
                    leavingSince = now;
                else if (now - leavingSince > LeavingGraceSeconds)
                    ReleaseCharacter();
                return;
            }
            if (c.IsLocalClient)
                return;
            double silent = SecondsSilent(c);
            if (silent < takeOverAfterSeconds)
                return;
            Debug.LogWarning($"[Net] Client {ClientIdOf(claimant.Owner)} logged in with the character of client {c.ClientId}, which has sent nothing for {silent:0} s: that session is closed.", this);
            leavingSince = Time.unscaledTime;
            c.Disconnect(true);
        }

        /// <summary>Server: real seconds since the last message from a client (0 when not known yet).</summary>
        private double SecondsSilent(NetworkConnection c)
        {
            TimeManager tm = TimeManager;
            if (c == null || tm == null)
                return 0.0;
            uint last = c.PacketTick.LocalTick;
            uint now = tm.LocalTick;
            return last == 0 || now <= last ? 0.0 : tm.TicksToTime(now - last);
        }

        /// <summary>
        /// Server: this player object, still in the world, no longer plays its character (its session is over, and another
        /// login wants the character). The character's id is forgotten here - on what the player shows too - so no key can
        /// be made from this object again, and its wallet's balances are kept under the key now and no longer written from it.
        /// </summary>
        private void ReleaseCharacter()
        {
            StopWaiting();
            leavingSince = -1f;
            if (applyRoutine != null)
                StopCoroutine(applyRoutine);
            applyRoutine = null;
            CharacterSave released = characterAccepted ? Character : null;
            characterAccepted = false;
            if (released != null && !string.IsNullOrEmpty(released.id))
            {
                string id = released.id;
                foreach (AppliedCharacter marker in GetComponentsInChildren<AppliedCharacter>(true))
                    if (marker != null && marker.Save != null && marker.Save.id == id)
                        marker.Save.id = "";
                released.id = "";
                CurrencyWallet wallet = Entity != null ? CurrencyWallet.For(Entity, false) : null;
                if (wallet != null)
                    WalletSaves.Untrack(wallet);
            }
            // Anyone waiting for this character gets it at their next look.
        }

        /// <summary>Server: a spawn starts with nothing of an earlier one (no character, nothing waiting).</summary>
        private void ForgetCharacter()
        {
            StopWaiting();
            nextCharacterAt = 0f;
            if (applyRoutine != null)
                StopCoroutine(applyRoutine);
            applyRoutine = null;
            characterAccepted = false;
            leavingSince = -1f;
            Character = null;
            appliedCode = null;
        }

        private static int ClientIdOf(NetworkConnection c) => c != null ? c.ClientId : -1;

        /// <summary>
        /// Server: a player's lasting key, from the character the server accepted for them. A network player whose
        /// character was not accepted (none sent, still waiting, or an id too weak to be a key) has none: "" - final, so
        /// nothing else (the character picked on a host's start screens) can make one. Null for anything that is not a
        /// network player.
        /// </summary>
        private static string ResolveKey(CombatEntity e)
        {
            if (e == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                NetPlayer p = all[i];
                if (p != null && p.Entity == e)
                    return p.characterAccepted && p.Character != null && CharacterSave.IsStrongId(p.Character.id) ? PlayerKeys.FromId(p.Character.id) : "";
            }
            return null;
        }

        private void OnCharacterChanged(string prev, string next, bool asServer)
        {
            if (asServer || IsServerInitialized)
                return; // the server (and the host) applied it with the rules already
            ShowSyncedCharacter();
        }

        /// <summary>A client shows the character the server synced (class, race, height, look; traits come mirrored).</summary>
        private void ShowSyncedCharacter()
        {
            string code = CharacterCode.Value;
            if (string.IsNullOrEmpty(code) || code == appliedCode)
                return;
            CharacterSave save = CharacterSave.Decode(code);
            if (save != null)
                ApplyCharacter(save, code, false);
        }

        private void ApplyCharacter(CharacterSave save, string code, bool runsRules)
        {
            Character = save;
            appliedCode = code;
            if (applyRoutine != null)
                StopCoroutine(applyRoutine);
            if (isActiveAndEnabled)
                applyRoutine = StartCoroutine(ApplyWhenReady(save, runsRules));
            else
                CharacterProfileApplier.Apply(gameObject, save, runsRules);
        }

        // The player's Start must have applied its own class first, or the creation's class would be applied twice.
        private IEnumerator ApplyWhenReady(CharacterSave save, bool runsRules)
        {
            PlayerStatusController status = GetComponentInChildren<PlayerStatusController>(true);
            float giveUp = Time.unscaledTime + 10f;
            while (status != null && !status.IsInitialized && Time.unscaledTime < giveUp)
                yield return null;
            applyRoutine = null;
            CharacterProfileApplier.Apply(gameObject, save, runsRules);
        }
    }
}
#endif
