using System;
using System.Globalization;

namespace BikiniWipeout.Core
{
    public static class TimeFormatter
    {
        /// <summary>Formats seconds as m:ss.cc (centiseconds), e.g. 1:07.42.</summary>
        public static string Format(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                seconds = 0f;
            }

            // Work in whole centiseconds so 59.999 rolls over to 1:00.00 instead of 0:60.00.
            long totalCentis = (long)Math.Floor(seconds * 100.0 + 0.0001);
            long minutes = totalCentis / 6000;
            long secs = totalCentis / 100 % 60;
            long centis = totalCentis % 100;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", minutes, secs, centis);
        }
    }
}
