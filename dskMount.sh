#!/bin/bash
echo "p
w
"|sudo fdisk /dev/sdc
echo "y
"|sudo mkfs -t ext4 /dev/sdc
sudo mount /dev/sdc $HOME/data
df -k
cd ~/p2b-bin-ubuntu.18.04-x64
sudo env "PATH=$PATH" ./AzureStorageUtilities.PageToBlockMover.BatchWorker "DefaultEndpointsProtocol=https;AccountName=siigowebha;AccountKey=v89MdFDcH796TGWLUeEYRKtKt7g5DBP3J2lxng1QFWq4rxt
Hj9eViG1iWxXmgokAi/lrCxuo7l7WJJaxeSpLPQ==;EndpointSuffix=core.windows.net" SIIGO 82c8c2cb-4fef-4a77-92b1-11801ff0b70a
