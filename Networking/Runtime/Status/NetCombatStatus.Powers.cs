#if FISHNET
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Powers (size, invisibility, mirage copies, sky walk, links, gates... see <see cref="ActivePowers"/>): the server
    /// runs them and sends their state to every client that sees the character whenever it changes (a power starts or
    /// ends, a copy vanishes). Each client's copy of the character then shows them: the body and colliders take the
    /// same size, the meshes hide, the copies and gates appear and move the same way, movement powers run in the
    /// owner's prediction. Late joiners get the last state.
    /// </summary>
    public partial class NetCombatStatus
    {
        private const int MaxPowerJson = 60000;

        private ActivePowers powers;
        private int powersSent = int.MinValue;
        private float powersNextSend;
        private static bool powerNetHooked;

        /// <summary>Lets powers name other characters by network id (a soul link's partner).</summary>
        private static void HookPowerNet()
        {
            if (powerNetHooked)
                return;
            powerNetHooked = true;
            PowerNet.IdOf = e =>
            {
                NetCombatStatus s = Of(e);
                return s != null ? s.ObjectId : -1;
            };
            PowerNet.Resolve = id =>
            {
                foreach (var kv in byEntity)
                    if (kv.Value != null && kv.Value.ObjectId == id)
                        return kv.Key;
                return null;
            };
        }

        // Server, every tick: sends the powers when they changed (at most 10 times a second).
        private void PollPowers()
        {
            HookPowerNet();
            if (entity == null)
                return;
            if (powers == null)
            {
                powers = entity.GetComponent<ActivePowers>();
                if (powers == null)
                    return; // added the first time a power is given
            }
            if (powers.Version == powersSent || Time.unscaledTime < powersNextSend)
                return;
            powersSent = powers.Version;
            powersNextSend = Time.unscaledTime + 0.1f;
            string json = powers.CaptureNet();
            if (json == null || json.Length > MaxPowerJson)
                return;
            ObserversPowers(json);
        }

        /// <summary>The powers of this character, to every client that sees it (the last state is kept for late joiners).</summary>
        [ObserversRpc(ExcludeServer = true, BufferLast = true)]
        private void ObserversPowers(string json)
        {
            HookPowerNet();
            if (entity == null || json == null || json.Length > MaxPowerJson)
                return;
            ActivePowers.For(entity).ApplyNet(json);
        }
    }
}
#endif
