using Microsoft.WindowsAzure.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AzureStorageUtlities.MultipleFilesDownloader
{
    public static class FileListDownloader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileListPath">Has the route of a tab sepparated values file in which each row contains the url in the blob storage and in the second column, has the name of the filen once downloaded</param>        
        public static void DownloadFileList(string fileListPath, string destinyPath)
        {
            
            if (!Directory.Exists(destinyPath))
            {
                Directory.CreateDirectory(destinyPath);
            }
            var storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureStorageConnectionString"));
            var blobClient = storageAccount.CreateCloudBlobClient();


            string[] registries = File.ReadAllLines(fileListPath);
             Parallel.ForEach(
                 registries,
                 urlNamePair =>
                 {
            //foreach (var urlNamePair in registries)
            //{
                var components = urlNamePair.Split('\t');
                var url = components[0];
                var fileName = components[1];
                var blob = blobClient.GetBlobReferenceFromServerAsync(new Uri(url)).Result;
                blob.DownloadToFileAsync(Path.Combine(destinyPath, fileName), FileMode.CreateNew);
            }

            );
        }
    }
}
