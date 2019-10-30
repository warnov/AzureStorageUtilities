using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AzureStorageUtilities.PageToBlockMover.Common
{
    public static class EnvironmentConfigurator
    {
        const string LOGS_FOLDER = "p2blogs";
        const string DATA_FOLDER = "p2bdata";

        public static string ParamsTable = "page2blockparams";
        public static string ProgressTable = "page2blockprogress";
        public static string JobsQueuePrefix = "p2bjobs";
        public static int MaxMinutesPerDownload = 180;
        public static int HoursOffset = -5;
        public static int QueueWaitMinutes = 60;
        public static int LifeSignalMinutes = 5;
        public static string WorkingFolderRoot;


        public static PlatformID OS;

        //OSDependantVariables
        public static string HomePath;
        public static string WorkerIdShellCommand;
        public static string ShellPath;

        public static string LogsFolder
        {
            get
            {
                return Path.Combine(HomePath, LOGS_FOLDER);
            }
        }

        public static string DataFolder(string workingPath)
        {
            if (OnWindows)
            {
                if (workingPath.Contains(":"))
                    return Path.Combine(workingPath, DATA_FOLDER);
                else
                    return Path.Combine(HomePath, workingPath, DATA_FOLDER);
            }
            else
            {
                //It is required that when in Linux, if an additional disk is required,
                //that disk be mounted inside the home directory with the name of the working folder
                return Path.Combine(HomePath, workingPath, DATA_FOLDER);
            }
        }



        public static void SetConfiguration()
        {
            OS = Environment.OSVersion.Platform;
            if (OnWindows)
            {
                HomePath = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                WorkerIdShellCommand = "wmic csproduct get UUID";
                ShellPath = "cmd.exe";
                WorkingFolderRoot = string.Empty;
            }
            else
            {
                HomePath = Environment.GetEnvironmentVariable("HOME");
                WorkerIdShellCommand = "sudo cat /sys/class/dmi/id/product_uuid";
                ShellPath = "/bin/bash";
                WorkingFolderRoot = HomePath;
            }
        }

        public static bool OnWindows
        {
            get
            {
                return OS == PlatformID.Win32NT;
            }
        }
    }
}
