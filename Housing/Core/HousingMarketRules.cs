using System;
using UnityEngine;

/// <summary>
/// The rental market of houses. Every <see cref="tickHours"/> the market clears: every house that is free, and every
/// lease that ends then and is renewed, goes to the best standing order for it - sealed, binding, paid in advance into
/// escrow - each order winning at most one house, and each winner paying what they offered. A sitting tenant keeps the
/// house unless someone offers more than their protection (1.1 times it), and pays a small premium for that protection;
/// nobody's losing offer ever changes what anyone pays. Each house's asking rent follows its own demand, and only
/// offers that are binding move it: it rises (towards that offer, never past it) while someone besides its tenant offers
/// more than it asks, asks more than its base rent only as far as an offer stands behind it, and falls while it stands
/// empty. The offer a tenant's rent rests on is held to take the house if they leave.
/// </summary>
[Serializable]
public class HousingMarketRules
{
    [Header("Clearing")]
    [Tooltip("Lease-clock hours between two clearings of the market (lease ends fall on clearings).")]
    [Range(1f, 48f)] public float tickHours = 6f;
    [Tooltip("Lease-clock hours a newly offered house waits before its first clearing (everyone sees it first: no race at launch).")]
    [Range(0f, 168f)] public float firstClearingDelayHours = 24f;
    [Tooltip("Lease-clock hours before a lease ends that its renewal is fixed (rent, protection, the money set aside) and the tenant told.")]
    [Range(1f, 168f)] public float renewalNoticeHours = 24f;

    [Header("Orders")]
    [Tooltip("Houses one order may name (it wins at most one of them).")]
    [Range(1, 16)] public int maxOrderItems = 8;
    [Tooltip("An offer above this many times a house's asking rent asks the player to confirm it.")]
    [Min(1f)] public float confirmAboveAsking = 5f;
    [Tooltip("No offer may be above this many times a house's base rent (a guard against typing errors).")]
    [Min(2f)] public float maxOfferBase = 50f;
    [Tooltip("Online hours a player needs (a network session only) before they may place an order: a character made a minute ago cannot rent a house.")]
    [Range(0f, 100f)] public float minOnlineHours = 3f;

    [Header("Sitting tenants")]
    [Tooltip("A tenant's protection by default, times the renewal rent: an offer must beat 1.1 times it to take the house.")]
    [Range(1f, 5f)] public float defaultProtection = 2f;
    [Tooltip("The highest protection a tenant may choose, times the renewal rent.")]
    [Range(1f, 10f)] public float maxProtection = 5f;
    [Tooltip("An offer must be more than this share above the tenant's protection to take the house (ties keep the tenant).")]
    [Range(0f, 1f)] public float protectionMargin = 0.1f;
    [Tooltip("The premium for protection: this share of (protection - rent) is paid on top of the rent.")]
    [Range(0f, 0.5f)] public float protectionPremium = 0.05f;
    [Tooltip("How fast a sitting tenant's rent may rise towards the market, per day (0.015: about +11 % a week). It never rises without the market.")]
    [Range(0f, 0.2f)] public float maxRiseRatePerDay = 0.015f;
    [Tooltip("Most terms a rent account may hold in advance (it is for rent, not a vault).")]
    [Range(1, 52)] public int maxPrepaidTerms = 12;
    [Tooltip("Lease days a tenant may stay away (counted only while the game or server runs) before their lease no longer renews - the money stays in their rent account (0: always renews while paid).")]
    [Range(0f, 365f)] public float abandonAfterDays = 21f;

    [Header("Asking rents")]
    [Tooltip("The lowest and highest asking rent, times the base rent.")]
    [Range(0.05f, 1f)] public float minLevel = 0.25f;
    [Range(1f, 20f)] public float maxLevel = 4f;
    [Tooltip("How much a free house left unlet at a clearing asks less (0.03: 3 % a clearing).")]
    [Range(0f, 0.2f)] public float unletStep = 0.03f;
    [Tooltip("How much a house someone besides its tenant has a binding offer for, above its asking rent, asks more at each clearing " +
             "(0.01: 1 %) - towards that offer, never beyond it. What anyone offered never moves the price past what someone else offered. " +
             "A premium over the base rent lasts only while an offer stands behind it: when that offer goes, so does the premium.")]
    [Range(0f, 0.1f)] public float riseStep = 0.01f;

    [Header("Limits")]
    [Tooltip("Houses a player may rent at once (one more may be won while moving: a lease that ends at that clearing is then given up, " +
             "else the old lease ends at its term). Each house they rent renews on its own.")]
    [Range(1, 10)] public int maxHouses = 1;
}

/// <summary>
/// A sitting tenant's protection in numbers - the books' sums, shared by the windows (so what a player is shown is what
/// the market does). Rents are per day in thousandths of the currency.
/// </summary>
public static class HousingTenure
{
    /// <summary>The protection per day a tenant holds: <paramref name="protection"/> times the renewal rent (rounded up).</summary>
    public static long ProtectionMilli(long renewRateMilli, float protection) =>
        (long)Math.Ceiling(Math.Max(0L, renewRateMilli) * (double)Math.Max(1f, protection));

    /// <summary>What an offer per day must be MORE than to take the house from its tenant (protection x (1 + margin), rounded down).</summary>
    public static long BeatMilli(long protectionMilli, float margin) => (long)Math.Floor(protectionMilli * (1.0 + Math.Max(0f, margin)));

    /// <summary>The premium per day the tenant pays for their protection: a share of (protection - rent).</summary>
    public static double PremiumMilli(long renewRateMilli, long protectionMilli, float premiumShare) =>
        Math.Max(0.0, (double)premiumShare) * Math.Max(0L, protectionMilli - renewRateMilli);

    /// <summary>What renewing for <paramref name="days"/> costs in all: (rent + premium) x days, rounded up to whole currency.</summary>
    public static long RenewTotal(long renewRateMilli, long protectionMilli, int days, float premiumShare) =>
        (long)Math.Ceiling((renewRateMilli + PremiumMilli(renewRateMilli, protectionMilli, premiumShare)) * Math.Max(0, days) / 1000.0 - 1e-9);
}
