using System;

namespace GameNet
{
    /// <summary>
    /// The server's weather as sent to the clients (<c>NetWorldChunks</c>): the weather clock, the forced weather and the
    /// time of day. The weather itself is a function of place, world seed and weather time (see <see cref="WeatherSystem"/>),
    /// so this is all a client needs to show the same weather as the server everywhere. Plain public fields of serializable
    /// types, so Fish-Net generates its serializer.
    /// </summary>
    public struct WeatherSyncState : IEquatable<WeatherSyncState>
    {
        /// <summary>False until the server has a weather system to read (clients then keep their own clock).</summary>
        public bool valid;
        /// <summary>The server's weather time (seconds) when this was captured.</summary>
        public double weatherTime;
        /// <summary>Network time (server tick time, seconds) when this was captured, to run the clock forward on arrival.</summary>
        public double capturedAt;
        /// <summary>The world seed the server's weather follows (0 = unknown): a client in another world does not apply it.</summary>
        public int worldSeed;
        /// <summary>Hour of the day (0-24) of the weather's day cycle, or -1 for none.</summary>
        public float timeOfDay;
        /// <summary>True while the server forces the weather (<see cref="WeatherSystem.ForceWeather"/>).</summary>
        public bool forced;
        /// <summary>The forced weather kind (with <see cref="forced"/>).</summary>
        public WeatherType forcedType;
        /// <summary>The forced weather's strength, 0-1 (with <see cref="forced"/>).</summary>
        public float forcedStrength;
        /// <summary>Seconds the forced weather still lasts when captured; 0 = until the server clears it.</summary>
        public float forcedSecondsLeft;

        /// <summary>
        /// True when the parts a client must apply at once differ (validity, world, forcing, time of day) - the clock alone
        /// is only re-sent now and then, since both sides run it at the same rate.
        /// </summary>
        public bool DiffersBeyondClock(WeatherSyncState o) =>
            valid != o.valid || worldSeed != o.worldSeed || forced != o.forced || forcedType != o.forcedType ||
            Math.Abs(forcedStrength - o.forcedStrength) > 0.001f || Math.Abs(timeOfDay - o.timeOfDay) > 0.01f ||
            (forcedSecondsLeft > 0f) != (o.forcedSecondsLeft > 0f);

        public bool Equals(WeatherSyncState o) =>
            valid == o.valid && weatherTime.Equals(o.weatherTime) && capturedAt.Equals(o.capturedAt) && worldSeed == o.worldSeed &&
            timeOfDay.Equals(o.timeOfDay) && forced == o.forced && forcedType == o.forcedType &&
            forcedStrength.Equals(o.forcedStrength) && forcedSecondsLeft.Equals(o.forcedSecondsLeft);

        public override bool Equals(object obj) => obj is WeatherSyncState o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = valid ? 1 : 0;
                h = h * 397 ^ weatherTime.GetHashCode();
                h = h * 397 ^ capturedAt.GetHashCode();
                h = h * 397 ^ worldSeed;
                h = h * 397 ^ (forced ? 1 : 0);
                return h * 397 ^ (int)forcedType;
            }
        }
    }
}
