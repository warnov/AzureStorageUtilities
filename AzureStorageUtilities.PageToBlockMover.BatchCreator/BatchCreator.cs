using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Queue;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AzureStorageUtilities.PageToBlockMover.Common;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace AzureStorageUtilities.PageToBlockMover.BatchCreator
{

    class BatchCreator
    {
        #region Internal Fields
        static MovementConfiguration conf;
        static DynamicTableEntity dynaConfig;
        static CloudStorageAccount srcStorageAccount;
        static CloudBlobContainer srcContainer;
        static List<CloudBlob> srcBlobs;
        static string batchId;
        #endregion

        static void Main(string[] args)
        {

            string option = CheckArguments(args);
            switch (option)
            {
                case "version":
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

                    Console.WriteLine($"AzureStorageUtilities.PageToBlockMover.BatchCreator V{fvi.FileVersion}");
                    break;
                default://execute                  
                    MainExecution(args);
                    break;
            }

        }

        private static void MainExecution(string[] args)
        {
            EnvironmentConfigurator.SetConfiguration();
            //Params
            try
            {
                Console.WriteLine("Page2Block batch creator started...");
                ReadArguments(args);
                Execute();
            }
            catch
            {
                ReportError("Invalid Parameters. Check http://warnov.com/@page2block for use instructions");
                End();
            }
        }

        private static string CheckArguments(string[] args)
        {
            if (args[0].Contains("--"))
            {
                return args[0].Substring(2);
            }
            else
                return "execute";
        }

        private static void ReadArguments(string[] args)
        {
            //Read from line arguments
            conf.SrcAccountConnectionString = args[0];
            conf.SrcContainerName = args[1];
            conf.DestAccountConnectionString = args[2];
            conf.DestContainerName = args[3];
            //* for all the blobs in the container >[filename] For a file with the list of names  or #[n] where n is the first n blobs to get
            conf.SrcPattern = args[4];
            conf.SrcExcludePattern = args[5];
            conf.DeleteFromSource = bool.Parse(args[6]);
            conf.SafeDeleteFromSource = bool.Parse(args[7]);
            conf.DestTier = args[8];
            conf.LocalTempPath = args[9];
            conf.DeleteFromLocalTemp = bool.Parse(args[10]);
            conf.OverwriteIfExists = bool.Parse(args[11]);
            conf.AzCopyPath = args[12];
            conf.CustomerId = args[13];
        }

        private static void Execute()
        {
            SetupEnvironment();
            GenerateJobsQueue();
            Console.WriteLine("All operations completed");
            End();
        }

        private static void SetupEnvironment()
        {
            //Acquiring storage account objects
            try
            {
                srcStorageAccount = CloudStorageAccount.Parse(conf.SrcAccountConnectionString);
                _ = CloudStorageAccount.Parse(conf.DestAccountConnectionString);
                Console.WriteLine("Storage Accounts Validated...");
            }
            catch
            {
                ReportError("Invalid Connection String(s)");
            }

            //Generate Parameters Record
            GenerateParametersRecord();
        }

        /// <summary>
        /// Creates a record in a table named page2blockparams inside the source storage account
        /// </summary>
        private static void GenerateParametersRecord()
        {
            //Initializing the configuration record with today's date and batch GUID
            batchId = Guid.NewGuid().ToString();
            dynaConfig = new DynamicTableEntity()
            {
                PartitionKey = conf.CustomerId,
                RowKey = batchId
            };
            FillDynaConfig();

            var tableClient = srcStorageAccount.CreateCloudTableClient();
            //This is just fired. No need to wait as we are just creating the table in case it doesn't exist
            _ = tableClient.GetTableReference(EnvironmentConfigurator.ProgressTable).CreateIfNotExistsAsync();
            //We need to wait for this table to be created, as we are going to work with this
            var paramsTable = tableClient.GetTableReference(EnvironmentConfigurator.ParamsTable);
            _ = paramsTable.CreateIfNotExistsAsync().Result;


            TableOperation insertOperation =
                TableOperation.Insert(dynaConfig);
            _ = paramsTable.ExecuteAsync(insertOperation).Result;
            Console.WriteLine("Param tables created or verified and filled...");
        }

        private static void FillDynaConfig()
        {
            dynaConfig.Properties.Add("SrcAccountConnectionString",
                EntityProperty.GeneratePropertyForString(conf.SrcAccountConnectionString));
            dynaConfig.Properties.Add("SrcContainerName",
                EntityProperty.GeneratePropertyForString(conf.SrcContainerName));
            dynaConfig.Properties.Add("DestAccountConnectionString",
                EntityProperty.GeneratePropertyForString(conf.DestAccountConnectionString));
            dynaConfig.Properties.Add("DestContainerName",
                EntityProperty.GeneratePropertyForString(conf.DestContainerName));
            dynaConfig.Properties.Add("SrcBlobName",
                EntityProperty.GeneratePropertyForString(conf.SrcPattern));  //* for all the blobs in the container
            dynaConfig.Properties.Add("DeleteFromSource",
                EntityProperty.GeneratePropertyForBool(conf.DeleteFromSource));
            dynaConfig.Properties.Add("SafeDeleteFromSource",
                EntityProperty.GeneratePropertyForBool(conf.SafeDeleteFromSource));
            dynaConfig.Properties.Add("DestTier",
                EntityProperty.GeneratePropertyForString(conf.DestTier));
            dynaConfig.Properties.Add("LocalTempPath",
                EntityProperty.GeneratePropertyForString(conf.LocalTempPath));
            dynaConfig.Properties.Add("DeleteFromLocalTemp",
                EntityProperty.GeneratePropertyForBool(conf.DeleteFromLocalTemp));
            dynaConfig.Properties.Add("AzCopyPath",
               EntityProperty.GeneratePropertyForString(conf.AzCopyPath));
            dynaConfig.Properties.Add("CustomerId",
               EntityProperty.GeneratePropertyForString(conf.CustomerId));
            dynaConfig.Properties.Add("OverwriteIfExists",
              EntityProperty.GeneratePropertyForBool(conf.OverwriteIfExists));
        }

        private static void GenerateJobsQueue()
        {
            srcContainer = srcStorageAccount.CreateCloudBlobClient()
                .GetContainerReference(conf.SrcContainerName);
            GetBlobs();
            InsertBlobsInQueue();
        }

        private static void GetBlobs()
        {
            srcBlobs = new List<CloudBlob>();
            var exclusionsRegexpPresent = !string.IsNullOrEmpty(conf.SrcExcludePattern);
            var inclusionRegexpPresent = false;
            var inclusionRegexp = string.Empty;

            //Getting all the blobs
            switch (conf.SrcPattern.Substring(0, 1))
            {
                case "*": //Used for all the blobs, of a regexp (the regexp will be the text following the *
                    AddNBlobs(0);
                    if (inclusionRegexpPresent = conf.SrcPattern.Length > 1)
                        inclusionRegexp = conf.SrcPattern.Substring(1);
                    break;
                case ">":
                    var filePath = conf.SrcPattern.Substring(1);
                    var filePathLines = File.ReadLines(filePath);
                    foreach (var line in filePathLines)
                    {
                        AddBlobIfExists(line);
                    }
                    break;
                case "#":
                    var blobAmount = int.Parse(conf.SrcPattern.Substring(1));
                    AddNBlobs(blobAmount);
                    break;
                default:
                    if (!AddBlobIfExists(conf.SrcPattern))
                    {
                        ReportError("Invalid source blob name");
                    }
                    break;
            }

            //Substracting the blobs meeting the exclusions regexp        
            if (exclusionsRegexpPresent)
            {
                EvaluateRegexpAndDeleteIfApplies(conf.SrcExcludePattern, true);
            }

            //Substracting the blobs not meeting the exclusions regexp
            if (inclusionRegexpPresent)
            {
                EvaluateRegexpAndDeleteIfApplies(inclusionRegexp, false);
            }
        }

        /// <summary>
        /// Checks if a blob name meet the requirements of the given regular expression
        /// (it could be a regex for exclude the blob or to include it. Depending on it, the blob will 
        /// be excluded from the list if it meets the requirement to exclude or if it doesn't meet\
        /// the requirement to include)
        /// </summary>
        /// <param name="regexp"></param>
        /// <param name="exclusions">Determines if we are handling exclusions or inclusions</param>
        private static void EvaluateRegexpAndDeleteIfApplies(string regexp, bool exclusions)
        {
            var remainingBlobs = srcBlobs.Count;
            for (int i = 0; i < remainingBlobs; i++)
            {
                var blob = srcBlobs[i];
                if (Regex.Match(blob.Name, regexp).Captures.Count > 0 == exclusions)
                {
                    srcBlobs.Remove(blob);
                    remainingBlobs--;
                    i--;
                }
            }
        }

        private static void AddNBlobs(int blobAmount)
        {
            var containerContent = Utilities.ListBlobsAsync(srcContainer).Result;
            if (blobAmount == 0) blobAmount = containerContent.Count;
            for (int i = 0; i < blobAmount; i++)
            {
                var item = containerContent[i];
                if (item.GetType() != typeof(CloudBlobDirectory))
                    srcBlobs.Add((CloudBlob)item);
            }
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

        /// <summary>
        /// Unserts all the blobs names in a queue calle page2blockjobs
        /// Each blob, is a queue message
        /// Each message has a jobid (the original message ID) and a parent batch id
        /// </summary>
        private static void InsertBlobsInQueue()
        {
            var queueClient = srcStorageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference($"{EnvironmentConfigurator.JobsQueuePrefix}-{batchId}");
            var queueCreated = queue.CreateIfNotExistsAsync().Result;
            var totalBlobs = srcBlobs.Count;
            Console.WriteLine($"Adding {totalBlobs} blobs to process: ");
            var idx = 1;
            foreach (var blob in srcBlobs)
            {
                var message = new CloudQueueMessage(blob.Uri.AbsoluteUri);
                queue.AddMessageAsync(message).Wait();
                Utilities.Inform(idx++, totalBlobs, "Messages added to the queue");
            }
            Console.WriteLine();
        }

        private static int End()
        {
            Console.WriteLine($"Batch id {batchId} created and ready to be processed.");
            Console.WriteLine("Press any key to finish...");
            Console.ReadKey();
            return 0;
        }

        private static void ReportError(string errorMessage)
        {
            Console.WriteLine(errorMessage);
        }
    }
}
