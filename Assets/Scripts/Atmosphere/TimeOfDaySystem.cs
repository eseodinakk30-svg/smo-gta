using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Atmosphere
{
    /// <summary>
    /// The San Monica clock. Drives lighting, traffic and pedestrian rhythms,
    /// shop opening hours, police presence and the radio schedule.
    /// </summary>
    public class TimeOfDaySystem : MonoBehaviour
    {
        [Header("Clock")]
        [Range(0f, 24f)] public float TimeOfDay = 8.5f;
        public int Day = 1;
        [Tooltip("Real seconds for one in-game hour.")]
        public float SecondsPerHour = 60f;
        public bool Paused;

        public int Hour => Mathf.FloorToInt(TimeOfDay) % 24;
        public int Minute => Mathf.FloorToInt((TimeOfDay - Mathf.Floor(TimeOfDay)) * 60f);
        public float GameHoursDelta { get; private set; }
        public float NormalisedTime => TimeOfDay / 24f;

        public bool IsNight => TimeOfDay < 6.2f || TimeOfDay > 19.6f;
        public bool IsDawn => TimeOfDay >= 5.4f && TimeOfDay < 7.4f;
        public bool IsDusk => TimeOfDay >= 18.4f && TimeOfDay < 20.4f;
        public bool HeadlightsRequired => TimeOfDay < 6.8f || TimeOfDay > 18.8f ||
                                          (Services.Weather != null && Services.Weather.VisibilityScale < 0.72f);

        public string ClockText => Hour.ToString("00") + ":" + Minute.ToString("00");

        private int _lastHour = -1;

        private void Update()
        {
            if (Paused) { GameHoursDelta = 0f; return; }
            float hours = Time.deltaTime / Mathf.Max(1f, SecondsPerHour);
            GameHoursDelta = hours;
            TimeOfDay += hours;
            while (TimeOfDay >= 24f) { TimeOfDay -= 24f; Day++; }

            if (Hour != _lastHour)
            {
                _lastHour = Hour;
                GameEvents.RaiseHourChanged(Hour);
            }
        }

        public void SkipHours(float hours)
        {
            TimeOfDay += hours;
            while (TimeOfDay >= 24f) { TimeOfDay -= 24f; Day++; }
            GameEvents.RaiseHourChanged(Hour);
        }

        public void SetTime(float hour, int day = -1)
        {
            TimeOfDay = Mathf.Repeat(hour, 24f);
            if (day > 0) Day = day;
            _lastHour = Hour;
            GameEvents.RaiseHourChanged(Hour);
        }

        /// <summary>Sun elevation in degrees above the horizon.</summary>
        public float SunElevation => Mathf.Sin((TimeOfDay - 6f) / 12f * Mathf.PI) * 78f;

        /// <summary>0 at midnight, 1 at noon - used to blend lighting and ambience.</summary>
        public float DaylightAmount => Mathf.Clamp01(Mathf.Sin(Mathf.Max(0f, (TimeOfDay - 5.2f) / 13.6f) * Mathf.PI) * 1.15f);
    }
}
