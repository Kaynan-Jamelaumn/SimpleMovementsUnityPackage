using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// How players learn what harvested parts are good for, by using them where the rules run (single player, the server):
/// selling one to a merchant tells them its price and that it sells; putting one in an alchemy vessel, that it is an
/// alchemy ingredient; eating one (<see cref="HarvestedFoodSO"/>), that it is food. Also keeps the world's
/// <see cref="HarvestDiscoveries"/> on disk (<c>persistentDataPath/Harvesting/discoveries.json</c>): loaded at start,
/// saved when one is made. A network client follows the server's instead (sent with its harvesting skills).
/// </summary>
public static class HarvestLearning
{
    private static bool hooked;
    private static bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Unhook();
        HarvestDiscoveries.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        if (hooked)
            return;
        hooked = true;
        MerchantTransactions.Completed += OnTransaction;
        AlchemyStation.IngredientAdded += OnIngredient;
        HarvestDiscoveries.Changed += SaveDiscoveries;
        LoadDiscoveries();
    }

    private static void Unhook()
    {
        if (!hooked)
            return;
        hooked = false;
        MerchantTransactions.Completed -= OnTransaction;
        AlchemyStation.IngredientAdded -= OnIngredient;
        HarvestDiscoveries.Changed -= SaveDiscoveries;
    }

    private static void OnTransaction(MerchantTransactionRequest request, MerchantTransactionResult result)
    {
        if (request.Type != MerchantTransactionType.Sell || request.Player == null || !(request.Item is IHarvestedItem))
            return;
        HarvestKnowledge k = HarvestKnowledge.For(request.Player.transform);
        if (k == null)
            return;
        k.Appraised(request.Item);
        k.LearnUse(request.Item, HarvestUse.Trade);
    }

    private static void OnIngredient(CombatEntity player, ItemSO item, int count)
    {
        if (player == null || !(item is IHarvestedItem))
            return;
        HarvestKnowledge.For(player)?.LearnUse(item, HarvestUse.Alchemy);
    }

    // ------------------------------------------------------------------ the world's discoveries on disk
    /// <summary>Where the world's discoveries are saved.</summary>
    public static string DiscoveriesPath => Path.Combine(Application.persistentDataPath, "Harvesting", "discoveries.json");

    [Serializable]
    private class Saved
    {
        public List<string> keys = new List<string>();
    }

    private static void LoadDiscoveries()
    {
        if (HarvestKnowledge.FollowsServer)
            return;
        try
        {
            string path = DiscoveriesPath;
            if (!File.Exists(path))
                return;
            Saved s = JsonUtility.FromJson<Saved>(File.ReadAllText(path));
            loading = true;
            HarvestDiscoveries.SetAll(s != null ? s.keys : null);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Harvesting] Could not read the discoveries: {e.Message}");
        }
        finally
        {
            loading = false;
        }
    }

    private static void SaveDiscoveries()
    {
        if (loading || HarvestKnowledge.FollowsServer)
            return;
        try
        {
            string path = DiscoveriesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new Saved { keys = new List<string>(HarvestDiscoveries.Keys) }));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Harvesting] Could not save the discoveries: {e.Message}");
        }
    }
}
