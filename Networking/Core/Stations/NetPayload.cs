using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GameNet
{
    /// <summary>
    /// Text sent over the network that can be large (a workshop, a preparation, a crafted item's definition): UTF-8,
    /// compressed when that helps, cut into parts small enough for one message (<see cref="Split"/>) and put back
    /// together on the other side (<see cref="NetPayloadAssembler"/>). Unpacking is bounded (<see cref="MaxSize"/>), so a
    /// hostile message cannot make a machine allocate without limit. Pure: no scene, no network.
    /// </summary>
    public static class NetPayload
    {
        /// <summary>Largest part sent in one message (bytes; Fish-Net messages stay well under its 64 KB limit).</summary>
        public const int MaxPart = 48 * 1024;
        /// <summary>Largest text accepted once unpacked (bytes).</summary>
        public const int MaxSize = 8 * 1024 * 1024;
        /// <summary>Most parts one payload may have.</summary>
        public const int MaxParts = 200;
        /// <summary>Texts shorter than this are sent as they are (compressing them does not pay).</summary>
        public const int CompressAbove = 256;

        private const byte Plain = 0;
        private const byte Deflated = 1;

        /// <summary>The bytes of <paramref name="text"/>: a format byte, then UTF-8 (deflated when it is long).</summary>
        public static byte[] Pack(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text ?? "");
            if (raw.Length < CompressAbove)
            {
                var plain = new byte[raw.Length + 1];
                plain[0] = Plain;
                Buffer.BlockCopy(raw, 0, plain, 1, raw.Length);
                return plain;
            }
            using (var ms = new MemoryStream(raw.Length / 3 + 16))
            {
                ms.WriteByte(Deflated);
                using (var z = new DeflateStream(ms, CompressionLevel.Fastest, true))
                    z.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        /// <summary>The text of bytes made by <see cref="Pack"/>. False when they are not such bytes or unpack to more than <paramref name="maxSize"/>.</summary>
        public static bool TryUnpack(byte[] data, out string text, int maxSize = MaxSize)
        {
            text = null;
            if (data == null || data.Length < 1)
                return false;
            try
            {
                if (data[0] == Plain)
                {
                    if (data.Length - 1 > maxSize)
                        return false;
                    text = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                    return true;
                }
                if (data[0] != Deflated)
                    return false;
                using (var input = new MemoryStream(data, 1, data.Length - 1, false))
                using (var z = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[16 * 1024];
                    int n;
                    while ((n = z.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + n > maxSize)
                            return false;
                        output.Write(buffer, 0, n);
                    }
                    text = Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
                    return true;
                }
            }
            catch (Exception)
            {
                text = null;
                return false;
            }
        }

        /// <summary>Cuts <paramref name="data"/> into parts of at most <paramref name="partSize"/> bytes (at least one part, even when empty).</summary>
        public static List<byte[]> Split(byte[] data, int partSize = MaxPart)
        {
            var parts = new List<byte[]>();
            data = data ?? new byte[0];
            partSize = Math.Max(1, partSize);
            if (data.Length == 0)
            {
                parts.Add(new byte[0]);
                return parts;
            }
            for (int at = 0; at < data.Length; at += partSize)
            {
                int n = Math.Min(partSize, data.Length - at);
                var p = new byte[n];
                Buffer.BlockCopy(data, at, p, 0, n);
                parts.Add(p);
            }
            return parts;
        }
    }

    /// <summary>
    /// Puts the parts of payloads back together on the receiving side. Parts of one transfer arrive in order on a reliable
    /// channel; a part of a newer transfer drops an older one that never finished. Limits: <see cref="NetPayload.MaxParts"/>
    /// parts, <see cref="NetPayload.MaxPart"/> bytes each.
    /// </summary>
    public sealed class NetPayloadAssembler
    {
        private int transfer = int.MinValue;
        private int expected;
        private readonly List<byte[]> parts = new List<byte[]>();

        /// <summary>Is a transfer half received?</summary>
        public bool Busy => expected > 0 && parts.Count < expected;

        /// <summary>
        /// Adds part <paramref name="index"/> of <paramref name="count"/> of <paramref name="transferId"/>. True when
        /// that completed it (<paramref name="whole"/> is the payload); false while parts are missing or the part was
        /// not valid (out of order, too large, from an older transfer).
        /// </summary>
        public bool Add(int transferId, int index, int count, byte[] part, out byte[] whole)
        {
            whole = null;
            if (part == null || part.Length > NetPayload.MaxPart || count < 1 || count > NetPayload.MaxParts || index < 0 || index >= count)
                return false;
            if (transferId != transfer)
            {
                if (index != 0)
                    return false; // the start of this transfer was lost (or it is older): wait for a new one
                transfer = transferId;
                expected = count;
                parts.Clear();
            }
            if (count != expected || index != parts.Count)
            {
                Reset();
                return false;
            }
            parts.Add(part);
            if (parts.Count < expected)
                return false;
            int total = 0;
            foreach (byte[] p in parts) total += p.Length;
            whole = new byte[total];
            int at = 0;
            foreach (byte[] p in parts)
            {
                Buffer.BlockCopy(p, 0, whole, at, p.Length);
                at += p.Length;
            }
            Reset();
            return true;
        }

        /// <summary>Forgets a half received transfer.</summary>
        public void Reset()
        {
            parts.Clear();
            expected = 0;
        }
    }
}
