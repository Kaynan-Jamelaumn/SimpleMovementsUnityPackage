#if FISHNET
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a player's money (<see cref="CurrencyWallet"/>): the server's wallet is the truth. Every balance of a
    /// number currency is replicated to the player's owner only (other players never learn it), keyed by the currency's
    /// <see cref="CurrencyDefinition.NetworkKey"/>, and shown there with <see cref="CurrencyWallet.ApplyReplicatedBalance"/>;
    /// the owner's wallet refuses changes of its own (merchants and trades ask the server). Coin-item currencies are
    /// items: they follow the replicated inventory.
    /// <para>Put it on the player prefab root next to the NetworkObject. Offline it does nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetWallet : NetworkBehaviour
    {
        [Tooltip("The wallet to network. Empty = the player's CurrencyWallet (added to the player on the server when missing).")]
        [SerializeField] private CurrencyWallet wallet;

        private readonly SyncDictionary<string, int> balances = new SyncDictionary<string, int>(new SyncTypeSettings(ReadPermission.OwnerOnly));

        private CurrencyWallet hooked;
        private readonly HashSet<string> warnedUnknown = new HashSet<string>();

        /// <summary>The wallet this component networks.</summary>
        public CurrencyWallet Wallet => wallet;

        private void Awake()
        {
            if (wallet == null)
                wallet = GetComponentInChildren<CurrencyWallet>(true);
        }

        private CurrencyWallet EnsureWallet()
        {
            if (wallet == null)
                wallet = CurrencyWallet.For(this, true);
            return wallet;
        }

        // ------------------------------------------------------------------ server
        public override void OnStartServer()
        {
            base.OnStartServer();
            CurrencyWallet w = EnsureWallet();
            if (w == null)
                return;
            hooked = w;
            w.BalanceChanged += OnWalletChanged;
            PushAll();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Unhook();
        }

        private void OnDestroy()
        {
            Unhook();
            balances.OnChange -= OnBalancesChanged;
        }

        private void Unhook()
        {
            if (hooked != null)
                hooked.BalanceChanged -= OnWalletChanged;
            hooked = null;
        }

        /// <summary>Writes every number currency the wallet knows (the default one included).</summary>
        private void PushAll()
        {
            Write(hooked.DefaultCurrency, hooked.GetBalance(hooked.DefaultCurrency));
            IReadOnlyList<CurrencyBalance> list = hooked.Balances;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].currency != null)
                    Write(list[i].currency, hooked.GetBalance(list[i].currency));
        }

        private void OnWalletChanged(CurrencyDefinition currency, int before, int after) => Write(currency, after);

        private void Write(CurrencyDefinition currency, int amount)
        {
            if (!IsServerInitialized || currency == null || currency.IsItemBacked)
                return;
            string key = currency.NetworkKey;
            if (string.IsNullOrEmpty(key))
                return;
            if (!balances.TryGetValue(key, out int current) || current != amount)
                balances[key] = amount;
        }

        // ------------------------------------------------------------------ owner client
        public override void OnStartClient()
        {
            base.OnStartClient();
            balances.OnChange += OnBalancesChanged;
            if (!IsServerInitialized && IsOwner)
                foreach (KeyValuePair<string, int> kv in balances)
                    Apply(kv.Key, kv.Value);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            balances.OnChange -= OnBalancesChanged;
        }

        private void OnBalancesChanged(SyncDictionaryOperation op, string key, int value, bool asServer)
        {
            if (asServer || IsServerInitialized)
                return;
            if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set)
                Apply(key, value);
        }

        private void Apply(string key, int value)
        {
            CurrencyDefinition currency = CurrencyDefinition.FindByKey(key);
            if (currency == null)
            {
                if (warnedUnknown.Add(key ?? ""))
                    Debug.LogWarning($"[Net] The server sent a balance of currency '{key}', which is not loaded here (keep currency asset names the same on every build).", this);
                return;
            }
            CurrencyWallet w = EnsureWallet();
            if (w != null)
                w.ApplyReplicatedBalance(currency, value);
        }
    }
}
#endif
