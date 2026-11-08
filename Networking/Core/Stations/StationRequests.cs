using System;
using System.Collections.Generic;

namespace GameNet
{
    /// <summary>
    /// A <see cref="CraftRequest"/> as it travels from a client to the server: plain values only (the item by its
    /// <see cref="ItemDatabase"/> id), with the client's number for it (<see cref="seq"/>: the server's answer carries it
    /// back). The server checks its shape, and the station bounds it with the player's <see cref="CraftRequestGuard"/>.
    /// </summary>
    [Serializable]
    public struct CraftNetRequest
    {
        public byte kind;
        /// <summary>The client's number for the request (0 = no answer wanted).</summary>
        public int seq;
        public string piece, other, id, itemId;
        public int count, i0, i1, i2;
        public float a, b, c, d;
        public bool flag;
        public byte location;
        public float[] points;

        public static CraftNetRequest From(in CraftRequest r, string itemId) => new CraftNetRequest
        {
            kind = (byte)r.kind,
            seq = r.seq,
            piece = r.piece ?? "",
            other = r.other ?? "",
            id = r.id ?? "",
            itemId = itemId ?? r.itemId ?? "",
            count = r.count,
            i0 = r.i0,
            i1 = r.i1,
            i2 = r.i2,
            a = r.a,
            b = r.b,
            c = r.c,
            d = r.d,
            flag = r.flag,
            location = (byte)r.location,
            points = r.points != null && r.points.Count > 0 ? r.points.ToArray() : null,
        };

        /// <summary>The request the station runs (empty strings become null, the item is named by <see cref="itemId"/>).</summary>
        public CraftRequest ToRequest() => new CraftRequest((CraftRequestKind)kind, Empty(piece))
        {
            seq = seq,
            other = Empty(other),
            id = Empty(id),
            itemId = Empty(itemId),
            count = count,
            i0 = i0,
            i1 = i1,
            i2 = i2,
            a = a,
            b = b,
            c = c,
            d = d,
            flag = flag,
            location = (CraftLocation)location,
            points = points != null && points.Length <= CraftRequestGuard.MaxPathFloats ? new List<float>(points) : null,
        };

        /// <summary>Small enough to look at (the guard checks the rest once it is a request).</summary>
        public bool IsWellFormed => points == null || points.Length <= CraftRequestGuard.MaxPathFloats;

        private static string Empty(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
