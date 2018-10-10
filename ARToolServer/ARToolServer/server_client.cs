using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using ARToolServer;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace ARToolServer
{
    public class Client
    {
        // storage account data
        string policyName = "SimLabIT_Policy";
        //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=simlabitvideos;AccountKey=yWWkrOc52O+krVXnikLhy8at9cXX3LKWEBeBD4jHmImY2hYzNcCyWsaEAaEvk4XnYnkMl+mH1U6Z2kN3RJHkEw==;EndpointSuffix=core.windows.net";
        string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=platformhot;AccountKey=rCaKi0AFir8YydNm8UwuqUhQonDX05K9e8mjJ7cij5Ferm9tVx55eCc7VLx6e33iFQtrmnoxDmSQTXrNgDPQSQ==;EndpointSuffix=core.windows.net";

        public TcpClient clientSocket;
        public TcpClient pingSocket;
        public string clientName; //ip
        public string clientNumber;

        public STATUS status = STATUS.RUNNING;
        public int returnCode = 0; //set error code here
        public int requestCount = 0;
        private Server server;
        public Thread ctThread;
        databaseConnection db;

        int requestMaxSize;

        


        private Client() { } //hide default constructor
        public Client(int requestMaxSize, Server myServer)
        {
            server = myServer;
            this.requestMaxSize = requestMaxSize;
        }


        public byte[] bytesFrom = new byte[10025];
        public Byte[] sendBytes = null;
        public string dataFromClient = null;
        public string serverResponse = null;

        public string rCount = null;
        NetworkStream stream;
        BinaryReader reader;
        BinaryWriter writer;


        NetworkStream pingStream;
        BinaryWriter pingWriter;

        public MemoryStream memStream = new MemoryStream();

        //userinfo
        string username = ""; //logged in username


        public void StartClient(TcpClient inClientSocket, TcpClient pingSocket, string clientNumber)
        {
            
            this.clientSocket = inClientSocket;
            this.pingSocket = pingSocket;
            this.clientName = ((IPEndPoint)inClientSocket.Client.RemoteEndPoint).Address.ToString();
            this.clientNumber = clientNumber;
            stream = clientSocket.GetStream();
            pingStream = pingSocket.GetStream();
            pingWriter = new BinaryWriter(pingStream);

            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
            ctThread = new Thread(ServeClient);
            ctThread.Start();
        }


        string[][] lastVideo;
        string[][] lastFetchedVideos;
        string[][] lastFetchedDBresult;

        public bool Login()
        {
            int passwordRetrys = 1000;

            string passwordInDB = "";
            string salt = "";

            string password = "";

            while (passwordRetrys > 0)
            {
                //get username from user

                //get salt from database and other info about that user from database

                //send salt to user

                //receive password

                //validate password against database

                // if ok -> get max send size from database and other user information

                //send login_ok or not


                if (password == passwordInDB)
                {
                    return true;
                }
                passwordRetrys--;
            }


            return false;
        }


        public bool FetchContentPacksByUser(string userName)
        {
            lastFetchedDBresult = db.getListOfContentPackagesCreatedBy(userName);
            if (lastFetchedDBresult != null)
            {
                SendArrayArrayString(lastFetchedVideos);//send the video list to user
                return true;
            }
            return false;
        }

        public bool FetchVideoSeriesInPackage(string contentPackID)
        {
            lastFetchedDBresult = db.getListOfVideoSeriesInPackage(contentPackID);
            if (lastFetchedDBresult != null)
            {
                SendArrayArrayString(lastFetchedVideos);//send the video list to user
                return true;
            }

            return false;
        }
        public bool FetchVideoNamesAndDataInSerie(string seriesID)
        {
            lastFetchedVideos = db.getVideoIDs_andNamesInSerie(seriesID);

            if (lastFetchedVideos != null)
            {
                SendArrayArrayString(lastFetchedVideos);//send the video list to user

                return true;
            }

            return false;
        }

        public bool Sendping()
        {
            try
            {
                pingWriter.Write((Int32)PROTOCOL_CODES.KEEPALIVE_SIGNAL);
                pingStream.Flush();
                Console.WriteLine("Sent Ping to " + clientName);
                return true;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
            
        }

        private void ServeClient()
        {
            while ((true))
            {
                try
                {
                    while (true)
                    {
                        
                        PROTOCOL_CODES code = receiveProtocolCode();
                        requestCount = requestCount + 1;

                        if(code == PROTOCOL_CODES.ERROR)
                        {
                            server.removeClient(clientName);
                            Console.WriteLine(clientName + " encountered error!");
                            return;
                        }
                        int requestResult = HandleRequest(code);
                        if (requestResult == 0)
                        { //client wanted to quit
                            server.removeClient(clientName);
                            Console.WriteLine(clientName + " has quit! ");
                            writer.Flush();
                            reader.Close();
                            writer.Close();
                            pingSocket.Close();
                            clientSocket.Close();
                            return;
                        } //requestResult == -1 && 
                        if (status == STATUS.ERROR)
                        {//error in handling request that wasnt trivial
                            server.removeClient(clientName);
                            Console.WriteLine(clientName + " has crashed!");
                            return;
                        }


                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Client " + clientName + "experienced error!: " + ex.ToString());
                    returnCode = -1;
                    status = STATUS.ERROR;
                }
            }
            returnCode = 0;
            status = STATUS.QUIT;


        }

        int HandleSendimage()
        {
            Int32 bytesToCome;
            //Console.WriteLine("replying with: ok");
            SendProtocolCode(PROTOCOL_CODES.ACCEPT);


            //Console.WriteLine("awaiting reply");
             //read how many bytes are incoming
            bytesToCome = reader.ReadInt32();
            Console.WriteLine("got reply");
            if (bytesToCome < requestMaxSize)
            {
                SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                Byte[] received = ReceiveBytes(bytesToCome);
            }
            else
            {
                SendProtocolCode(PROTOCOL_CODES.DENY);
            }
            return 1;

        }

        int UploadAssetPackage()
        {
            SendProtocolCode(PROTOCOL_CODES.ACCEPT); //TODO add logic to check if the upload file is too large
            string fileName = ReceiveMessage();

            SendMessage(generateSASkeytoUpload(username, fileName));
            PROTOCOL_CODES reply = receiveProtocolCode();


            if (reply == PROTOCOL_CODES.OK) 
            {
                if (db.UploadAssetPackage(username, fileName))
                {
                    SendProtocolCode(PROTOCOL_CODES.OK);
                    return 1; // all went fine
                }
                else
                { //something went wrong with adding data to the database
                    SendProtocolCode(PROTOCOL_CODES.ERROR_NO_DBCONNECTION);
                    return -1;
                }
            }
            else if (reply == PROTOCOL_CODES.ERROR)
            {//something is wrong with the sas link or upload.. 

            }
            return -1;
        }


        //0 is quit -1 error 1 is ok
        int HandleRequest(PROTOCOL_CODES request)
        {
            Int32 bytesToCome;
            switch (request)
            {

                case PROTOCOL_CODES.UPLOAD_ASSETPACKAGE:
                    return UploadAssetPackage();

                case PROTOCOL_CODES.SENDIMAGE:
                    return HandleSendimage();
                //loput samalla tavalla

                case PROTOCOL_CODES.GET_MY_CONTENTPACKS:
                    if (FetchContentPacksByUser(username)) return 1;
                    return -1;

                case PROTOCOL_CODES.GET_SERIES_IN_PACKAGE:
                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    //read how many bytes are incoming
                    bytesToCome = reader.ReadInt32();
                    if (bytesToCome < requestMaxSize)
                    {
                        SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = ReceiveBytes(bytesToCome);
                        if (FetchVideoSeriesInPackage(Encoding.UTF8.GetString(received))) return 1;
                        return -1;
                    }
                    else
                    {
                        SendProtocolCode(PROTOCOL_CODES.DENY);
                        return 1;
                    }

                case PROTOCOL_CODES.GET_VIDEOS_IN_SERIES:
                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                   //read how many bytes are incoming
                    bytesToCome = reader.ReadInt32();
                    if (bytesToCome < requestMaxSize)
                    {
                        SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = ReceiveBytes(bytesToCome);
                        if (FetchVideoSeriesInPackage(Encoding.UTF8.GetString(received))) return 1;
                        return -1;
                    }
                    else
                    {
                        SendProtocolCode(PROTOCOL_CODES.DENY);
                        return 1;
                    }

                /**
                 * HERE IS WHERE YOUR CODE IS NEEDED AND SIMPLE USE CASE INTEGRATES IN 
                 * 
                 * 
                 **/
                case PROTOCOL_CODES.UPLOAD_VIDEO:
                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                   //read how many bytes are incoming (size of videos name)
                    bytesToCome = reader.ReadInt32();
                    string filename = Encoding.UTF8.GetString(ReceiveBytes(bytesToCome));

                    //TODO logic to check if user would go over his upload limit

                    //--------------------------------------------------------------------------------
                    //TODO:
                    //generate the SAS link where the client application can upload the video 
                    //(the link should be limited to work only for that certain IP where the request comes from, and only work for a certain amount of time)
                    //
                    //we add all the nesserary stuff to generate SAS access links for viewing the video by others to the database into the videos "data field" which is just a byte array
                    //
                    //send the SAS link to the client -- that application then handles the actual uploading of the video the azure storage
                    ////--------------------------------------------------------------------------------



                    return -1;
                case PROTOCOL_CODES.POST_EDITS:

                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    SendBytes(Encoding.UTF8.GetBytes(generateSASkeytoWatch("robert", "SAM_100_0131.mp4")));

                    return 1;


                case PROTOCOL_CODES.REQUEST_VIEW_VIDEO:


                    
                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);

                    string msg = ReceiveMessage();
                    Console.WriteLine(msg);
                    SendBytes(Encoding.UTF8.GetBytes(generateSASkeytoWatch(msg.Split('/')[0], msg.Split('/')[1])));

                    //todo sivu videoiden lähettäminen

                    return 1;
                /*SendProtocolCode(PROTOCOL_CODES.ACCEPT);

                string videoID = ReceiveMessage(); //user sends the ID of the video he wants to view

                byte[][] videoData = db.getVideoData(videoID); //we get the data that is needed to generate the SAS key that lets user view it

                string[] reply;
                if (videoData[0].Length == 0)
                {
                    SendListString(null);
                    return 1; //if the video is not found in the database return 0 its fiiine...
                }


                string uploader = System.Text.Encoding.UTF8.GetString(videoData[0]);
                string sideVideoLeft = System.Text.Encoding.UTF8.GetString(videoData[1]);
                string sideVideoRight = System.Text.Encoding.UTF8.GetString(videoData[2]);


                if (sideVideoLeft.Length > 0 || sideVideoRight.Length > 0)
                {//if either of the side videos exist the lenght of the reply array is 3 
                    reply = new string[3];                   
                }
                else
                {
                    reply = new string[1];
                }

                reply[0] = generateSASkeytoWatch(uploader, videoID);

                if (sideVideoLeft.Length > 0)
                {
                    reply[1] = generateSASkeytoWatch(uploader,sideVideoLeft);
                }
                if (sideVideoRight.Length > 0)
                {
                    reply[2] = generateSASkeytoWatch(uploader, sideVideoRight); 
                }

                SendListString(reply);
                return 0;
                */
                case PROTOCOL_CODES.SENDJSON:
                    SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                     //read how many bytes are incoming
                    //bytesToCome = reader.ReadInt32();
                    string data = reader.ReadString();
                    try
                    {
                        Console.WriteLine("got reply: " + data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    /*if (bytesToCome < requestMaxSize)
                    {
                        SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = ReceiveBytes(bytesToCome);
                        Console.WriteLine(System.Text.Encoding.UTF8.GetString(received));
                    }
                    else
                    {
                        SendProtocolCode(PROTOCOL_CODES.DENY);
                    }*/
                    return 1;
                case PROTOCOL_CODES.QUIT:
                    //SendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    bytesToCome = 0;
                    return 0;

                default:
                    SendProtocolCode(PROTOCOL_CODES.ERROR);
                    Console.WriteLine("Cannot handle request: " + ((PROTOCOL_CODES)request).ToString());
                    return -1;
            }

        }

        PROTOCOL_CODES receiveProtocolCode()
        {
            if (clientSocket == null)
            {
                return PROTOCOL_CODES.ERROR;
            }
            try
            {
                //read the replycode
                Int32 request = reader.ReadInt32(); ;
                Console.WriteLine("Received request: " + ((PROTOCOL_CODES)request).ToString());
                return (PROTOCOL_CODES)request;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return PROTOCOL_CODES.ERROR;
            }
        }

        bool SendProtocolCode(PROTOCOL_CODES code)
        {
            try
            {
                // Get a stream object for writing. 			

                writer.Write((Int32)code);
                writer.Flush();
                Console.WriteLine("Sent protocol code:" + ((PROTOCOL_CODES)code).ToString());
                
                return true;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
        }

        PROTOCOL_CODES SendRequest(PROTOCOL_CODES code)
        {
            try
            {
                // Get a stream object for writing. 			
                writer.Write((Int32)code);
                writer.Flush();
                //read the replycode
                Int32 reply = reader.ReadInt32(); ;
                Console.WriteLine("Client sent request. Received reply:" + ((PROTOCOL_CODES)reply).ToString());
                return (PROTOCOL_CODES)reply;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                status = STATUS.ERROR;
                return PROTOCOL_CODES.ERROR;
            }
            return PROTOCOL_CODES.ERROR;
        }

        byte[] ReceiveBytes(int lenght)
        {
            try
            {
                byte[] bytes = reader.ReadBytes(lenght);
                Console.WriteLine("received full : " + bytes.Length + " bytes");
                return bytes;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return null;
            }
        }

        public void SendBytes(Byte[] clientMessageAsByteArray)
        {
            if (clientSocket == null)
            {
                return;
            }
            try
            { 

                Console.WriteLine("viestin pituus" + clientMessageAsByteArray.Length);
                writer.Write(clientMessageAsByteArray.Length); //send the size of array
                writer.Flush();

                //read the replycode
                Int32 reply = reader.ReadInt32();
                if (reply == (int)PROTOCOL_CODES.ACCEPT)
                {
                    writer.Write(clientMessageAsByteArray);
                    writer.Flush();
                }
                else
                {
                    Console.WriteLine("Server denied request to send something so large!");
                    //TODO : handle not acccepting
                }
                //Console.WriteLine("Client sent his message - should be received by server");

            }
            catch (Exception socketException)
            {
                status = STATUS.ERROR;
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        public void SendMessage(string msg)
        {
            if (SendRequest(PROTOCOL_CODES.SENDMESSAGE) == PROTOCOL_CODES.ACCEPT)
            {
                Console.WriteLine(msg);
                writer.Write(msg);
                writer.Flush();
                

            }
            else return;

            /*byte[] buffer = System.Text.Encoding.UTF8.GetBytes(msg);
            SendBytes(buffer);*/
        }

        public string ReceiveMessage()
        {
            return reader.ReadString();
        }

        public void SendListString(string[] obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, obj);
            stream.Flush();
        }
        public string[] ReceiveListString()
        {
            BinaryFormatter bf = new BinaryFormatter();
            return (string[])bf.Deserialize(stream);
        }


        public void SendArrayArrayString(string[][] obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, obj);
            stream.Flush();
        }

        

        public string[][] ReceiveArrayArrayString()
        {
            BinaryFormatter bf = new BinaryFormatter();
            return (string[][])bf.Deserialize(stream);
        }


        private string UploadToAzure()
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
                cloudBlobContainer = cloudBlobClient.GetContainerReference(username.ToLower());

                cloudBlobContainer.CreateIfNotExists();

                // Set the permissions so the blobs are public. 
                BlobContainerPermissions permissions = new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Container
                };

                var storedPolicy = new SharedAccessBlobPolicy()
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                    SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-1),
                    Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List
                };
                var accessPolicy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
                };

                // add in the new one
                permissions.SharedAccessPolicies.Add(policyName, storedPolicy);
                permissions.SharedAccessPolicies.Add(policyName + "access", accessPolicy);
                cloudBlobContainer.SetPermissions(permissions);
                //
                //
                //
                // upload files
                //CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);//TÄMÄ KOODI KUTSUTAAN UNITY APPLIKAATIOSTA
                //cloudBlockBlob.UploadFromByteArray(bytesFrom, 0, 4);
                //
                //

                // Now we are ready to create a shared access signature based on the stored access policy
                return cloudBlobContainer.GetSharedAccessSignature(storedPolicy, policyName, null, new IPAddressOrRange(clientName));

            }
            else
            {
                return string.Empty;
            }
        }


        public string generateSASkeytoUpload(string uploader, string filename)
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
                cloudBlobContainer.CreateIfNotExists();
                


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

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("filename");


                string saskey = cloudBlockBlob.Uri.AbsoluteUri + cloudBlobContainer.GetSharedAccessSignature(null, policyName);
                Console.WriteLine(cloudBlockBlob.Uri.AbsoluteUri + saskey);
                return saskey;

            }
            return "";
        }

        public string generateSASkeytoWatch(string uploader, string filename)
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
                Console.WriteLine("https://simlabit.azureedge.net/" + uploader + "/" + filename + saskey);
                return "https://simlabit.azureedge.net/"+ uploader + "/"+ filename +saskey;

            }
            return "";
        }


    }

}
