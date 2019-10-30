#!/bin/bash
mkdir data
echo "p
w
"|sudo fdisk /dev/sdc
echo "y
"|sudo mkfs -t ext4 /dev/sdc
sudo mount /dev/sdc $HOME/data
mkdir downloads && cd downloads
wget https://sacredialoanzanotifier.blob.core.windows.net/waresources/p2b-bin-ubuntu.18.04-x64.zip
sudo apt install unzip
unzip p2b-bin-ubuntu.18.04-x64.zip -d $HOME
cd $HOME/p2b-bin-ubuntu.18.04-x64
chmod +x AzureStorageUtilities.PageToBlockMover.BatchWorker
chmod +x azcopy
PATH=$PATH:$HOME/p2b-bin-ubuntu.18.04-x64
