using SharpDX.Mathematics.Interop;
using System;
using System.Numerics;

namespace AssistantItemFinder.Content
{
    public static class Utils
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long GetCurrentUnixTimestampMillis()
        {
            DateTime localDateTime, univDateTime;
            localDateTime = DateTime.Now;
            univDateTime = localDateTime.ToUniversalTime();
            return (long)(univDateTime - UnixEpoch).TotalMilliseconds;
        }

        public static RawMatrix3x2 ToRawMatrix3x2(this Matrix3x2 self)
        {
            RawMatrix3x2 raw = new RawMatrix3x2();

            raw.M11 = self.M11;
            raw.M12 = self.M12;

            raw.M21 = self.M21;
            raw.M22 = self.M22;

            raw.M31 = self.M31;
            raw.M32 = self.M32;

            return raw;
        }        
    }
}
