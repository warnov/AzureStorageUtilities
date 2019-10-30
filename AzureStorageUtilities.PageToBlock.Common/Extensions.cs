using System;
using System.Collections.Generic;
using System.Text;

namespace AzureStorageUtilities.PageToBlockMover.Common
{
    public static class Extensions
    {
        public static string ToOffsetShortDateTimeString(this DateTime now, int offsetHours)
        {
            DateTime offsetted = now + TimeSpan.FromHours(offsetHours);
            return $"{offsetted.ToShortDateString()} {offsetted.ToShortTimeString()}";
        }
    }
}
