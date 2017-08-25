using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FaceTag.Content
{
    /// <summary>
    /// Miscellaneous helps methods 
    /// </summary>
    public static class Utils
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Get the current time (from 01/01/1970 00:00:00) in ms  
        /// </summary>
        /// <returns></returns>
        public static long GetCurrentUnixTimestampMillis()
        {
            DateTime localDateTime, univDateTime;
            localDateTime = DateTime.Now;
            univDateTime = localDateTime.ToUniversalTime();
            return (long)(univDateTime - UnixEpoch).TotalMilliseconds;
        }

        /// <summary>
        /// Extension method for Matrix3x2 to conver to RawMatrix3x2
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
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
