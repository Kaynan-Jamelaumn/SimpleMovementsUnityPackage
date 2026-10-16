#if FISHNET
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>The player's character as the server accepted it (encoded <see cref="CharacterSave"/>), the same on every machine.</summary>
        public readonly SyncVar<string> CharacterCode = new SyncVar<string>("");

        /// <summary>The character shown on this machine (null until it arrived).</summary>
        public CharacterSave Character { get; private set; }

        private bool characterAccepted;
        private string appliedCode;
        private Coroutine applyRoutine;

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
            CharacterSave wanted = CharacterSave.Decode(code);
            if (wanted == null)
                return;
            var problems = new List<string>();
            CharacterSave clean = CharacterBuild.Sanitize(wanted, CharacterCatalog.Instance, problems);
            if (problems.Count > 0)
                Debug.LogWarning($"[Net] Character of client {(Owner != null ? Owner.ClientId : -1)} adjusted: {string.Join(" ", problems)}", this);
            characterAccepted = true;
            if (!string.IsNullOrWhiteSpace(clean.characterName))
                DisplayName.Value = CleanName(clean.characterName, Owner != null ? Owner.ClientId : 0);
            string synced = clean.Encode();
            CharacterCode.Value = synced;
            ApplyCharacter(clean, synced, true);
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
