using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// What the housing windows share - the innkeeper's (<see cref="InnWindow"/>), the realtor's (<see cref="RealtorWindow"/>)
/// and the player's own (<see cref="MyHousingWindow"/>): money and time in words, numbers the player types read the same
/// in every language, the views made safe to show (nothing missing, nothing unreadable), the rows they all show
/// (messages, what is kept for the player, who they let in, the players next to them, the places they manage) with
/// the signatures that say when those rows must be made again (<see cref="HousingSection"/>), and the plain pieces they
/// are built of. Labels are ASCII (a fallback font may lack other symbols) and never read rich-text tags out of names or
/// messages.
/// </summary>
public static class HousingUI
{
    /// <summary>The windows' size in reference pixels (shrunk to fit small screens).</summary>
    public static readonly Vector2 WindowSize = new Vector2(1200f, 760f);

    private const int MaxMessages = 40;
    private const int MaxHeld = 30;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ words
    /// <summary>Text made safe for any font: the few symbols the housing texts use become ASCII, control characters go.</summary>
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool plain = true;
        foreach (char c in s)
            if (c > 126 || (c < 32 && c != '\n')) { plain = false; break; }
        if (plain) return s;
        var b = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            int u = c;
            switch (u)
            {
                case 0x00B2: b.Append('2'); break; // squared
                case 0x00B3: b.Append('3'); break; // cubed
                case 0x00D7: b.Append('x'); break; // times
                case 0x00B7: case 0x2022: case 0x2219: b.Append('-'); break; // middle dot, bullets
                case 0x2010: case 0x2011: case 0x2012: case 0x2013: case 0x2014: case 0x2015: case 0x2212: b.Append('-'); break; // dashes, minus
                case 0x2018: case 0x2019: case 0x201A: case 0x2032: b.Append('\''); break; // single quotes, prime
                case 0x201C: case 0x201D: case 0x201E: case 0x2033: b.Append('"'); break; // double quotes
                case 0x2026: b.Append("..."); break; // ellipsis
                case 0x2192: case 0x25B6: case 0x25B8: case 0x203A: b.Append('>'); break; // arrows right
                case 0x2190: case 0x25C0: case 0x25C2: case 0x2039: b.Append('<'); break; // arrows left
                case 0x25B2: b.Append('^'); break;
                case 0x25BC: b.Append('v'); break;
                case 0x2715: case 0x2716: case 0x2717: b.Append('x'); break; // crosses
                case 0x2713: case 0x2714: b.Append('+'); break; // ticks
                case 0x00A0: case 0x2007: case 0x2009: case 0x202F: case 0x0009: b.Append(' '); break; // spaces, tab
                case 0x00B0: b.Append(" deg"); break;
                default:
                    if (u < 32 && u != 10) break; // other control characters
                    if (char.IsSurrogate(c)) break; // emoji and other symbols beyond the basic plane
                    if (u >= 0x2000 && u <= 0x200A) { b.Append(' '); break; } // odd spaces
                    if ((u >= 0x200B && u <= 0x200F) || (u >= 0x2028 && u <= 0x202E) || (u >= 0x2060 && u <= 0x206F) || u == 0xFEFF) break; // invisible
                    if (u >= 0x2190 && u <= 0x2BFF) break; // arrows, shapes and dingbats a font may lack
                    b.Append(c); // letters with accents and currency signs stay
                    break;
            }
        }
        return b.ToString();
    }

    private static string Symbol(string symbol)
    {
        string s = Clean(symbol ?? "").Trim();
        return s.Length > 0 ? " " + s : "";
    }

    /// <summary>"1,234 g" (the view's currency symbol; nothing when it has none).</summary>
    public static string Money(long amount, string symbol) => amount.ToString("#,0", Inv) + Symbol(symbol);

    /// <summary>
    /// Thousandths of money as a number: whole or two decimals, rounded up to the hundredth ("12", "12.5", "12.35"; with
    /// <paramref name="grouped"/> false, no thousands separators - as a field takes it back).
    /// </summary>
    public static string Rate(long milli, bool grouped = true)
    {
        if (milli == long.MinValue) milli = long.MinValue + 1;
        if (milli < 0) return "-" + Rate(-milli, grouped);
        long cents = milli / 10 + (milli % 10 != 0 ? 1 : 0);
        long whole = cents / 100;
        int frac = (int)(cents % 100);
        string w = whole.ToString(grouped ? "#,0" : "0", Inv);
        if (frac == 0) return w;
        return frac % 10 == 0 ? w + "." + (frac / 10).ToString(Inv) : w + "." + frac.ToString("00", Inv);
    }

    /// <summary>"12.35 g a day" (thousandths a day).</summary>
    public static string PerDay(long milli, string symbol) => Rate(milli) + Symbol(symbol) + " a day";

    /// <summary>What a term costs at a rent a day - the books' own rule: ceil(per day x days).</summary>
    public static long Total(long rateMilli, int days) => (long)Math.Ceiling(rateMilli * (double)Math.Max(0, days) / 1000.0 - 1e-9);

    /// <summary>Thousandths rounded up to the hundredth (an offer typed with two decimals that is not below them).</summary>
    public static long UpToCent(long milli) => milli <= 0 ? 0 : (milli + 9) / 10 * 10;

    // ------------------------------------------------------------------ numbers the player types
    /// <summary>
    /// What a money field lets the player type (its <c>onValidateInput</c>, which a text field asks before its own content
    /// type): digits, '.' and ',' - either may be the decimal mark or separate the thousands, whatever the system's
    /// language - and spaces or apostrophes between groups of digits. What was typed is read by
    /// <see cref="ParseRate(string, out long, out string)"/> or <see cref="ParseAmount(string, out long, out string)"/>,
    /// which refuse what they cannot read for sure.
    /// </summary>
    public static char NumberChar(string text, int index, char c)
    {
        if (c >= '0' && c <= '9') return c;
        return c == '.' || c == ',' || IsGroupMark(c) ? c : '\0';
    }

    /// <summary>Marks that only ever stand between groups of three digits (spaces, apostrophes).</summary>
    private static bool IsGroupMark(char c) =>
        c == ' ' || c == '\'' || c == '_' || c == (char)0x00A0 || c == (char)0x2009 || c == (char)0x202F || c == (char)0x2019;

    private enum NumberRead { Ok, Unreadable, Ambiguous, NotWhole, TooLarge }

    /// <summary>
    /// Reads a number as the player wrote it, in any language: '.' or ',' before the last one or two digits is the decimal
    /// mark ("12.50", "12,5"); '.', ',', spaces or apostrophes before each group of three digits separate the thousands
    /// ("1,234.50", "1.234,50", "1 234,5", "1'234"). A single '.' or ',' before exactly three digits ("1.250") is 1250,
    /// or 1.25 written with three decimals: with <paramref name="decimals"/> it is not guessed (ambiguous); without, it
    /// can only separate thousands. What was meant is never multiplied: anything else cannot be read.
    /// </summary>
    private static NumberRead ReadNumber(string text, bool decimals, int maxWholeDigits, out long whole, out int cents)
    {
        whole = 0;
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return NumberRead.Unreadable;
        string s = text.Trim();
        // The marks between the runs of digits (run k comes before mark k, run n after the last one).
        var marks = new List<int>(4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9') continue;
            if (c != '.' && c != ',' && !IsGroupMark(c)) return NumberRead.Unreadable;
            marks.Add(i);
        }
        int n = marks.Count;
        int RunStart(int k) => k == 0 ? 0 : marks[k - 1] + 1;
        int RunLength(int k) => (k < n ? marks[k] : s.Length) - RunStart(k);
        char Mark(int k) => s[marks[k]];
        // The decimal mark: the last '.' or ',' when no more than two digits follow it.
        int dec = -1;
        if (n > 0 && (Mark(n - 1) == '.' || Mark(n - 1) == ','))
        {
            int after = RunLength(n);
            if (after <= 2) dec = n - 1;
            else if (after != 3) return NumberRead.Unreadable;
            else if (decimals && n == 1) return NumberRead.Ambiguous;
        }
        // Every other mark separates thousands: all of one kind, not the decimal mark, each before three digits.
        int groupMarks = dec >= 0 ? dec : n;
        char kind = '\0';
        for (int k = 0; k < groupMarks; k++)
        {
            char m = IsGroupMark(Mark(k)) ? ' ' : Mark(k);
            if (kind == '\0') kind = m;
            else if (m != kind) return NumberRead.Unreadable;
            if (RunLength(k + 1) != 3) return NumberRead.Unreadable;
        }
        if (groupMarks > 0)
        {
            if (RunLength(0) < 1 || RunLength(0) > 3) return NumberRead.Unreadable;
            if (dec >= 0 && Mark(dec) == kind) return NumberRead.Unreadable;
        }
        int wholeRuns = dec >= 0 ? dec + 1 : n + 1;
        int digits = 0, significant = 0;
        long w = 0;
        for (int k = 0; k < wholeRuns; k++)
            for (int i = RunStart(k), end = i + RunLength(k); i < end; i++)
            {
                digits++;
                if (w == 0 && s[i] == '0') continue;
                if (++significant > maxWholeDigits) return NumberRead.TooLarge;
                w = w * 10 + (s[i] - '0');
            }
        int fracLength = dec >= 0 ? RunLength(dec + 1) : 0;
        if (digits == 0 && fracLength == 0) return NumberRead.Unreadable;
        int f = 0;
        if (fracLength >= 1) f = (s[RunStart(dec + 1)] - '0') * 10;
        if (fracLength == 2) f += s[RunStart(dec + 1) + 1] - '0';
        whole = w;
        cents = f;
        return !decimals && f != 0 ? NumberRead.NotWhole : NumberRead.Ok;
    }

    /// <summary>The digits of what was typed, nothing else ("1.250" - "1250").</summary>
    private static string DigitsOf(string text)
    {
        var b = new StringBuilder();
        if (text != null)
            foreach (char c in text)
                if (c >= '0' && c <= '9') b.Append(c);
        return b.ToString();
    }

    /// <summary>An offer a day typed by the player ("12", "12.5", "12,50", "1,250.00"); in thousandths.</summary>
    public static bool ParseRate(string text, out long milli) => ParseRate(text, out milli, out _);

    /// <summary>
    /// An offer a day typed by the player, in thousandths: a whole number or up to two decimals, '.' or ',' as the decimal
    /// mark ("12.5", "12,50", "1,250.00", "1.250,00"). False when it cannot be read for sure; <paramref name="problem"/>
    /// then says what to write.
    /// </summary>
    public static bool ParseRate(string text, out long milli, out string problem)
    {
        milli = 0;
        problem = "";
        switch (ReadNumber(text, true, 12, out long whole, out int cents))
        {
            case NumberRead.Ok:
                milli = whole * 1000 + cents * 10;
                if (milli > 0) return true;
                problem = "Write an offer above 0.";
                return false;
            case NumberRead.Ambiguous:
                string digits = DigitsOf(text);
                problem = $"Is it {digits}, or a number with decimals? Write {digits} with no separator, or at most two decimals (12.50 or 12,50).";
                return false;
            case NumberRead.TooLarge:
                problem = "That offer is too large.";
                return false;
            default:
                problem = "Write an offer a day: a whole number or two decimals (12.50 or 12,50).";
                return false;
        }
    }

    /// <summary>A whole amount of money typed by the player ("1250", "1,250", "1.250", "1 250").</summary>
    public static bool ParseAmount(string text, out long amount) => ParseAmount(text, out amount, out _);

    /// <summary>
    /// A whole amount of money typed by the player: '.', ',', spaces or apostrophes may separate the thousands, and a
    /// decimal part is refused unless it is zero ("12,50" is not read as 1250). False when it cannot be read for sure;
    /// <paramref name="problem"/> then says what to write.
    /// </summary>
    public static bool ParseAmount(string text, out long amount, out string problem)
    {
        amount = 0;
        problem = "";
        NumberRead r = ReadNumber(text, false, 10, out long whole, out _);
        if (r == NumberRead.Ok && whole > 0 && whole <= int.MaxValue)
        {
            amount = whole;
            return true;
        }
        problem = r == NumberRead.NotWhole ? "Whole amounts only (no cents)."
            : r == NumberRead.TooLarge || (r == NumberRead.Ok && whole > int.MaxValue) ? "That amount is too large."
            : r == NumberRead.Ok ? "Write an amount above 0."
            : "Write a whole amount (1250 or 1,250).";
        return false;
    }

    /// <summary>Lease days as words ("2 days 3 h", "40 min"), however broken the numbers that came.</summary>
    public static string Duration(double days, double secondsPerDay, bool gameTime)
    {
        if (double.IsNaN(days) || double.IsInfinity(days) || days < 0.0) days = 0.0;
        if (days > 100000.0) days = 100000.0;
        return Clean(HousingService.Duration(days, SecondsPerDay(secondsPerDay), gameTime));
    }

    /// <summary>"3 h ago".</summary>
    public static string Ago(double days, double secondsPerDay, bool gameTime) => Duration(days, secondsPerDay, gameTime) + " ago";

    public static string Nights(int n) => n == 1 ? "1 night" : n.ToString(Inv) + " nights";

    public static string Plural(int n, string one, string many) => n.ToString(Inv) + " " + (n == 1 ? one : many);

    /// <summary>"week" or "month" (<see cref="LeaseTerm"/>).</summary>
    public static string TermName(int term) => term == (int)LeaseTerm.Month ? "month" : "week";

    public static int TermDays(RealtorView v, int term) => v == null ? 7 : term == (int)LeaseTerm.Month ? v.monthDays : v.weekDays;

    /// <summary>"x1.3".</summary>
    public static string Times(double f) => "x" + (double.IsNaN(f) || double.IsInfinity(f) ? 1.0 : f).ToString("0.0#", Inv);

    /// <summary>An inn's price level in words: "usual prices", "busier than usual: prices x1.3", "quiet: prices x0.8".</summary>
    public static string InnLevel(double level)
    {
        if (double.IsNaN(level) || double.IsInfinity(level) || level <= 0.0) return "usual prices";
        if (level >= 1.05) return "busier than usual: prices " + Times(level);
        if (level <= 0.95) return "quiet: prices " + Times(level);
        return "usual prices";
    }

    public static string Percent(double share) =>
        (double.IsNaN(share) || double.IsInfinity(share) ? 0.0 : Math.Round(Math.Max(0.0, Math.Min(1.0, share)) * 100.0)).ToString("0", Inv) + "%";

    public static string SquareMetres(float area) => Mathf.RoundToInt(float.IsNaN(area) ? 0f : Mathf.Max(0f, area)).ToString("#,0", Inv) + " m2";

    /// <summary>"House 80 m2 (2 floors) - Land 300 m2 - Garden 30 m2".</summary>
    public static string Sizes(ListingView l)
    {
        if (l == null) return "";
        var s = new StringBuilder();
        s.Append("House ").Append(SquareMetres(l.houseArea)).Append(" (").Append(Plural(Math.Max(1, l.floors), "floor", "floors")).Append(')');
        s.Append(" - Land ").Append(SquareMetres(l.landArea));
        if (l.gardenArea > 0.5f) s.Append(" - Garden ").Append(SquareMetres(l.gardenArea));
        return s.ToString();
    }

    /// <summary>"120 m north-east" ("here" when it is close).</summary>
    public static string Way(ListingView l)
    {
        if (l == null) return "";
        string dir = Clean(l.direction ?? "").Trim();
        if (dir == "here") return "right here";
        string m = Mathf.RoundToInt(float.IsNaN(l.distance) ? 0f : Mathf.Max(0f, l.distance)).ToString("#,0", Inv) + " m";
        return dir.Length > 0 ? m + " " + dir : m;
    }

    /// <summary>"Bob #K3PQ" (a player is named by name and fingerprint, never by key).</summary>
    public static string Who(string name, string fingerprint)
    {
        string n = string.IsNullOrWhiteSpace(name) ? "Someone" : name.Trim();
        return string.IsNullOrWhiteSpace(fingerprint) ? n : n + " #" + fingerprint.Trim();
    }

    /// <summary>"Apple x3, Iron Sword (4 items)".</summary>
    public static string Names(HeldView h)
    {
        if (h == null) return "";
        var s = new StringBuilder();
        int shown = 0;
        if (h.names != null)
            foreach (string n in h.names)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (shown++ > 0) s.Append(", ");
                s.Append(n.Trim());
            }
        if (shown == 0) s.Append("Some things");
        else if (h.stacks > shown) s.Append(" and more");
        if (h.items > 0) s.Append(" (").Append(Plural(h.items, "item", "items")).Append(')');
        return s.ToString();
    }

    public static string Capital(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ------------------------------------------------------------------ the views, made safe to show
    private static double Finite(double d, double fallback = 0.0) => double.IsNaN(d) || double.IsInfinity(d) ? fallback : d;

    private static double SecondsPerDay(double s) => double.IsNaN(s) || double.IsInfinity(s) || s <= 0.0 ? 86400.0 : s;

    private static float Finite(float f, float fallback) => float.IsNaN(f) || float.IsInfinity(f) ? fallback : f;

    public static AccountView Fix(AccountView a)
    {
        if (a == null) a = new AccountView();
        if (a.currency == null) a.currency = "";
        if (a.notices == null) a.notices = new List<NoticeView>();
        if (a.held == null) a.held = new List<HeldView>();
        if (a.places == null) a.places = new List<PlaceAccessView>();
        a.notices.RemoveAll(n => n == null);
        a.held.RemoveAll(h => h == null);
        a.places.RemoveAll(p => p == null);
        foreach (NoticeView n in a.notices)
        {
            if (n.text == null) n.text = "";
            n.ago = Finite(n.ago);
        }
        foreach (HeldView h in a.held)
        {
            if (h.source == null) h.source = "";
            if (h.names == null) h.names = new List<string>();
            h.names.RemoveAll(x => x == null);
            h.heldFor = Finite(h.heldFor);
            h.freeFor = Finite(h.freeFor, -1.0);
            h.discardIn = Finite(h.discardIn, -1.0);
        }
        foreach (PlaceAccessView p in a.places)
        {
            if (p.placeId == null) p.placeId = "";
            if (p.roomId == null) p.roomId = "";
            if (p.name == null) p.name = "";
            if (p.grants == null) p.grants = new List<GrantView>();
            Fix(p.grants);
        }
        return a;
    }

    private static void Fix(List<GrantView> grants)
    {
        grants.RemoveAll(g => g == null);
        foreach (GrantView g in grants)
        {
            if (g.name == null) g.name = "";
            if (g.fingerprint == null) g.fingerprint = "";
        }
    }

    private static void Fix(List<NearbyView> nearby)
    {
        nearby.RemoveAll(n => n == null);
        foreach (NearbyView n in nearby)
        {
            if (n.name == null) n.name = "";
            if (n.fingerprint == null) n.fingerprint = "";
        }
    }

    public static InnView Fix(InnView v)
    {
        if (v == null) return null;
        if (v.closed == null) v.closed = "";
        if (v.innId == null) v.innId = "";
        if (v.name == null) v.name = "";
        if (v.district == null) v.district = "";
        if (v.myRoomId == null) v.myRoomId = "";
        if (v.myRoomName == null) v.myRoomName = "";
        if (v.rooms == null) v.rooms = new List<InnRoomView>();
        v.rooms.RemoveAll(r => r == null);
        foreach (InnRoomView r in v.rooms)
        {
            if (r.id == null) r.id = "";
            if (r.name == null) r.name = "";
            r.quality = Finite(r.quality, 1f);
            if (r.nightly < 0) r.nightly = 0;
        }
        if (v.myGrants == null) v.myGrants = new List<GrantView>();
        Fix(v.myGrants);
        if (v.nearby == null) v.nearby = new List<NearbyView>();
        Fix(v.nearby);
        v.maxNights = Math.Max(1, v.maxNights);
        v.level = Finite(v.level, 1.0);
        if (v.level <= 0.0) v.level = 1.0;
        v.occupancy = Math.Max(0.0, Math.Min(1.0, Finite(v.occupancy)));
        v.nextFreeIn = Finite(v.nextFreeIn, -1.0);
        v.myEndsIn = Finite(v.myEndsIn);
        if (v.extendNightly < 0) v.extendNightly = 0;
        v.maxMoreNights = Math.Max(0, Math.Min(v.maxMoreNights, v.maxNights));
        v.extendReason ??= "";
        v.secondsPerDay = SecondsPerDay(v.secondsPerDay);
        v.account = Fix(v.account);
        return v;
    }

    public static RealtorView Fix(RealtorView v)
    {
        if (v == null) return null;
        if (v.closed == null) v.closed = "";
        if (v.realtorName == null) v.realtorName = "";
        if (v.mayOrder == null) v.mayOrder = "";
        v.clearingEvery = Finite(v.clearingEvery, 0.25);
        if (v.clearingEvery <= 0.0) v.clearingEvery = 0.25;
        if (v.listings == null) v.listings = new List<ListingView>();
        if (v.leases == null) v.leases = new List<LeaseView>();
        if (v.orderHouses == null) v.orderHouses = new List<string>();
        if (v.orderTerms == null) v.orderTerms = new List<int>();
        if (v.orderOffersMilli == null) v.orderOffersMilli = new List<long>();
        if (v.orderPendingHouses == null) v.orderPendingHouses = new List<string>();
        if (v.orderPendingTerms == null) v.orderPendingTerms = new List<int>();
        if (v.orderPendingOffersMilli == null) v.orderPendingOffersMilli = new List<long>();
        v.orderPendingIn = Finite(v.orderPendingIn, -1.0);
        v.orderCancelIn = Finite(v.orderCancelIn, -1.0);
        v.orderNotice = Math.Max(0.0, Finite(v.orderNotice));
        if (v.orderHeldFor == null) v.orderHeldFor = "";
        v.orderHeldIn = Finite(v.orderHeldIn, -1.0);
        if (v.districts == null) v.districts = new List<string>();
        if (v.districtLevels == null) v.districtLevels = new List<double>();
        if (v.nearby == null) v.nearby = new List<NearbyView>();
        v.listings.RemoveAll(l => l == null);
        foreach (ListingView l in v.listings)
        {
            if (l.id == null) l.id = "";
            if (l.name == null) l.name = "";
            if (l.description == null) l.description = "";
            if (l.district == null) l.district = "";
            if (l.zones == null) l.zones = "";
            if (l.tenant == null) l.tenant = "";
            if (l.direction == null) l.direction = "";
            if (l.salesMilli == null) l.salesMilli = new List<long>();
            if (l.salesAgo == null) l.salesAgo = new List<double>();
            if (!l.weekly && !l.monthly) l.weekly = true;
            l.houseArea = Finite(l.houseArea, 0f);
            l.landArea = Finite(l.landArea, 0f);
            l.gardenArea = Finite(l.gardenArea, 0f);
            l.distance = Finite(l.distance, 0f);
            l.floors = Math.Max(1, l.floors);
            l.endsIn = Finite(l.endsIn);
            l.firstClearingIn = Finite(l.firstClearingIn);
            if (l.askingMilli < 1) l.askingMilli = 1;
            if (l.minOfferMilli < 0) l.minOfferMilli = 0;
            if (l.takeAtMilli < 0) l.takeAtMilli = 0;
        }
        v.leases.RemoveAll(l => l == null);
        foreach (LeaseView l in v.leases)
        {
            if (l.propertyId == null) l.propertyId = "";
            if (l.name == null) l.name = "";
            if (l.grants == null) l.grants = new List<GrantView>();
            Fix(l.grants);
            l.endsIn = Finite(l.endsIn);
            l.protection = Finite(l.protection, 0f);
        }
        Fix(v.nearby);
        v.nextClearingIn = Finite(v.nextClearingIn);
        v.weekDays = Math.Max(1, v.weekDays);
        v.monthDays = Math.Max(1, v.monthDays);
        v.maxOrderItems = Math.Max(1, v.maxOrderItems);
        v.maxProtection = Math.Max(1f, Finite(v.maxProtection, 1f));
        v.defaultProtection = Mathf.Clamp(Finite(v.defaultProtection, 1f), 1f, v.maxProtection);
        v.protectionMargin = Mathf.Max(0f, Finite(v.protectionMargin, 0f));
        v.protectionPremium = Mathf.Max(0f, Finite(v.protectionPremium, 0f));
        v.confirmAboveAsking = Mathf.Max(1f, Finite(v.confirmAboveAsking, 5f));
        v.secondsPerDay = SecondsPerDay(v.secondsPerDay);
        v.account = Fix(v.account);
        return v;
    }

    /// <summary>A house of the view by id (null: not listed here).</summary>
    public static ListingView Listing(RealtorView v, string id)
    {
        if (v == null || string.IsNullOrEmpty(id)) return null;
        foreach (ListingView l in v.listings)
            if (l.id == id) return l;
        return null;
    }

    /// <summary>A house's name: from the view, else the loaded property, else "A house".</summary>
    public static string HouseName(RealtorView v, string id)
    {
        ListingView l = Listing(v, id);
        if (l != null && !string.IsNullOrWhiteSpace(l.name)) return l.name.Trim();
        Property p = string.IsNullOrEmpty(id) ? null : HousingDirector.FindProperty(id);
        return p != null ? p.DisplayName : "A house";
    }

    // ------------------------------------------------------------------ pieces
    /// <summary>A label of view text (cleaned; tags in names and messages are shown as typed).</summary>
    public static TextMeshProUGUI Text(Transform parent, string text, float size = 16f, Color? color = null, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI t = UITheme.Label(parent, Clean(text), size, color ?? UITheme.Text, align, style, "Text");
        t.richText = false;
        return t;
    }

    /// <summary>
    /// Sets a label's text (cleaned; untouched when it says that already); with <paramref name="hideEmpty"/> an empty label
    /// takes no room in its layout.
    /// </summary>
    public static void Set(TMP_Text t, string text, bool hideEmpty = false)
    {
        if (t == null) return;
        string s = Clean(text);
        if (t.text != s) t.text = s;
        if (hideEmpty && t.gameObject.activeSelf != (s.Length > 0)) t.gameObject.SetActive(s.Length > 0);
    }

    /// <summary>Sets a label's text and colour (in place: a label kept between views).</summary>
    public static void Set(TMP_Text t, string text, Color color)
    {
        if (t == null) return;
        Set(t, text);
        if (t.color != color) t.color = color;
    }

    /// <summary>A number in words for the player ("5", "2.5").</summary>
    public static string Number(double d) => (double.IsNaN(d) || double.IsInfinity(d) ? 0.0 : d).ToString("0.##", Inv);

    /// <summary>A quiet line ("Nobody.", "Asking the innkeeper...").</summary>
    public static TextMeshProUGUI Note(Transform parent, string text) => Text(parent, text, 15f, UITheme.Muted, FontStyles.Italic);

    /// <summary>Takes the rest of a row's width.</summary>
    public static T Flex<T>(T c) where T : Component
    {
        InventoryUIFactory.Size(c, -1f, -1f, 1f);
        return c;
    }

    /// <summary>Removes what a list shows (hidden at once, destroyed at the end of the frame).</summary>
    public static void Clear(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            GameObject g = t.GetChild(i).gameObject;
            g.SetActive(false);
            UnityEngine.Object.Destroy(g);
        }
    }

    /// <summary>A rounded card whose height follows what is in it (a padded vertical stack).</summary>
    public static RectTransform Card(Transform parent, int padding = 14, float spacing = 6f)
    {
        Image bg = UITheme.Panel("Card", parent, UITheme.Card, UITheme.Radius);
        InventoryUIFactory.Vertical(bg.gameObject, padding, spacing, false);
        return bg.rectTransform;
    }

    /// <summary>A row as tall as its tallest part (text wraps; buttons keep their height, centred).</summary>
    public static RectTransform Line(Transform parent, float spacing = 8f, float minHeight = 34f)
    {
        RectTransform rt = InventoryUIFactory.Rect("Line", parent);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(rt.gameObject, spacing);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        return rt;
    }

    /// <summary>
    /// Names a part after what it is for - a player, someone let in, what is kept, a lease, a place, a room, a house of
    /// the order, or a card of a list: when its section is made again the keyboard's selection is found by these names
    /// (<see cref="HousingSection"/>), so it stays with the same one and never lands on another one's button.
    /// </summary>
    public static RectTransform Named(RectTransform part, string name)
    {
        if (part != null) part.gameObject.name = name ?? "";
        return part;
    }

    /// <summary>A stack of lines that takes the rest of a row's width.</summary>
    public static RectTransform TextColumn(Transform row)
    {
        RectTransform col = UITheme.Column("Text", row, 0, 2f);
        InventoryUIFactory.Size(col, -1f, -1f, 1f);
        return col;
    }

    /// <summary>A compact button.</summary>
    public static Button Small(Transform parent, string label, UnityAction onClick, float width = 100f,
        UITheme.ButtonStyle style = UITheme.ButtonStyle.Normal, float height = 34f)
    {
        Button b = UITheme.Button(label, parent, label, onClick, style, width, height);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.fontSize = height < 40f ? 15f : 17f;
            t.richText = false;
        }
        return b;
    }

    public static void SetLabel(Button b, string text)
    {
        if (b == null) return;
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t == null) return;
        t.richText = false;
        t.text = Clean(text);
    }

    /// <summary>"MESSAGES ..... [Clear]".</summary>
    public static Button SectionHeader(Transform parent, string text, string buttonLabel = null, UnityAction onButton = null, bool enabled = true)
    {
        RectTransform row = Line(parent, 8f, 28f);
        TextMeshProUGUI h = UITheme.Header(row, text, 26f);
        h.richText = false;
        InventoryUIFactory.Size(h, -1f, 26f, 1f);
        if (string.IsNullOrEmpty(buttonLabel)) return null;
        Button b = Small(row, buttonLabel, onButton, 90f, UITheme.ButtonStyle.Normal, 30f);
        b.interactable = enabled;
        return b;
    }

    /// <summary>A smaller header inside a card.</summary>
    public static TextMeshProUGUI SubHeader(Transform parent, string text)
    {
        TextMeshProUGUI t = Text(parent, text.ToUpperInvariant(), 13f, UITheme.Accent, FontStyles.Bold);
        t.characterSpacing = 3f;
        InventoryUIFactory.Size(t, -1f, 24f);
        return t;
    }

    /// <summary>
    /// A clickable card of a list (a house, a room, an item of the order): returns where its lines go - a vertical
    /// stack, or a row with <paramref name="horizontal"/>.
    /// </summary>
    public static RectTransform ClickCard(Transform parent, UnityAction onClick, out Button button, bool horizontal = false, float minHeight = 44f)
    {
        Image bg = UITheme.Panel("Item", parent, Color.white, UITheme.Radius);
        if (horizontal)
        {
            HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 10f);
            h.padding = new RectOffset(16, 14, 8, 8);
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
        }
        else
        {
            VerticalLayoutGroup v = InventoryUIFactory.Vertical(bg.gameObject, 0, 2f, false);
            v.padding = new RectOffset(16, 12, 8, 8);
        }
        LayoutElement le = bg.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        UITheme.Colors(button, UITheme.ButtonStyle.ListItem);
        if (onClick != null) button.onClick.AddListener(onClick);
        return bg.rectTransform;
    }

    /// <summary>Two buttons choosing a term ("Week", "Month"); a term the house is not let by is hidden.</summary>
    public static void TermButtons(Transform row, out Button week, out Button month, UnityAction onWeek, UnityAction onMonth)
    {
        week = Small(row, "Week", onWeek, 96f, UITheme.ButtonStyle.Tab, 36f);
        month = Small(row, "Month", onMonth, 96f, UITheme.ButtonStyle.Tab, 36f);
    }

    public static void ShowTerms(Button week, Button month, bool weekly, bool monthly, int term, bool interactable = true)
    {
        if (week != null)
        {
            week.gameObject.SetActive(weekly);
            week.interactable = interactable;
            UITheme.SetSelected(week, term == (int)LeaseTerm.Week);
        }
        if (month != null)
        {
            month.gameObject.SetActive(monthly);
            month.interactable = interactable;
            UITheme.SetSelected(month, term == (int)LeaseTerm.Month);
        }
    }

    // ------------------------------------------------------------------ requests every window makes
    /// <summary>
    /// A claim of what is kept for the player, with the fee they were shown: where the rules run, a claim whose fee changed
    /// meanwhile (its free days ended) is refused, and the window shows the new fee.
    /// </summary>
    public static HousingRequest ClaimRequest(HeldView h) => new HousingRequest
    {
        kind = (byte)HousingRequestKind.Claim, bundleId = h != null ? h.id : 0, flag = true, expected = h != null ? Math.Max(0L, h.fee) : 0,
    };

    /// <summary>Claims <paramref name="h"/> with the fee shown (asked first when it costs something).</summary>
    internal static void Claim(IHousingHost host, HeldView h, string currency)
    {
        if (host == null || h == null) return;
        HousingRequest r = ClaimRequest(h);
        if (h.fee > 0)
            host.Ask($"Claim what is kept for you{(!string.IsNullOrWhiteSpace(h.source) ? " from " + h.source.Trim() : "")}? Keeping it this long costs {Money(h.fee, currency)}.",
                "Claim", () => host.Send(r));
        else host.Send(r);
    }

    /// <summary>Letting a player next to the player into a place (its ids), with the rights chosen.</summary>
    public static HousingRequest GrantRequest(string placeId, string roomId, NearbyView n, HousingAccess access) => new HousingRequest
    {
        kind = (byte)HousingRequestKind.Grant, placeId = placeId ?? "", roomId = roomId ?? "", playerId = n != null ? n.playerId : 0, access = (int)access,
    };

    /// <summary>Letting someone out of a place (named by their fingerprint, never by key).</summary>
    public static HousingRequest RevokeRequest(string placeId, string roomId, GrantView g) => new HousingRequest
    {
        kind = (byte)HousingRequestKind.Revoke, placeId = placeId ?? "", roomId = roomId ?? "", fingerprint = g != null ? g.fingerprint : "",
    };

    /// <summary>
    /// A manager lets a player next to them into a place they manage, with the rights chosen (asked first unless they
    /// may only come in). The books refuse anything the manager may not give.
    /// </summary>
    internal static void LetIn(IHousingHost host, PlaceAccessView p, NearbyView n, HousingAccess access)
    {
        if (host == null || p == null || n == null) return;
        HousingRequest r = GrantRequest(p.placeId, p.roomId, n, access);
        if (access == HousingAccess.Enter)
        {
            host.Send(r);
            return;
        }
        string place = !string.IsNullOrWhiteSpace(p.name) ? p.name.Trim() : "that place";
        host.Ask($"Let {Who(n.name, n.fingerprint)} into {place}? They may {HousingService.Describe(access)}.", "Let them in", () => host.Send(r));
    }

    /// <summary>A manager lets out someone they let in.</summary>
    internal static void LetOut(IHousingHost host, PlaceAccessView p, GrantView g)
    {
        if (host == null || p == null || g == null) return;
        host.Send(RevokeRequest(p.placeId, p.roomId, g));
    }

    // ------------------------------------------------------------------ the order notice
    /// <summary>The player's order is being withdrawn (it stands, and may still win, until then).</summary>
    public static bool Withdrawing(RealtorView v) => v != null && v.orderCancelIn >= 0.0;

    /// <summary>A change of the player's order that takes something back waits for the order notice.</summary>
    public static bool ChangeWaiting(RealtorView v) => v != null && !Withdrawing(v) && v.orderPendingIn >= 0.0 && v.orderPendingHouses.Count > 0;

    /// <summary>The player's order is held for one house (a tenant's rent was fixed while its offer stood behind it).</summary>
    public static bool Held(RealtorView v) => v != null && !string.IsNullOrEmpty(v.orderHeldFor) && v.orderHouses.Count > 0;

    /// <summary>
    /// What is under way with the player's order - held for a house, its withdrawal, or a change waiting for the order
    /// notice - or "".
    /// </summary>
    public static string OrderUnderWay(RealtorView v)
    {
        string held = Held(v)
            ? $"Your order is held for {HouseName(v, v.orderHeldFor)}: its tenant's rent was fixed while your offer stood behind it. Until it " +
              $"is offered (in about {Duration(v.orderHeldIn, v.secondsPerDay, v.gameTime)}) your order wins no other house, and then it is tried " +
              "for that house first - if its tenant leaves, your offer takes it."
            : "";
        string rest = "";
        if (Withdrawing(v))
            rest = $"Your order is being withdrawn: in {Duration(v.orderCancelIn, v.secondsPerDay, v.gameTime)}. Offers are binding: until then it stands, " +
                "and may still win a house (and pays for it if it does).";
        else if (ChangeWaiting(v))
            rest = $"Your change takes effect in {Duration(v.orderPendingIn, v.secondsPerDay, v.gameTime)}. Offers are binding: until then your order " +
                "stands as it was, and may still win a house (and pays for it if it does).";
        return held.Length == 0 ? rest : rest.Length == 0 ? held : held + " " + rest;
    }

    /// <summary>"Withdraw your order? ..." - it is withdrawn only after the order notice.</summary>
    public static string WithdrawQuestion(RealtorView v)
    {
        if (v == null) return "Withdraw your order?";
        string notice = Duration(v.orderNotice, v.secondsPerDay, v.gameTime);
        return $"Withdraw your order? Offers are binding: it is withdrawn in about {notice} - until then it stands, and may still win a house " +
            $"(and pays for it if it does). Then its deposit ({Money(v.orderEscrow, v.account.currency)}) is given back to you." +
            (ChangeWaiting(v) ? " The change that waits is called off." : "");
    }

    // ------------------------------------------------------------------ signatures (what a row shows: made again only when it changes)
    /// <summary>The messages as <see cref="Messages"/> shows them.</summary>
    public static string MessagesSignature(AccountView a, double secondsPerDay, bool gameTime)
    {
        var s = new HousingSig();
        if (a == null || a.notices == null) return s.Add("none").ToString();
        foreach (NoticeView n in a.notices)
            if (n != null) s.Add(Ago(n.ago, secondsPerDay, gameTime)).Add(n.text);
        return s.ToString();
    }

    /// <summary>What is kept for the player, as <see cref="Held"/> shows it.</summary>
    public static string HeldSignature(AccountView a, double secondsPerDay, bool gameTime, bool atDesk)
    {
        var s = new HousingSig().Add(atDesk);
        if (a == null || a.held == null) return s.Add("none").ToString();
        s.Add(a.currency);
        foreach (HeldView h in a.held)
            if (h != null)
                s.Add(h.id).Add(h.source).Add(Names(h)).Add(h.fee).Add(Duration(h.heldFor, secondsPerDay, gameTime))
                 .Add(h.freeFor > 0.0 ? Duration(h.freeFor, secondsPerDay, gameTime) : "-").Add(h.discardIn >= 0.0 ? Duration(h.discardIn, secondsPerDay, gameTime) : "-");
        return s.ToString();
    }

    /// <summary>The places the player may use, as <see cref="Places"/> shows them (with the players next to them where they manage one).</summary>
    public static string PlacesSignature(AccountView a, List<NearbyView> nearby)
    {
        var s = new HousingSig();
        if (a == null || a.places == null) return s.Add("none").ToString();
        bool manages = false;
        foreach (PlaceAccessView p in a.places)
        {
            if (p == null) continue;
            s.Add(p.placeId).Add(p.roomId).Add(p.name).Add(p.access).Add(p.tenant);
            if (!Manages(p)) continue;
            manages = true;
            s.Add(p.grants);
        }
        if (manages) s.Add(nearby);
        return s.ToString();
    }

    // ------------------------------------------------------------------ the rows every window shows
    /// <summary>The player's messages, newest first, with Clear.</summary>
    public static void Messages(Transform parent, AccountView a, double secondsPerDay, bool gameTime, UnityAction clear)
    {
        List<NoticeView> list = a != null ? a.notices : null;
        RectTransform card = Named(Card(parent), "Messages");
        bool any = list != null && list.Count > 0;
        SectionHeader(card, "Messages", any ? "Clear" : null, clear);
        if (!any)
        {
            Note(card, "No messages.");
            return;
        }
        var sorted = new List<NoticeView>();
        foreach (NoticeView n in list)
            if (n != null) sorted.Add(n);
        sorted.Sort((x, y) => x.ago.CompareTo(y.ago));
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i >= MaxMessages)
            {
                Note(card, "And " + Plural(sorted.Count - MaxMessages, "older one", "older ones") + ".");
                break;
            }
            RectTransform m = UITheme.Column("Message", card, 0, 0f);
            Text(m, Ago(sorted[i].ago, secondsPerDay, gameTime), 13f, UITheme.Muted);
            Text(m, sorted[i].text, 15f);
        }
    }

    /// <summary>What is kept for the player (left in a chest when a stay or lease ended), with Claim.</summary>
    public static void Held(Transform parent, AccountView a, double secondsPerDay, bool gameTime, Action<HeldView> claim, bool atDesk)
    {
        RectTransform card = Named(Card(parent), "Kept For You");
        SectionHeader(card, "Kept for you");
        List<HeldView> list = a != null ? a.held : null;
        if (list == null || list.Count == 0)
        {
            Note(card, "Nothing is kept for you.");
            return;
        }
        if (!atDesk) Note(card, "Claims are made at a realtor or an innkeeper.");
        string cur = a.currency;
        for (int i = 0; i < list.Count; i++)
        {
            if (i >= MaxHeld)
            {
                Note(card, "And " + Plural(list.Count - MaxHeld, "more", "more") + ".");
                break;
            }
            HeldView h = list[i];
            if (h == null) continue;
            RectTransform row = Named(Line(card), "Kept " + h.id.ToString(Inv)); // named after it: a free claim is made without asking
            RectTransform col = TextColumn(row);
            Text(col, !string.IsNullOrWhiteSpace(h.source) ? "From " + h.source.Trim() : "Kept for you", 16f, UITheme.Text, FontStyles.Bold);
            Text(col, Names(h), 14f);
            string when = h.freeFor > 0.0 ? " for another " + Duration(h.freeFor, secondsPerDay, gameTime) : "";
            string discard = h.discardIn >= 0.0 ? " - thrown away in " + Duration(h.discardIn, secondsPerDay, gameTime) + " if not claimed" : "";
            Text(col, "Kept for " + Duration(h.heldFor, secondsPerDay, gameTime) +
                (h.fee > 0 ? " - claiming it costs " + Money(h.fee, cur) : " - free to claim" + when) + discard, 13f, UITheme.Muted);
            Button b = Small(row, "Claim", () => claim?.Invoke(h), 96f, UITheme.ButtonStyle.Primary);
            b.interactable = atDesk;
        }
    }

    /// <summary>Who the player let in (name #fingerprint, what they may do), with Remove.</summary>
    public static void Grants(Transform parent, List<GrantView> grants, Action<GrantView> remove)
    {
        if (grants == null || grants.Count == 0)
        {
            Note(parent, "Nobody else may use it.");
            return;
        }
        foreach (GrantView g in grants)
        {
            if (g == null) continue;
            RectTransform row = Named(Line(parent), "Grant " + g.fingerprint); // named after them: Remove lets them out at once
            RectTransform col = TextColumn(row);
            Text(col, Who(g.name, g.fingerprint), 16f, UITheme.Text, FontStyles.Bold);
            Text(col, "May " + HousingService.Describe((HousingAccess)g.access), 14f, UITheme.Muted);
            Small(row, "Remove", () => remove?.Invoke(g), 100f);
        }
    }

    /// <summary>The players next to the player (whom they may let in), one button per kind of access.</summary>
    public static void Nearby(Transform parent, List<NearbyView> nearby, params (string label, Action<NearbyView> act)[] buttons)
    {
        if (nearby == null || nearby.Count == 0)
        {
            Note(parent, "Nobody is next to you: whoever you let in must stand next to you.");
            return;
        }
        foreach (NearbyView n in nearby)
        {
            if (n == null) continue;
            // Visitor lets them in at once: the row is named after the player (who they are, and their session).
            RectTransform row = Named(Line(parent), "Player " + n.playerId.ToString(Inv) + " " + n.fingerprint);
            Flex(Text(row, Who(n.name, n.fingerprint), 16f));
            foreach ((string label, Action<NearbyView> act) in buttons)
            {
                Action<NearbyView> a = act;
                Small(row, label, () => a?.Invoke(n), 116f);
            }
        }
    }

    /// <summary>The player may let others in there and is not its tenant: a manager, who lets in or out only those they let in.</summary>
    public static bool Manages(PlaceAccessView p) => p != null && !p.tenant && ((HousingAccess)p.access & HousingAccess.Manage) != 0;

    /// <summary>
    /// What a manager may give (never Manage, never more than they may do themselves - the books refuse anything else):
    /// "Friend", "Garden only" (not in an inn's room) and "Visitor", each when they may do all of it, and "Same as you"
    /// when what they may do is another mix.
    /// </summary>
    public static List<(string label, HousingAccess access)> Giveable(PlaceAccessView p)
    {
        var list = new List<(string, HousingAccess)>();
        if (p == null) return list;
        HousingAccess own = (HousingAccess)p.access & HousingAccess.Friend;
        bool room = !string.IsNullOrEmpty(p.roomId);
        if (own == HousingAccess.Friend) list.Add(("Friend", HousingAccess.Friend));
        else if (own != HousingAccess.None && own != HousingAccess.Enter && own != HousingAccess.Garden) list.Add(("Same as you", own));
        if (!room && (own & HousingAccess.Garden) != 0) list.Add(("Garden only", HousingAccess.Garden));
        if ((own & HousingAccess.Enter) != 0) list.Add(("Visitor", HousingAccess.Enter));
        return list;
    }

    /// <summary>
    /// A place the player manages: who they let in there (Remove: only those, never the tenant's own choices) and those
    /// next to them, with what the player may give them.
    /// </summary>
    public static void Managed(Transform parent, PlaceAccessView p, List<NearbyView> nearby, Action<PlaceAccessView, NearbyView, HousingAccess> letIn,
        Action<PlaceAccessView, GrantView> letOut)
    {
        if (p == null) return;
        SubHeader(parent, "Who you let in");
        var mine = new List<GrantView>();
        if (p.grants != null)
            foreach (GrantView g in p.grants)
                if (g != null && g.byMe) mine.Add(g);
        if (mine.Count == 0) Note(parent, "Nobody yet. You may change or remove only those you let in.");
        else Grants(parent, mine, g => letOut?.Invoke(p, g));
        SubHeader(parent, "Let in");
        List<(string label, HousingAccess access)> giveable = Giveable(p);
        if (giveable.Count == 0)
        {
            Note(parent, "You may give only what you may do yourself: nothing here.");
            return;
        }
        var buttons = new (string, Action<NearbyView>)[giveable.Count];
        for (int i = 0; i < giveable.Count; i++)
        {
            HousingAccess access = giveable[i].access;
            buttons[i] = (giveable[i].label, n => letIn?.Invoke(p, n, access));
        }
        Nearby(parent, nearby, buttons);
    }

    /// <summary>
    /// The places the player may use: theirs, and those they were let into - where they may let others in (and are not
    /// the tenant), who they let in and those next to them (<see cref="Managed"/>; without <paramref name="letIn"/> only
    /// what they may do is said).
    /// </summary>
    public static void Places(Transform parent, AccountView a, List<NearbyView> nearby = null, Action<PlaceAccessView, NearbyView, HousingAccess> letIn = null,
        Action<PlaceAccessView, GrantView> letOut = null)
    {
        RectTransform card = Named(Card(parent), "Places");
        SectionHeader(card, "Places you may use");
        if (a == null || a.places == null || a.places.Count == 0)
        {
            Note(card, "None yet.");
            return;
        }
        foreach (PlaceAccessView p in a.places)
        {
            if (p == null) continue;
            string place = p.placeId + "/" + p.roomId;
            RectTransform col = UITheme.Column("Place " + place, card, 0, 0f);
            Text(col, !string.IsNullOrWhiteSpace(p.name) ? p.name.Trim() : "A place", 16f, UITheme.Text, FontStyles.Bold);
            Text(col, p.tenant ? "Yours." : "You were let in: you may " + HousingService.Describe((HousingAccess)p.access) + ".", 14f, UITheme.Muted);
            if (letIn == null || !Manages(p)) continue;
            RectTransform manage = UITheme.Column("Manage " + place, card, 0, 6f);
            Managed(manage, p, nearby, letIn, letOut);
        }
    }

    // ------------------------------------------------------------------ the frame of a window
    /// <summary>Title, subtitle, the purse and Close along the top of a window.</summary>
    public static void Header(Transform w, UnityAction close, out TextMeshProUGUI title, out TextMeshProUGUI subtitle, out TextMeshProUGUI purse)
    {
        title = UITheme.Label(w, "", 28f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        title.richText = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -54f), new Vector2(-440f, -14f));
        subtitle = UITheme.Label(w, "", 15f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        subtitle.richText = false;
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -84f), new Vector2(-440f, -54f));
        RectTransform head = UITheme.Area("Header", w, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-430f, -64f), new Vector2(-24f, -18f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(head.gameObject, 12f);
        h.childAlignment = TextAnchor.MiddleRight;
        h.childForceExpandHeight = false;
        purse = UITheme.Label(head, "", 17f, UITheme.Gold, TextAlignmentOptions.Right, FontStyles.Bold, "Purse");
        purse.richText = false;
        purse.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Size(purse, -1f, 42f, 1f);
        UITheme.Button("Close", head, "Close", close, UITheme.ButtonStyle.Normal, 110f, 42f);
    }

    /// <summary>The status line at the bottom of a window.</summary>
    public static TextMeshProUGUI StatusLine(Transform w)
    {
        TextMeshProUGUI s = UITheme.Label(w, "", 15f, UITheme.Good, TextAlignmentOptions.Center, FontStyles.Normal, "Status");
        s.richText = false;
        UITheme.Place(s.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 10f), new Vector2(-24f, 52f));
        return s;
    }

    /// <summary>The question over a window (<see cref="HousingConfirm"/>: Enter confirms; the window's close closes it first).</summary>
    public static HousingConfirm Confirm(Transform window) => HousingConfirm.Build(window);

    /// <summary>A panel on the window, placed by its offsets from the window's corners.</summary>
    public static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image bg = UITheme.Panel(name, parent, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
        return bg;
    }

    /// <summary>A list that fills <paramref name="parent"/> (inset by <paramref name="inset"/>); returns where its rows go.</summary>
    public static RectTransform FillList(Transform parent, string name, int padding = 10, float spacing = 8f, float inset = 4f)
    {
        RectTransform content = UITheme.ScrollList(name, parent, 0f, out _, new Color(0f, 0f, 0f, 0f), padding, spacing);
        InventoryUIFactory.Stretch((RectTransform)content.parent.parent, inset, inset);
        return content;
    }

    /// <summary>The window keeps its size, shrunk to fit small screens (like the shop window).</summary>
    public static void FitToCanvas(RectTransform rt, Vector2 size)
    {
        if (rt == null) return;
        rt.sizeDelta = size;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        Vector2 room = canvasRect.rect.size - new Vector2(40f, 40f);
        float scale = Mathf.Min(1f, room.x / Mathf.Max(1f, size.x), room.y / Mathf.Max(1f, size.y));
        rt.localScale = Vector3.one * Mathf.Max(0.5f, scale);
    }

    /// <summary>The mouse button is held (a slider being dragged, a click under way): the window waits to rebuild.</summary>
    public static bool PointerHeld()
    {
        Mouse m = Mouse.current;
        return m != null && m.leftButton != null && m.leftButton.isPressed;
    }
}

/// <summary>A housing window's side of the requests, for the pages it shows (<see cref="HousingHomePage"/>, <see cref="HousingAccountPage"/>).</summary>
internal interface IHousingHost
{
    CombatEntity Player { get; }

    /// <summary>Deposits, withdrawals and claims may be made (the player stands at a realtor or an innkeeper).</summary>
    bool AtDesk { get; }

    /// <summary>Sends a request (one at a time: "..." while one is on its way).</summary>
    void Send(HousingRequest request);

    /// <summary>Asks the player first (Enter confirms, Escape cancels).</summary>
    void Ask(string text, string yes, Action confirmed, bool danger = false, Action cancelled = null);

    /// <summary>The status line.</summary>
    void Say(string text, bool ok);
}

/// <summary>
/// The requests of one housing window: one at a time ("..." until its answer, told apart from others by its number),
/// the status line, the confirmation, and asking for fresh views - when an answer brought none, every so often, and
/// (where the rules run) when the books changed. A request with no answer in 5 s is kept: the player is asked to send it
/// again - the same request, with its id, so the housing office does it only once - or to leave it, and nothing else is
/// sent until then. It changes no game state itself: everything goes through <see cref="HousingClient"/>.
/// </summary>
internal sealed class HousingWindowCore
{
    public const float AnswerSeconds = 5f;
    public const float PollSeconds = 20f;
    private const float StatusSeconds = 8f;
    private const float BooksSeconds = 3f;
    /// <summary>The views follow every answer (where the rules run, in the same call; online, right after it): asked for only when none came this long after it.</summary>
    private const float AfterAnswerSeconds = 2f;

    private readonly TextMeshProUGUI status;
    private readonly HousingConfirm confirm;
    /// <summary>The request on its way; one no answer came for in time (kept, with its id, to be sent again or let go).</summary>
    private HousingRequest pending, stalled;
    /// <summary>Every number the request on its way (or stalled) was sent with: an answer to any of them is its answer.</summary>
    private readonly List<int> sentAs = new List<int>();
    private int refreshSeq;
    private float pendingSince, refreshAt = -1f, nextPoll, statusUntil, booksAllowed;
    private bool listening, inRefresh, refreshAnswered, askingStalled;

    /// <summary>The window's follow-up of the answer to its request (the request's kind; the answer).</summary>
    public Action<HousingRequestKind, HousingReply> Answered;
    /// <summary>The window has a view to show (a refusal of the views is said at once while it has none).</summary>
    public Func<bool> HasView;

    public HousingWindowCore(TextMeshProUGUI status, HousingConfirm confirm)
    {
        this.status = status;
        this.confirm = confirm;
    }

    public CombatEntity Player { get; private set; }
    /// <summary>The window shows the player's own housing (wherever they stand): its requests ask for those views.</summary>
    public bool Own { get; set; }
    /// <summary>A request is on its way, or waits to be sent again.</summary>
    public bool Busy => pending != null || stalled != null;
    public bool Asking => confirm != null && confirm.IsOpen;

    /// <summary>The window opened for <paramref name="player"/>: listens and asks for the views.</summary>
    public void Begin(CombatEntity player)
    {
        End();
        Player = player;
        listening = true;
        HousingClient.Answered += OnAnswered;
        HousingDirector.BooksChanged += OnBooksChanged;
        Say("", true);
        if (player != null) RefreshNow();
    }

    /// <summary>The window closed: stops listening; a late answer is ignored, a stalled request forgotten.</summary>
    public void End()
    {
        if (listening)
        {
            HousingClient.Answered -= OnAnswered;
            HousingDirector.BooksChanged -= OnBooksChanged;
            listening = false;
        }
        pending = null;
        stalled = null;
        sentAs.Clear();
        askingStalled = false;
        refreshSeq = 0;
        refreshAt = -1f;
        CloseConfirm();
        Player = null;
    }

    public void CloseConfirm()
    {
        if (confirm != null) confirm.Close();
    }

    public void Send(HousingRequest r)
    {
        if (r == null) return;
        if (Player == null)
        {
            Say("Nobody to ask for.", false);
            return;
        }
        if (stalled != null)
        {
            AskAboutStalled();
            return;
        }
        if (pending != null)
        {
            Wait();
            return;
        }
        sentAs.Clear();
        Dispatch(r);
    }

    /// <summary>Sends <paramref name="r"/> (again, after no answer came: the same object keeps its id, so it is done once).</summary>
    private void Dispatch(HousingRequest r)
    {
        r.own = Own;
        pending = r;
        pendingSince = Time.unscaledTime;
        Wait();
        int seq;
        try
        {
            seq = HousingClient.Send(Player, r);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (ReferenceEquals(pending, r))
            {
                pending = null;
                sentAs.Clear();
            }
            Say("Something went wrong.", false);
            RefreshSoon();
            return;
        }
        // Where the rules run the answer came during Send (matched by the number Send set on the request first).
        if (ReferenceEquals(pending, r) && seq != 0 && !sentAs.Contains(seq)) sentAs.Add(seq);
    }

    public void Ask(string text, string yes, Action confirmed, bool danger = false, Action cancelled = null)
    {
        if (stalled != null)
        {
            cancelled?.Invoke();
            AskAboutStalled();
            return;
        }
        if (pending != null)
        {
            Wait();
            cancelled?.Invoke();
            return;
        }
        if (confirm == null)
        {
            confirmed?.Invoke();
            return;
        }
        confirm.Show(text, yes, confirmed, danger, cancelled);
    }

    public void Say(string text, bool ok)
    {
        if (status == null) return;
        status.text = HousingUI.Clean(text);
        status.color = ok ? UITheme.Good : UITheme.Bad;
        statusUntil = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + StatusSeconds;
    }

    /// <summary>A plain remark on the status line.</summary>
    public void Tell(string text)
    {
        Say(text, true);
        if (status != null) status.color = UITheme.Text;
    }

    private void Wait()
    {
        if (status == null) return;
        status.text = "...";
        status.color = UITheme.Muted;
        statusUntil = 0f;
    }

    /// <summary>Asks for the views soon (not while a request is on its way or waits to be sent again: its answer brings them).</summary>
    public void RefreshSoon(float delay = 0f)
    {
        float at = Time.unscaledTime + Mathf.Max(0f, delay);
        if (refreshAt < 0f || at < refreshAt) refreshAt = at;
    }

    /// <summary>The window took a view: it is as fresh as can be, so a refresh that was coming is not needed.</summary>
    public void Viewed() => refreshAt = -1f;

    public void RefreshNow()
    {
        refreshAt = -1f;
        nextPoll = Time.unscaledTime + PollSeconds;
        if (Player == null) return;
        int seq = 0;
        inRefresh = true;
        refreshAnswered = false;
        try
        {
            seq = HousingClient.Refresh(Player, Own);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            inRefresh = false;
        }
        // Where the rules run its answer came during the call; on a network client it comes later, with this number.
        refreshSeq = refreshAnswered ? 0 : seq;
    }

    /// <summary>Every frame while the window is open.</summary>
    public void Tick()
    {
        float now = Time.unscaledTime;
        if (pending != null && now - pendingSince > AnswerSeconds)
        {
            // No answer (a lost message, a slow or vanished server): the request is kept, to be sent again - done once -
            // or let go. Nothing else is sent meanwhile, not even a refresh (the office remembers the request by its id).
            stalled = pending;
            pending = null;
            if (stalled.seq != 0 && !sentAs.Contains(stalled.seq)) sentAs.Add(stalled.seq);
            Say("No answer came (it may still have been done).", false);
            AskAboutStalled();
        }
        if (pending == null && stalled == null && Player != null && ((refreshAt >= 0f && now >= refreshAt) || now >= nextPoll))
            RefreshNow();
        if (statusUntil > 0f && now > statusUntil && status != null)
        {
            statusUntil = 0f;
            status.text = "";
        }
    }

    /// <summary>The question about a request no answer came for: send it again (it is done once) or leave it.</summary>
    private void AskAboutStalled()
    {
        if (stalled == null) return;
        if (confirm == null)
        {
            LetGo();
            return;
        }
        if (askingStalled && confirm.IsOpen) return;
        askingStalled = true;
        confirm.Show("No answer came for your last request: it may have been done already. Send it again? The housing office never does it twice.",
            "Send again", SendAgain, false, LetGo, "Leave it");
    }

    private void SendAgain()
    {
        askingStalled = false;
        HousingRequest r = stalled;
        stalled = null;
        if (r == null) return;
        if (Player == null)
        {
            sentAs.Clear();
            return;
        }
        Dispatch(r);
    }

    private void LetGo()
    {
        askingStalled = false;
        if (stalled == null) return;
        stalled = null;
        sentAs.Clear();
        Tell("Not sent again: the window shows what was done.");
        RefreshSoon();
    }

    private void OnAnswered(HousingReply reply)
    {
        if (reply == null) return;
        if (inRefresh || (refreshSeq != 0 && reply.seq == refreshSeq))
        {
            refreshAnswered = true;
            refreshSeq = 0;
            // Views refused ("The housing office is closed.", "Not so fast."): said unless the status line says something else.
            if (!reply.ok && pending == null && stalled == null && (HasView == null || !HasView() || Time.unscaledTime >= statusUntil))
                Say(reply.message, false);
            return;
        }
        HousingRequest r = pending ?? stalled;
        if (r == null) return;
        bool ours = reply.seq != 0 && (reply.seq == r.seq || sentAs.Contains(reply.seq));
        // A refusal of a request that could not be sent at all carries no number.
        if (!ours && !(reply.seq == 0 && !reply.ok && pending != null)) return;
        var kind = (HousingRequestKind)r.kind;
        bool wasStalled = stalled != null;
        pending = null;
        stalled = null;
        sentAs.Clear();
        if (wasStalled && askingStalled)
        {
            // A late answer answers the question about it too.
            askingStalled = false;
            CloseConfirm();
        }
        string text = !string.IsNullOrEmpty(reply.message) ? reply.message
            : !reply.ok ? "That cannot be done."
            : kind == HousingRequestKind.ReadNotices ? "Messages cleared." : "Done.";
        Say(text, reply.ok);
        RefreshSoon(AfterAnswerSeconds);
        try
        {
            Answered?.Invoke(kind, reply);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void OnBooksChanged()
    {
        // Where the rules run: a clearing, a stay that ended, a payout - the window follows (at most every few seconds).
        if (Player == null || refreshAt >= 0f) return; // fresh views are coming already
        float now = Time.unscaledTime;
        float at = Mathf.Max(now + 0.25f, booksAllowed);
        RefreshSoon(at - now);
        booksAllowed = at + BooksSeconds;
    }
}

/// <summary>
/// A part of a housing window made again only when what it shows changed (its signature: what it says, in one string).
/// Views that bring nothing new for it cost nothing and leave its buttons as they are - hovered, pressed, selected with
/// the keyboard or a gamepad - and what is typed in the window's fields is never touched. When it is made again, the
/// control that had the keyboard's selection is selected again if it is still there: found by the names of the parts
/// on the way down to it, never by its place. Rows come and go above it (someone let in, a player who came near), and
/// the control now at its old place may be another player's "Visitor" or "Remove", which act without asking - so the
/// rows of the lists are named after what they are for (<see cref="HousingUI.Named"/>: a player, someone let in, what
/// is kept, a lease, a place, a room, a house of the order). The selection stays with the same one, and when that one
/// is gone nothing is selected.
/// </summary>
internal sealed class HousingSection
{
    /// <summary>A step down to the selected control: a part's name, and how many shown parts before it have that name.</summary>
    private struct Step
    {
        public string name;
        public int index;
    }

    private readonly RectTransform root;
    private readonly bool hideEmpty;
    private readonly List<Step> path = new List<Step>();
    private string shown;
    private bool made;

    /// <summary>A section that fills <paramref name="root"/> (a list's content); with <paramref name="hideEmpty"/> it takes no room while it shows nothing.</summary>
    public HousingSection(RectTransform root, bool hideEmpty = false)
    {
        this.root = root;
        this.hideEmpty = hideEmpty;
    }

    /// <summary>A section of its own: a stack under <paramref name="parent"/> that takes no room while it shows nothing.</summary>
    public static HousingSection Under(Transform parent, string name, float spacing = 10f) =>
        new HousingSection(UITheme.Column(name, parent, 0, spacing), true);

    public RectTransform Root => root;

    /// <summary>
    /// Makes the section with <paramref name="build"/> (null: empty) unless it shows <paramref name="signature"/> already
    /// (null: made again every time); true when it was made again.
    /// </summary>
    public bool Show(string signature, Action<RectTransform> build)
    {
        if (root == null) return false;
        if (made && signature != null && signature == shown) return false;
        bool reselect = FindSelected();
        HousingUI.Clear(root);
        made = false;
        shown = null;
        try
        {
            build?.Invoke(root);
            made = true;
            shown = signature;
        }
        finally
        {
            if (hideEmpty)
            {
                bool any = HasContent();
                if (root.gameObject.activeSelf != any) root.gameObject.SetActive(any);
            }
            if (reselect) Reselect();
        }
        return true;
    }

    /// <summary>The next <see cref="Show"/> makes it again, whatever it shows.</summary>
    public void Forget()
    {
        made = false;
        shown = null;
    }

    private bool HasContent()
    {
        for (int i = 0; i < root.childCount; i++)
            if (root.GetChild(i).gameObject.activeSelf) return true;
        return false;
    }

    /// <summary>
    /// Where the keyboard's selection is in the section: the name of each part on the way down to it, and how many shown
    /// parts of that name come before it there. A row named after what it is for is the only one of its name, so rows
    /// coming or going above it change nothing; parts that share a name are the fixed pieces of a card (its header line,
    /// a row of buttons), told apart by their order.
    /// </summary>
    private bool FindSelected()
    {
        path.Clear();
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected == null || selected.transform == root || !selected.transform.IsChildOf(root)) return false;
        for (Transform t = selected.transform; t != root; t = t.parent)
        {
            Transform parent = t.parent;
            string name = t.name;
            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == t) break;
                if (c.gameObject.activeSelf && c.name == name) index++;
            }
            path.Insert(0, new Step { name = name, index = index });
        }
        return true;
    }

    /// <summary>
    /// Selects the control found by the same names. When it is gone (the player left, the grant was removed, what was
    /// kept was claimed) or may not be used now (the room was taken), nothing is selected: the selection never moves to
    /// another one's button.
    /// </summary>
    private void Reselect()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.alreadySelecting) return;
        Transform t = root;
        foreach (Step step in path)
        {
            Transform next = null;
            int seen = 0;
            for (int i = 0; i < t.childCount && next == null; i++)
            {
                Transform c = t.GetChild(i);
                // What was taken away is skipped (hidden at once, destroyed at the end of the frame), and so are other parts.
                if (!c.gameObject.activeSelf || c.name != step.name) continue;
                if (seen++ == step.index) next = c;
            }
            if (next == null) return;
            t = next;
        }
        Selectable s = t.GetComponent<Selectable>();
        if (s != null && s.isActiveAndEnabled && s.IsInteractable()) es.SetSelectedGameObject(s.gameObject);
    }
}

/// <summary>What a part of a window shows, written as one string: compared with the last one to know whether to make it again.</summary>
internal sealed class HousingSig
{
    private const char Separator = (char)0x1F;
    private readonly StringBuilder b = new StringBuilder(256);

    public HousingSig Add(string s)
    {
        b.Append(s ?? "").Append(Separator);
        return this;
    }

    public HousingSig Add(long v)
    {
        b.Append(v.ToString(CultureInfo.InvariantCulture)).Append(Separator);
        return this;
    }

    public HousingSig Add(double v)
    {
        b.Append(v.ToString("R", CultureInfo.InvariantCulture)).Append(Separator);
        return this;
    }

    public HousingSig Add(bool v)
    {
        b.Append(v ? '1' : '0').Append(Separator);
        return this;
    }

    /// <summary>Who was let in (name, fingerprint, rights, by whom).</summary>
    public HousingSig Add(List<GrantView> grants)
    {
        Add(grants != null ? grants.Count : -1);
        if (grants != null)
            foreach (GrantView g in grants)
                if (g != null) Add(g.name).Add(g.fingerprint).Add(g.access).Add(g.byMe);
        return this;
    }

    /// <summary>The players next to the player.</summary>
    public HousingSig Add(List<NearbyView> nearby)
    {
        Add(nearby != null ? nearby.Count : -1);
        if (nearby != null)
            foreach (NearbyView n in nearby)
                if (n != null) Add(n.playerId).Add(n.name).Add(n.fingerprint);
        return this;
    }

    public override string ToString() => b.ToString();
}

/// <summary>
/// Lines of text a part of a window shows, written down before they are made: what they say is the part's signature, so
/// it is made again only when a line changed (<see cref="HousingSection"/>).
/// </summary>
internal sealed class HousingLines
{
    private enum Kind : byte { Text, Header, Note }

    private struct Line
    {
        public Kind kind;
        public string text;
        public float size;
        public Color color;
        public FontStyles style;
    }

    private readonly List<Line> lines = new List<Line>();

    public int Count => lines.Count;

    public void Clear() => lines.Clear();

    public void Text(string text, float size = 16f, Color? color = null, FontStyles style = FontStyles.Normal) =>
        lines.Add(new Line { kind = Kind.Text, text = text ?? "", size = size, color = color ?? UITheme.Text, style = style });

    /// <summary>A smaller header (<see cref="HousingUI.SubHeader"/>).</summary>
    public void Header(string text) => lines.Add(new Line { kind = Kind.Header, text = text ?? "" });

    /// <summary>A quiet line (<see cref="HousingUI.Note"/>).</summary>
    public void Note(string text) => lines.Add(new Line { kind = Kind.Note, text = text ?? "" });

    public string Signature()
    {
        var s = new HousingSig();
        foreach (Line l in lines)
        {
            Color32 c = l.color;
            s.Add((int)l.kind).Add(l.size).Add((int)l.style).Add((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a).Add(l.text);
        }
        return s.ToString();
    }

    public void Build(RectTransform parent)
    {
        foreach (Line l in lines)
        {
            switch (l.kind)
            {
                case Kind.Header: HousingUI.SubHeader(parent, l.text); break;
                case Kind.Note: HousingUI.Note(parent, l.text); break;
                default: HousingUI.Text(parent, l.text, l.size, l.color, l.style); break;
            }
        }
    }
}
