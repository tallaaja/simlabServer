﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Drawing;
using System.IO;
using ARToolServer;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Concurrent;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

public enum PROTOCOL_CODES
{
    ERROR = -1, ERROR_NO_DBCONNECTION
    ,ACCEPT, DENY, SENDIMAGE, SENDVIDEO, SENDJSON, SENDLOCATION, SENDMESSAGE, QUIT, OK
        
    ,GET_MY_CONTENTPACKS, SEARCH_CONTENT_PACKS, SEARCH_CONTENTPACKS_BY_USER
    ,GET_SERIES_IN_PACKAGE,GET_VIDEOS_IN_SERIES
    ,REQUEST_VIEW_VIDEO, REQUEST_EDIT_VIDEO
    ,POST_EDITS,UPLOAD_VIDEO, UPLOAD_ASSETPACKAGE

    ,KEEPALIVE_SIGNAL
};

public enum STATUS
{
    ERROR = -1,RUNNING,ENDED,QUIT
};

public enum REQUEST_RESULT
{
    ERROR = -1,OK,QUIT
};


public class Server
{
    ConcurrentBag<int> cb = new ConcurrentBag<int>();
    List<Client> clients = new List<Client>();
    TcpListener serverSocket;
    TcpListener serverPingSocket;
    TcpClient clientSocket;
    TcpClient clientPingSocket;
    databaseConnection db;

    int max_acceptedSend = int.MaxValue;

    public STATUS status = STATUS.ERROR;


    List<string>[] allContentPacks;

    public void removeClient(string name)
    {
        lock (clients)
        {
            List<Client> clientsCopy = new List<Client>(clients);
            for (int i = 0; i < clientsCopy.Count; i++)
            {
                if (clientsCopy[i].clientName == name)
                {

                    
                    clients[i].clientSocket.Close();
                    clients[i].pingSocket.Close();
                    clients.RemoveAt(i);
                    Console.WriteLine("Removed client: " + i);
                    try
                    {
                        
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("error sulkialihaksessa");
                    }
                }
            }
        }
    }

    public void sendPings()
    {

        while (true)
        {
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            List<Client> clientsCopy = new List<Client>(clients);
            foreach (Client c in clientsCopy)
            {
                if (c.Sendping() == false)
                {
                    removeClient(c.clientName);
                    
                }

            }

            Int32 unixTimestamp2 = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            //Console.WriteLine(2000 - (unixTimestamp - unixTimestamp2) + " c count: " + clients.Count);
            Thread.Sleep(2000 - (unixTimestamp - unixTimestamp2));
        }
    }



    void startServing()
    {
        serverSocket = new TcpListener(IPAddress.Any, 8052);
        serverPingSocket = new TcpListener(IPAddress.Any, 8051);
        clientSocket = default(TcpClient);
        serverSocket.Start();
        serverPingSocket.Start();

        Console.WriteLine(" >> " + "Server Started");

        db = new databaseConnection();
        db.updateVideoEdits("1","{\"name\": \"Afzaal Ahmad Zeeshan\",\"age\": 20}");
        db.getVideoEdits("1");
        Console.WriteLine(db.getVideoEdits("1"));
        Thread pinging = new Thread(sendPings);
        pinging.Start();
        while (true)
        {
            clientSocket = serverSocket.AcceptTcpClient();
            clientPingSocket = serverPingSocket.AcceptTcpClient();          

            Client client = new Client(max_acceptedSend, this);
            lock (clients)
            {
                clients.Add(client);
                Console.WriteLine(" >> " + "Client No:" + clients.Count + " started!");
                client.StartClient(clientSocket, clientPingSocket, Convert.ToString(clients.Count)); //start servering the client
            }
            clientSocket = null;
            clientPingSocket = null;
        }
    }

    void stop()
    {
        foreach (Client c in clients)
        {
            //TODO: proper quit
            c.clientSocket.Close();
        }
        serverSocket.Stop();
    }





    static void Main(string[] args)
    {
        
        Server server = new Server();
        Thread servingThread = new Thread(server.startServing);
        servingThread.Start();


        while (true)
        {//TODO: server querying interface

            Thread.Sleep(100);

        }

        Console.WriteLine(" >> " + "exit");
        server.stop();
        Console.ReadLine();
        
    }

    void UseAccountSAS(string sasToken)
    {
        // Create new storage credentials using the SAS token.
        StorageCredentials accountSAS = new StorageCredentials(sasToken);
        // Use these credentials and the account name to create a Blob service client.
        CloudStorageAccount accountWithSAS = new CloudStorageAccount(accountSAS, "simlabitvideos", endpointSuffix: null, useHttps: true);
        CloudBlobClient blobClientWithSAS = accountWithSAS.CreateCloudBlobClient();

        // Now set the service properties for the Blob client created with the SAS.
        blobClientWithSAS.SetServiceProperties(new ServiceProperties()
        {
            HourMetrics = new MetricsProperties()
            {
                MetricsLevel = MetricsLevel.ServiceAndApi,
                RetentionDays = 7,
                Version = "1.0"
            },
            MinuteMetrics = new MetricsProperties()
            {
                MetricsLevel = MetricsLevel.ServiceAndApi,
                RetentionDays = 7,
                Version = "1.0"
            },
            Logging = new LoggingProperties()
            {
                LoggingOperations = LoggingOperations.All,
                RetentionDays = 14,
                Version = "1.0"
            }
        });

        // The permissions granted by the account SAS also permit you to retrieve service properties.
        ServiceProperties serviceProperties = blobClientWithSAS.GetServiceProperties();
        Console.WriteLine(serviceProperties.HourMetrics.MetricsLevel);
        Console.WriteLine(serviceProperties.HourMetrics.RetentionDays);
        Console.WriteLine(serviceProperties.HourMetrics.Version);
    }
    /*public string generateSASkeytoWatch(string uploader, string filename)
    {
        CloudStorageAccount storageAccount;
        CloudBlobContainer cloudBlobContainer;

        // Check whether the connection string can be parsed.
        if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
        {
            // If the connection string is valid, proceed with operations against Blob storage here.

            // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

            // Create a container and append a GUID value to it to make the name unique. 
            cloudBlobContainer = cloudBlobClient.GetContainerReference(uploader.ToLower());
            //cloudBlobContainer.CreateIfNotExists();
            
            foreach (var blob in cloudBlobContainer.ListBlobs())
            {
                Console.WriteLine(blob.Uri);
            }

            

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Container
            };

            var storedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-1),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(10),
                Permissions = SharedAccessBlobPermissions.Read |
                  SharedAccessBlobPermissions.Write |
                  SharedAccessBlobPermissions.List
            };
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
            };

            // add in the new one
            permissions.SharedAccessPolicies.Add(policyName, storedPolicy);
            permissions.SharedAccessPolicies.Add(policyName + "access", accessPolicy);
            // save back to the container
            cloudBlobContainer.SetPermissions(permissions);

            //CloudBlobDirectory dir = cloudBlobContainer.GetDirectoryReference("boatphoto"); //TÄMÄ OLI MIKÄ JÄI PUUTTUMAAN!!!

            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);
            
            
            //string saskey = cloudBlockBlob.Uri.AbsoluteUri + cloudBlobContainer.GetSharedAccessSignature(null, policyName);

            string saskey = cloudBlobContainer.GetSharedAccessSignature(null, policyName);
            //Console.WriteLine(cloudBlockBlob.Uri.AbsoluteUri + saskey);
            Console.WriteLine("https://simlabit.azureedge.net/?" + saskey);
            return saskey;
            
        }
        return "";
    }*/


    public void omapaska(string token)
    {
        System.Uri url = new System.Uri(token);
        var cloudClient = new CloudBlobClient(url);
        var blob = cloudClient.GetBlobReferenceFromServer(url);
        MemoryStream mem = new MemoryStream(Encoding.UTF8.GetBytes("something"));
        
        //blob.UploadFromStream(mem);
        Console.WriteLine();


        var cloudBlob = new CloudBlob(url);
       if (cloudBlob.Exists()) { Console.WriteLine("container on olemassa"); }
       

        
    }

    // storage account data
    string policyName = "SimLabIT_Policy";
    string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=platformvideos;AccountKey=h6iS/e7UEIOXoLpd3UeECNXZhjOzVSvbdsn6QWs5+k0kJH/iBKZzxZFBJ41TTBZnkrtBC3WKOM2Xmp0ouBFXUg==;EndpointSuffix=core.windows.net";

}
