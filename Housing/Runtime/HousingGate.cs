using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A place the rules know: a property, or a room of an inn.</summary>
public readonly struct HousingPlace
{
    public readonly Property property;
    public readonly Inn inn;
    public readonly InnRoom room;

    public HousingPlace(Property p)
    {
        property = p;
        inn = null;
        room = null;
    }

    public HousingPlace(Inn i, InnRoom r)
    {
        property = null;
        inn = i;
        room = r;
    }

    public bool IsValid => property != null || (inn != null && room != null);
    /// <summary>The property's id, or the inn's id (with <see cref="RoomId"/>).</summary>
    public string PlaceId => property != null ? property.PropertyId : inn != null ? inn.InnId : "";
    public string RoomId => room != null ? room.RoomId : null;
    /// <summary>One string naming the place (the property's id, or "inn/room").</summary>
    public string Key => HousingGate.KeyOf(PlaceId, RoomId);
    public string Name => property != null ? property.DisplayName : room != null ? $"{room.DisplayName}, {(inn != null ? inn.DisplayName : "")}" : "";

    /// <summary>Is the world point inside (the house, or the room)?</summary>
    public bool Inside(Vector3 world) => property != null ? property.InHouse(world) : room != null && room.Contains(world);

    /// <summary>The same place (the same property, or the same room of the same inn)?</summary>
    public bool Is(HousingPlace other) => IsValid && property == other.property && inn == other.inn && room == other.room;

    /// <summary>Closed: its property, inn or room has the id of another one (it owns nothing and opens for nobody).</summary>
    public bool OutOfOrder => (property != null && property.OutOfOrder) || (inn != null && inn.OutOfOrder) || (room != null && room.OutOfOrder);
}

/// <summary>
/// Who may use the doors, beds, chests and gardens of rented places: the tenant, and whom they let in (Housing's access
/// rules). Installed on every machine as the access hooks of <see cref="SoilField"/>, <see cref="Storage"/>,
/// <see cref="SleepSpot"/> and <see cref="LockableDoor"/>: where the rules run it asks the books (the real check); on a
/// network client it answers the local player's prompts from what the server told it (the server checks again).
/// Things that belong to no property or room stay public. A hook a game set before is kept and asked first (its refusal
/// stands); a game that sets one later keeps the previous delegate and calls it first.
/// <para>One thing belongs to one place: a chest, bed, door or garden that two places list stays with the place that
/// registered first (the other is told once, as an error; the editor's validator reports it too).</para>
/// </summary>
public static class HousingGate
{
    private static readonly Dictionary<UnityEngine.Object, HousingPlace> places = new Dictionary<UnityEngine.Object, HousingPlace>();
    private static readonly HashSet<UnityEngine.Object> told = new HashSet<UnityEngine.Object>();
    private static bool installed;

    /// <summary>Client: what the local player may do in each place (from the server).</summary>
    public static readonly Dictionary<string, HousingAccess> LocalAccess = new Dictionary<string, HousingAccess>();
    /// <summary>Client: the places nobody rents (their doors open for visitors), from the server.</summary>
    public static readonly HashSet<string> VacantPlaces = new HashSet<string>();
    /// <summary>Client: the local player's access changed (prompts and windows follow).</summary>
    public static event Action LocalAccessChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        places.Clear();
        told.Clear();
        installed = false;
        LocalAccess.Clear();
        VacantPlaces.Clear();
        LocalAccessChanged = null;
    }

    public static string KeyOf(string placeId, string roomId) => string.IsNullOrEmpty(roomId) ? placeId ?? "" : $"{placeId}/{roomId}";

    /// <summary>
    /// Installs the access hooks (once). A hook already set (by the game) is kept and asked first: its refusal stands,
    /// and Housing's rules are asked only when it lets the player.
    /// </summary>
    public static void Install()
    {
        if (installed) return;
        installed = true;
        Func<SoilField, CombatEntity, FarmActionKind, string> garden = SoilField.AccessCheck;
        SoilField.AccessCheck = garden == null ? GardenCheck : (f, p, k) => garden(f, p, k) ?? GardenCheck(f, p, k);
        Func<Storage, InventoryManager, string> chest = Storage.AccessCheck;
        Storage.AccessCheck = chest == null ? ChestCheck : (s, inv) => chest(s, inv) ?? ChestCheck(s, inv);
        Func<SleepSpot, CombatEntity, string> bed = SleepSpot.AccessCheck;
        SleepSpot.AccessCheck = bed == null ? BedCheck : (b, p) => bed(b, p) ?? BedCheck(b, p);
        Func<LockableDoor, CombatEntity, string> door = LockableDoor.AccessCheck;
        LockableDoor.AccessCheck = door == null ? DoorCheck : (d, p) => door(d, p) ?? DoorCheck(d, p);
    }

    // ---------------------------------------------------------------- what belongs where
    public static void Register(Property p)
    {
        if (p == null) return;
        Install();
        var place = new HousingPlace(p);
        foreach (SoilField f in p.gardens) if (f != null) Claim(f, place);
        foreach (HousingChest c in p.chests) if (c != null && c.storage != null) Claim(c.storage, place);
        foreach (SleepSpot b in p.beds) if (b != null) Claim(b, place);
        foreach (LockableDoor d in p.doors) if (d != null) Claim(d, place);
    }

    public static void Register(Inn inn)
    {
        if (inn == null) return;
        Install();
        foreach (InnRoom r in inn.rooms)
        {
            if (r == null || !inn.Owns(r)) continue; // a room under another inn is that inn's
            r.Collect();
            var place = new HousingPlace(inn, r);
            if (r.bed != null) Claim(r.bed, place);
            foreach (HousingChest c in r.chests) if (c != null && c.storage != null) Claim(c.storage, place);
            foreach (LockableDoor d in r.doors) if (d != null) Claim(d, place);
        }
    }

    /// <summary>A thing becomes the place's - unless another place that is still here registered it first.</summary>
    private static void Claim(UnityEngine.Object thing, HousingPlace place)
    {
        if (places.TryGetValue(thing, out HousingPlace had) && had.IsValid && !had.Is(place) && StillHere(had))
        {
            if (told.Add(thing))
                Debug.LogError($"Housing: '{thing.name}' is listed by both '{had.Name}' and '{place.Name}': it stays with '{had.Name}'. " +
                               "List it in one place only.", thing);
            return;
        }
        places[thing] = place;
    }

    private static bool StillHere(HousingPlace p) =>
        p.property != null ? HousingDirector.IsRegistered(p.property) : p.inn != null && p.room != null && HousingDirector.IsRegistered(p.inn);

    /// <summary>Does the thing belong to this place (and to no other)?</summary>
    public static bool Owns(HousingPlace place, UnityEngine.Object thing) =>
        thing != null && places.TryGetValue(thing, out HousingPlace had) && had.Is(place);

    public static void Unregister(Property p)
    {
        if (p == null) return;
        Forget(pl => pl.property == p);
    }

    public static void Unregister(Inn inn)
    {
        if (inn == null) return;
        Forget(pl => pl.inn == inn);
    }

    private static readonly List<UnityEngine.Object> scratch = new List<UnityEngine.Object>();

    private static void Forget(Func<HousingPlace, bool> match)
    {
        scratch.Clear();
        foreach (KeyValuePair<UnityEngine.Object, HousingPlace> kv in places)
            if (kv.Key == null || match(kv.Value)) scratch.Add(kv.Key);
        foreach (UnityEngine.Object o in scratch) places.Remove(o);
    }

    /// <summary>The place a garden, chest, bed or door belongs to (false: none - it is public).</summary>
    public static bool PlaceOf(UnityEngine.Object thing, out HousingPlace place) => places.TryGetValue(thing, out place) && place.IsValid;

    // ---------------------------------------------------------------- who may
    /// <summary>What a player may do in a place now (where the rules run: the books; on a client: the local player's summary).</summary>
    public static HousingAccess AccessOf(HousingPlace place, CombatEntity player)
    {
        if (!place.IsValid || player == null || place.OutOfOrder) return HousingAccess.None;
        if (GameNet.NetAuthority.IsServer)
        {
            HousingBook book = HousingDirector.Book;
            if (book == null) return HousingAccess.None;
            return book.AccessOf(place.PlaceId, place.RoomId, HousingDirector.KeyOf(player), book.Now);
        }
        if (!CardTable.IsLocalPlayer(player)) return HousingAccess.None;
        return LocalAccess.TryGetValue(place.Key, out HousingAccess a) ? a : HousingAccess.None;
    }

    /// <summary>
    /// May visitors look round the place (a house nobody rents, with the settings' Vacant Doors Open)? Houses only: a free
    /// inn room stays locked for everyone, on every machine.
    /// </summary>
    public static bool OpenToVisitors(HousingPlace place) =>
        place.property != null && HousingSettings.Instance.vacantDoorsOpen && IsVacant(place);

    /// <summary>Is the place rented by nobody now?</summary>
    public static bool IsVacant(HousingPlace place)
    {
        if (!place.IsValid || place.OutOfOrder) return false;
        if (GameNet.NetAuthority.IsServer)
        {
            HousingBook book = HousingDirector.Book;
            return book != null && book.TenancyOf(place.PlaceId, place.RoomId, book.Now) == null;
        }
        return VacantPlaces.Contains(place.Key);
    }

    /// <summary>The client's summary arrived from the server.</summary>
    public static void SetLocalAccess(IList<string> placeKeys, IList<int> access, IList<string> vacant)
    {
        LocalAccess.Clear();
        if (placeKeys != null && access != null)
            for (int i = 0; i < placeKeys.Count && i < access.Count; i++)
                if (!string.IsNullOrEmpty(placeKeys[i])) LocalAccess[placeKeys[i]] = (HousingAccess)access[i] & HousingAccess.All;
        VacantPlaces.Clear();
        if (vacant != null) foreach (string v in vacant) if (!string.IsNullOrEmpty(v)) VacantPlaces.Add(v);
        LocalAccessChanged?.Invoke();
    }

    public static void ClearLocalAccess()
    {
        LocalAccess.Clear();
        VacantPlaces.Clear();
        LocalAccessChanged?.Invoke();
    }

    // ---------------------------------------------------------------- the hooks
    private static string GardenCheck(SoilField field, CombatEntity player, FarmActionKind kind)
    {
        if (field == null || !PlaceOf(field, out HousingPlace place)) return null;
        if ((AccessOf(place, player) & HousingAccess.Garden) != 0) return null;
        // The garden of a house nobody rents may be looked at (players look round a house before they bid), never worked.
        if (kind == FarmActionKind.Inspect && OpenToVisitors(place)) return null;
        return "Not your garden.";
    }

    private static string ChestCheck(Storage storage, InventoryManager inventory)
    {
        if (storage == null || !PlaceOf(storage, out HousingPlace place)) return null;
        CombatEntity player = inventory != null ? inventory.PlayerEntity : null;
        return (AccessOf(place, player) & HousingAccess.Storage) != 0 ? null : "Not yours.";
    }

    private static string BedCheck(SleepSpot bed, CombatEntity sleeper)
    {
        if (bed == null || !PlaceOf(bed, out HousingPlace place)) return null;
        return (AccessOf(place, sleeper) & HousingAccess.Sleep) != 0 ? null : "Not your bed.";
    }

    private static string DoorCheck(LockableDoor door, CombatEntity player)
    {
        if (door == null || !PlaceOf(door, out HousingPlace place)) return null;
        if (player == null) return "Locked";
        if ((AccessOf(place, player) & HousingAccess.Enter) != 0) return null;
        // A house nobody rents may be looked round. Nobody is shut in: anyone standing inside may open the doors to leave
        // (on every machine, so the prompt and the server agree); who stays inside without being let in is shown out.
        if (OpenToVisitors(place)) return null;
        if (place.Inside(player.transform.position)) return null;
        return "Locked";
    }
}
