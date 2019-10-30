using Microsoft.WindowsAzure.Storage;
using AzureStorageUtilities.PageToBlockMover.Common;

using System;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;
using System.IO;
using AzureStorageUtlities.MultipleFilesDownloader;

namespace AzureStorageUtilities.Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            /*var connectionString = args[0];
            var containerName = args[1];
            var reportPath = args[2];

            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            var blobClient = account.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);
            var blobs = Utilities.ListBlobsAsync(container).Result;
            var totalBlobs = blobs.Count;
            Console.WriteLine($"{totalBlobs} blobs to process");
            var idx = 1;
            long totalSize = 0;
            var report = new StringBuilder();
            foreach (CloudBlob blob in blobs)
            {
                var size = blob.Properties.Length;
                totalSize += size;
                report.AppendLine($"{blob.Name};{blob.Uri.AbsolutePath};{size}");
                Utilities.Inform(idx++, totalBlobs, "Blobs processed");
            }
            Console.WriteLine($"{totalBlobs} blobs processed. Total size {totalSize} ==> {Utilities.GetBytesReadable(totalSize)}");
            File.WriteAllText(reportPath, report.ToString());
            Console.WriteLine($"Report written to {reportPath}");*/
            FileListDownloader.DownloadFileList(@"c:\tmp\quidecahabeas.txt", @"c:\tmp\quideca\habeas");
           /* var files = Directory.GetFiles(@"c:\tmp\quideca\consentimiento");
            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.AppendLine(Path.GetFileName(file));
            }
            File.WriteAllText(@"c:\tmp\quideca\consentimientofinalfiles.txt", sb.ToString());*/
        }
    }
}
