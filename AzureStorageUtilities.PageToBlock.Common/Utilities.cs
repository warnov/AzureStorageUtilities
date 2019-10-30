using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageUtilities.PageToBlockMover.Common
{
    public static class Utilities
    {
        public static string Now2Log
        {
            get
            {
                return DateTime.UtcNow.ToOffsetShortDateTimeString(EnvironmentConfigurator.HoursOffset);
            }
        }

        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        // Credits to:  Shailesh N. Humbad https://www.somacon.com/p576.php
        public static string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }

        /// <summary>
        /// Returns the name (path) of the blob name after the first container
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string BlobNameByUrl(string url)
        {
            var components = url.Split("/");
            if (components.Length >= 3)
            {
                var containerName = components[3];
                var endContainderIdx = url.IndexOf(containerName) + containerName.Length + 1;
                return url.Substring(endContainderIdx);
            }
            else return string.Empty;
        }

        /// <summary>
        /// Returns the container name of the blob url
        /// </summary>
        /// <param name="url">the blob url</param>
        /// <returns></returns>
        public static string ContainerNameByUrl(string url)
        {
            var components = url.Split("/");
            if (components.Length >= 3)
            {
               return components[3];               
            }
            else return string.Empty;
        }




        // Wrapper to ListBlobsSegmentedAsync 
        // Credits: Ahmet Alp Balkan @ https://ahmet.im/blog/azure-listblobssegmentedasync-listcontainerssegmentedasync-how-to/
        public static async Task<List<IListBlobItem>> ListBlobsAsync(CloudBlobContainer container)
        {
            var continuationToken = new BlobContinuationToken();
            List<IListBlobItem> results = new List<IListBlobItem>();
            do
            {
                var response = await container.ListBlobsSegmentedAsync(string.Empty, true, BlobListingDetails.All, null, continuationToken, null, null);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);
            return results;
        }


        public static void Inform(int idx, int total, string message)
        {
            var originRow = Console.CursorTop;
            var originCol = Console.CursorLeft;
            Console.Write($"{idx}/{total} {message}");
            Console.SetCursorPosition(originCol, originRow);
        }       
    }
}
