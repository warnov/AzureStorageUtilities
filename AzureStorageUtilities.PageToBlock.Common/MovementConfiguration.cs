using System;

namespace AzureStorageUtilities.PageToBlockMover.Common
{
    public struct MovementConfiguration
    {
        public string SrcAccountConnectionString;
        public string SrcContainerName;
        public string DestAccountConnectionString;
        public string DestContainerName;
        public string SrcPattern;
        public string SrcExcludePattern; 
        public bool DeleteFromSource;
        public bool SafeDeleteFromSource;
        public bool DeleteFromLocalTemp;
        public bool OverwriteIfExists;
        public string DestTier;
        public string LocalTempPath;
        public string AzCopyPath;
        public string CustomerId;
        public string BatchId;        
    }
}
