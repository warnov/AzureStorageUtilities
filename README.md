# AzureStorageUtilities
Some *dotnet core* programs to work with Azure Storage

## 1. PageToBlockMover
*Want to archive page Azure blobs to save costs? This tool is useful for this.*
But in general, it automates the movement of blobs from one Azure Storage Account to another (or inside the same account). The tool uses *AzCopy 10* to offer better performance in the file transmission. So please be sure to have it [installed](http://warnov.com/@AzCopy10) in your machine.
### Problem
Page blobs cannot use access tiers. So you need to copy them as block blobs in a *blob storage account* (this type of storage account is the only that allows you to have tiers for your block blobs). In this copy process, you can specify if you want your blobs copied in *hot, cool* or *archive* access tier. You can also specify if you want to delete the original blob.

So this program takes one or all the blobs inside a container, downloads them to the hard disk and then upload them to the required destination, in the selected access tier. 

### Use Case:
You have TBs or even PBs of backups in page blobs (maybe from virtual disks) and you need to keep them but saving costs. Then, you should execute this tool inside a VM on the same region as the data you want to move. In this way download and upload will be very fast and you won't be charged for data transfer-out costs.

### Solution Structure:
Passing the right arguments to the tool:

 1. *AzureStorageUtilities.PageToBlockMover.Common*: Cross utilities
 2. *AzureStorageUtilities.PageToBlockMover.BatchCreatorSource*: Creates the jobs in an Azure queue storage so multiple workers can access it and process the container in parallel (the nature of AzCopy uses all the thread available in a given machine, so no sense trying to download many files in parallel inside the same execution environment. ie. a Virtual Machine)
 3. *AzureStorageUtilities.PageToBlockMover.BatchWorkerDestiny*: The worker process that checks the queue for download/upload jobs (basically the message contains the url of the blob to be processed. It downloads it, an then uploads it in the new format). You can have many workers running in parallel to empty the queue faster. Note that using Azure Batch or Containers doesn't make much sense here, as the bandwidth associated to the batch or the containers is distributed among them, so this will not increase the downloading speed. Thus, it is recommended to use pure VMs or VM Scale Sets so each VM has its own bandwidth. Also please be sure to assign enough disk to the VMs accordingly to the size of the individual blobs you need to process. ie. if you have blobs with 200GB in size you would want attaching an additional disk of this size to your machine. A size of 513GB is recommended in this case, as it will give you the best IOPS without being too costly. In general, a DSV2 machine is recommended for each worker because they are the cheaper machines with Accelerated Networking enabled and gave good results downloading files. Using bigger machines with more cores doesn't improve too much the efficiency as the bandwidth assigned to each VM to download/upload files doesn't increase with their size.

 *AzureStorageUtilities.PageToBlockMover* is deprecated as it is an experimental previous version.
 *AzureStorageUtilities.PageToBlockMover.BatchCreator.Linux* is a work in progress as the whole solution has been created using dotnet core 3.0.


### Invocation sample:

#### Passing the right arguments to the creator:

 1. Source Azure Storage Connection String 
 2. Source container with the blobs you want to transfer to another container in block type
 3. Destiny Azure Storage Connection String 
 4. Destiny container
 5. Name of the source blob you want to transfer *(**\*** if you want to transfer all the container's blob)*. Use ->FilePath if passing a txtfile with one line for each blob name inside the container you want to move. Example: ->c:\tmp\blobstomove.txt. You can also pass *#n*; where n is a number of a specific amount of blobs to process (the program then will take n random blobs for the process)
 6. Indicate if you want the original blobs to be deleted *(False|True)*
 7. Indicate if you want the tool to make sure the blob is already copied to destiny before it is deleted from source *(False|True)*
 8. Destination tier for the blob *(Archive|Cool|Hot)* 
 9. Local Path to store the temporary files 
 10. Indicate if you want to delete the local temporary files used for the transfer once each blob is copied to destiny
 11. AzCopy's path
 12. Job partition (you can differentiate many distinct jobs with it and have different parameters for each one)
 

##### In the console:

    dotnet AzureStorageUtilities.PageToBlockMover <[connectionString1>] pagebackups <[connectionString2>] blockbackups * false archive c:\tmp\backups AzCopy JuneBlobsOptimization

#### Passing the right arguments to the worker:

 1. Source Azure Storage Connection String 
 2. Job partition (you can differentiate many distinct jobs with it and have different parameters for each one)
 3. Job GUID (the creator gives a GUID for each batch job and then stores this job in the queue. A related Azure Table is created and populated with each of the blobs to be downloaded in that batch job having the same GUID as partition key) 
 

##### In the console:

    dotnet AzureStorageUtilities.PageToBlockMover <[connectionString1>] JuneBlobsOptimization 14583f47-e62e-4793-8546-0722fe3f9c66
<!--stackedit_data:
eyJoaXN0b3J5IjpbMTA5ODE4MTM2MSwyMDk1NjMyODk0XX0=
-->