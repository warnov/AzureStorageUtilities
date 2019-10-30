using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageUtilities.PageToBlockMover
{
    class Page2BlockMover
    {
        #region Fields
        //From params
        static string srcAccountConnectionString;
        static string srcContainerName;
        static string destAccountConnectionString;
        static string destContainerName;
        static string srcBlobName; //* for all the blobs in the container
        static bool deleteFromSource;
        static bool safeDeleteFromSource;
        static bool deleteFromLocalTemp;
        static string destTier;
        static string localTempPath;
        static string azCopyPath;
        static bool saveLog;
        static string logPath;

        //Internals
        static CloudStorageAccount srcStorageAccount, destStorageAccount;
        static string srcSAS, destSAS;
        static CloudBlobContainer srcContainer;
        static CloudBlobContainer destContainer;

        static List<CloudBlob> srcBlobs;
        static Process cmd;
        static long processedBytes;
        static DateTime startedTime;
        const long MB_FACTOR = 1024 * 1024;
        static string logFileName = string.Empty;
        static int currentBlobIndex;
        #endregion

        static void Main(string[] args)
        {
            //Params
            try
            {
                srcAccountConnectionString = args[0];
                srcContainerName = args[1];
                destAccountConnectionString = args[2];
                destContainerName = args[3];
                srcBlobName = args[4]; //* for all the blobs in the container
                deleteFromSource = bool.Parse(args[5]);
                safeDeleteFromSource = bool.Parse(args[6]);
                destTier = args[7];
                localTempPath = args[8];
                deleteFromLocalTemp = bool.Parse(args[9]);
                azCopyPath = args[10];
                saveLog = bool.Parse(args[11]);
                logPath = args[12];
            }
            catch
            {
                ReportError("Invalid Parameters. Check http://warnov.com/@page2block for use instructions");
                End();
                return;
            }

            _ = Execute().Result;
        }

        private static async Task<int> Execute()
        {
            if (SetupEnvironment())
            {
                await TransferFilesAsync();
            }
            Console.WriteLine("All operations completed");
            return End();
        }

        private static bool SetupEnvironment()
        {
            var lowerCapsTier = destTier.ToLower();
            if (lowerCapsTier == "hot" || lowerCapsTier == "cool" || lowerCapsTier == "archive")
            {
                destTier = $"{lowerCapsTier[0].ToString().ToUpper()}{lowerCapsTier.Substring(1)}";
            }
            else
            {
                ReportError("Invalid Tier");
                return false;
            }

            //Acquiring storage account objects
            try
            {
                srcStorageAccount = CloudStorageAccount.Parse(srcAccountConnectionString);
                destStorageAccount = CloudStorageAccount.Parse(destAccountConnectionString);
            }
            catch
            {
                ReportError("Invalid Connection String(s)");
                return false;
            }

            //Getting the SAS
            srcSAS = GetAccountSASToken(srcStorageAccount, true);
            destSAS = GetAccountSASToken(destStorageAccount, false);

            //Getting the src container object
            try
            {
                srcContainer = srcStorageAccount.CreateCloudBlobClient().GetContainerReference(srcContainerName);
                destContainer = destStorageAccount.CreateCloudBlobClient().GetContainerReference(destContainerName);
            }
            catch
            {
                ReportError("Invalid source container");
                return false;
            }

            //
            //Getting the blobs to transfer
            //            
            return GetBlobs();
        }

        static string GetAccountSASToken(CloudStorageAccount storageAccount, bool source)
        {
            var permissions = source ? SharedAccessAccountPermissions.Read |
                    SharedAccessAccountPermissions.List | SharedAccessAccountPermissions.Delete :
                    SharedAccessAccountPermissions.Create |
                    SharedAccessAccountPermissions.Write;

            // Create a new access policy for the account.
            SharedAccessAccountPolicy policy = new SharedAccessAccountPolicy()
            {
                Permissions = permissions,
                Services = SharedAccessAccountServices.Blob,
                ResourceTypes = SharedAccessAccountResourceTypes.Container |
                    SharedAccessAccountResourceTypes.Object,
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(96),
                Protocols = SharedAccessProtocol.HttpsOnly
            };
            // Return the SAS token.
            return storageAccount.GetSharedAccessSignature(policy);
        }

        private static bool GetBlobs()
        {
            srcBlobs = new List<CloudBlob>();
            if (srcBlobName == "*") //All the container
            {
                var containerContent = ListBlobsAsync(srcContainer).Result;
                foreach (var item in containerContent)
                {
                    if (item.GetType() != typeof(CloudBlobDirectory))
                        srcBlobs.Add((CloudBlob)item);
                }
            }
            else if (srcBlobName.Contains("->"))
            {
                var filePath = srcBlobName.Substring(2);
                var filePathLines = File.ReadLines(filePath);
                foreach (var line in filePathLines)
                {
                    AddBlobIfExists(line);
                }
            }
            else
            {
                if (!AddBlobIfExists(srcBlobName))
                {
                    ReportError("Invalid source blob name");
                    return false;
                }
            }
            return true;
        }

        private static bool AddBlobIfExists(string blobName)
        {
            var srcBlob = srcContainer.GetBlobReference(blobName);
            if (srcBlob.ExistsAsync().Result)
            {
                srcBlobs.Add(srcBlob);
                return true;
            }
            return false;
        }

        private async static Task TransferFiles()
        {
            var rule = "===========================================================================================================\n";
            var message = $"\n\n\n{rule}Starting the process of {srcBlobs.Count} blobs from " +
                $"{srcStorageAccount.BlobEndpoint.AbsoluteUri}{srcContainerName}/{srcBlobName}\n{ rule}";
            Console.WriteLine(message);
            //Log processing
            logFileName = $"{logPath}\\{DateTime.Now.ToShortDateString()}{DateTime.Now.ToShortTimeString()}page2BlockLog.txt";
            Append2Log($"Process started.");

            processedBytes = 0;
            startedTime = DateTime.Now;

            var currentBlobIndex = 0;
            foreach (var blob in srcBlobs)
            {
                //setup
                processedBytes += blob.Properties.Length;
                await blob.FetchAttributesAsync();

                //download               
                ShowDownloadInfo(blob, currentBlobIndex++);
                var downloadCommand = $"{azCopyPath} cp \"{blob.Uri}{srcSAS}\" \"{localTempPath}\\{blob.Name}\"";
                ExecuteAzCommand(downloadCommand);
                Append2Log($"Downloaded {blob.Uri.AbsoluteUri}");

                //upload                
                var destUrl = $"{destStorageAccount.BlobEndpoint.AbsoluteUri}{destContainerName}/{blob.Name}";
                message = $"Uploading {destTier} block blob to {destUrl}...";
                Console.WriteLine(message);
                Append2Log(message);
                var filePath = $"{ localTempPath }\\{ blob.Name}";
                var uploadCommand = $"{azCopyPath} cp {filePath} \"{destUrl}{destSAS}\" --block-blob-tier {destTier}";
                ExecuteAzCommand(uploadCommand);
                Append2Log($"Uploaded {destUrl}. Total processed: {processedBytes / MB_FACTOR}mb");


                //delete
                if (deleteFromSource)
                {
                    bool safe2Delete = !safeDeleteFromSource;
                    if (safeDeleteFromSource)
                    {
                        if (!(safe2Delete = destContainer.GetBlobReference(blob.Name).ExistsAsync().Result))
                            Console.WriteLine($"{blob.Uri} will not be deleted from source, because it is not yet in the destination");
                    }
                    if (safe2Delete)
                    {
                        Console.WriteLine($"{blob.Uri} will be deleted from source");
                        _ = blob.DeleteAsync();
                    }
                }
                if (deleteFromLocalTemp)
                {
                    File.Delete(filePath);
                }


                //finish current blob processing
                Console.WriteLine(".............................................................................");
            }
            message = $"\n\nProcessed finished.\n{processedBytes / MB_FACTOR}MB moved in {(DateTime.Now - startedTime).TotalMinutes} minutes";
            Console.WriteLine(message);
            Append2Log(message);
        }

        private async static Task TransferFilesAsync()
        {
            currentBlobIndex = 0;
            var rule = "===========================================================================================================\n";
            var message = $"\n\n\n{rule}Starting the process of {srcBlobs.Count} blobs from " +
                $"{srcStorageAccount.BlobEndpoint.AbsoluteUri}{srcContainerName}/{srcBlobName}\n{ rule}";
            Console.WriteLine(message);
            //Log processing
            logFileName = $"{logPath}\\{DateTime.Now.ToShortDateString()}{DateTime.Now.ToShortTimeString()}page2BlockLog.txt";
            Append2Log($"Process started.");

            processedBytes = 0;
            startedTime = DateTime.Now;

            foreach (var blob in srcBlobs)
            {
                await RunAzCopyAsync(blob);
            }
            message = $"\n\nProcessed finished.\n{processedBytes / MB_FACTOR}MB moved in {(DateTime.Now - startedTime).TotalMinutes} minutes";
            Console.WriteLine(message);
            Append2Log(message);
        }

        private async static Task<bool> RunAzCopyAsync(CloudBlob blob)
        {
            //setup
            processedBytes += blob.Properties.Length;
            await blob.FetchAttributesAsync();

            //download               
            ShowDownloadInfo(blob, currentBlobIndex++);
            var downloadCommand = $"{azCopyPath} cp \"{blob.Uri}{srcSAS}\" \"{localTempPath}\\{blob.Name}\"";
            ExecuteAzCommand(downloadCommand);
            //Append2Log($"Downloaded {blob.Uri.AbsoluteUri}");

            //upload                
            var destUrl = $"{destStorageAccount.BlobEndpoint.AbsoluteUri}{destContainerName}/{blob.Name}";
            var message = $"Uploading {destTier} block blob to {destUrl}...";
            Console.WriteLine(message);
            // Append2Log(message);
            var filePath = $"{ localTempPath }\\{ blob.Name}";
            var uploadCommand = $"{azCopyPath} cp {filePath} \"{destUrl}{destSAS}\" --block-blob-tier {destTier}";
            ExecuteAzCommand(uploadCommand);
            //Append2Log($"Uploaded {destUrl}. Total processed: {processedBytes / MB_FACTOR}mb");


            //delete
            if (deleteFromSource)
            {
                bool safe2Delete = !safeDeleteFromSource;
                if (safeDeleteFromSource)
                {
                    if (!(safe2Delete = destContainer.GetBlobReference(blob.Name).ExistsAsync().Result))
                        Console.WriteLine($"{blob.Uri} will not be deleted from source, because it is not yet in the destination");
                }
                if (safe2Delete)
                {
                    Console.WriteLine($"{blob.Uri} will be deleted from source");
                    _ = blob.DeleteAsync();
                }
            }
            if (deleteFromLocalTemp)
            {
                File.Delete(filePath);
            }


            //finish current blob processing
            Console.WriteLine(".............................................................................");
            return true;
        }

        private static void ShowDownloadInfo(CloudBlob blob, int currentBlobIndex)
        {
            var message =
            $"\n\n{currentBlobIndex + 1}/{srcBlobs.Count}:\n " +
                $"Downloading {blob.Name} " +
                $"({blob.Properties.Length / MB_FACTOR}mb - " +
                $"{blob.BlobType}) from " +
                $"{blob.Uri}";
            Console.WriteLine(message);
            Append2Log(message);
        }

        private static void ExecuteAzCommand(string cmdText)
        {
            AdjustCommand();
            cmd.StandardInput.WriteLine(cmdText);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            var output = CleanOutput(cmd.StandardOutput.ReadToEnd());
            Console.WriteLine(output);
        }

        private static void AdjustCommand()
        {
            cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
        }

        private static string CleanOutput(string output)
        {
            var lines = output.Split('\n');
            var cleanOutput = new StringBuilder();
            for (int i = 4; i < lines.Length - 3; i++)
            {
                cleanOutput.AppendLine(lines[i]);
            }
            return cleanOutput.ToString();
        }

        // Wrapper to ListBlobsSegmentedAsync 
        // Credits: Ahmet Alp Balkan @ https://ahmet.im/blog/azure-listblobssegmentedasync-listcontainerssegmentedasync-how-to/
        static async Task<List<IListBlobItem>> ListBlobsAsync(CloudBlobContainer container)
        {
            var continuationToken = new BlobContinuationToken();
            List<IListBlobItem> results = new List<IListBlobItem>();
            do
            {
                var response = await container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);
            return results;
        }

        private static void Append2Log(string message)
        {
            if (saveLog)
            {
                var text = $"{DateTime.Now.ToShortDateString()}{DateTime.Now.ToShortTimeString()}: {message}";
                File.AppendAllText(logFileName, text);
            }
        }

        private static void ReportError(string errorMessage)
        {
            Console.WriteLine(errorMessage);
        }

        private static int End()
        {
            Console.WriteLine("Press any key to finish...");
            Console.ReadKey();
            return 0;
        }
    }
}
