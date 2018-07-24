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



namespace ARToolServer
{
    public class Client
    {
        // storage account data
        string policyName = "SimLabIT_Policy";
        string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=simlabitvideos;AccountKey=yWWkrOc52O+krVXnikLhy8at9cXX3LKWEBeBD4jHmImY2hYzNcCyWsaEAaEvk4XnYnkMl+mH1U6Z2kN3RJHkEw==;EndpointSuffix=core.windows.net";

        public TcpClient clientSocket;
        public TcpClient pingSocket;
        public string clientName; //ip
        public STATUS status = STATUS.RUNNING;
        public int returnCode = 0; //set error code here
        public int requestCount = 0;
        private Server server;

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
        NetworkStream pingStream;
        public MemoryStream message = new MemoryStream();

        //userinfo
        string username = ""; //logged in username


        public void startClient(TcpClient inClientSocket, TcpClient pingSocket, string clineNo)
        {
            this.clientSocket = inClientSocket;
            this.pingSocket = pingSocket;
            this.clientName = clineNo;
            Thread ctThread = new Thread(serveClient);
            ctThread.Start();
            stream = clientSocket.GetStream();
            pingStream = pingSocket.GetStream();
        }


        string[][] lastVideo;
        string[][] lastFetchedVideos;
        string[][] lastFetchedDBresult;

        public bool login()
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


        public bool fetchContentPacksByUser(string userName)
        {
            lastFetchedDBresult = db.getListOfContentPackagesCreatedBy(userName);
            if (lastFetchedDBresult != null)
            {
                sendListString(lastFetchedVideos);//send the video list to user
                return true;
            }
            return false;
        }

        public bool fetchVideoSeriesInPackage(string contentPackID)
        {
            lastFetchedDBresult = db.getListOfVideoSeriesInPackage(contentPackID);
            if (lastFetchedDBresult != null)
            {
                sendListString(lastFetchedVideos);//send the video list to user
                return true;
            }

            return false;
        }
        public bool fetchVideoNamesAndDataInSerie(string seriesID)
        {
            lastFetchedVideos = db.getVideoIDs_andNamesInSerie(seriesID);

            if (lastFetchedVideos != null)
            {
                sendListString(lastFetchedVideos);//send the video list to user

                return true;
            }

            return false;
        }

        public bool sendping()
        {

            if (pingSocket == null)
            {

                return false;
            }
            try
            {
                // Get a stream object for writing. 			
                if (pingStream.CanWrite)
                {
                    byte[] message = BitConverter.GetBytes((int)PROTOCOL_CODES.KEEPALIVE_SIGNAL);
                    pingStream.Write(message, 0, 4); 
                    Console.WriteLine("Sent protocol code:" + (PROTOCOL_CODES.KEEPALIVE_SIGNAL).ToString());
                    return true;
                }
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
            
        }

        private void serveClient()
        {
            while ((true))
            {
                try
                {
                    while (true)
                    {
                        
                        PROTOCOL_CODES code = getRequest();
                        requestCount = requestCount + 1;

                        if(code == PROTOCOL_CODES.ERROR)
                        {
                            server.removeClient(clientName);
                            Console.WriteLine(clientName + " encountered error!");
                            return;
                        }
                        int requestResult = handleRequest(code);
                        if (requestResult == 0)
                        { //client wanted to quit
                            server.removeClient(clientName);
                            Console.WriteLine(clientName + " has quit!");
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

        byte[] receiveBytes(int lenght)
        {
            try
            {
                byte[] bytes = new byte[lenght];
                int received;
                int receivedSofar = 0;
                while (receivedSofar < lenght && (received = stream.Read(bytesFrom, 0, bytesFrom.Length)) > 0)
                {
                    Array.Copy(bytesFrom, 0, bytes, receivedSofar, received);
                    receivedSofar += received;
                    // Convert byte array to string message. 							
                    string clientMessage = Encoding.ASCII.GetString(bytesFrom, 0, received);
                    Console.WriteLine("received: " + received + " bytes");
                }
                Console.WriteLine("received full : " + receivedSofar + " bytes");
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

        int handleSendimage()
        {
            Int32 bytesToCome;
            Console.WriteLine("replying with: ok");
            sendProtocolCode(PROTOCOL_CODES.ACCEPT);


            Console.WriteLine("awaiting reply");
            stream.Read(bytesFrom, 0, 4); //read how many bytes are incoming
            bytesToCome = BitConverter.ToInt32(bytesFrom, 0);
            Console.WriteLine("got reply");
            if (bytesToCome < requestMaxSize)
            {
                sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                Byte[] received = receiveBytes(bytesToCome);
            }
            else
            {
                sendProtocolCode(PROTOCOL_CODES.DENY);
            }
            return 1;

        }


        //0 is quit -1 error 1 is ok
        int handleRequest(PROTOCOL_CODES request)
        {
            Int32 bytesToCome;
            switch (request)
            {
                case PROTOCOL_CODES.SENDIMAGE:
                    return handleSendimage();
                //loput samalla tavalla

                case PROTOCOL_CODES.GET_MY_CONTENTPACKS:
                    if (fetchContentPacksByUser(username)) return 1;
                    return -1;

                case PROTOCOL_CODES.GET_SERIES_IN_PACKAGE:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    stream.Read(bytesFrom, 0, 4); //read how many bytes are incoming
                    bytesToCome = BitConverter.ToInt32(bytesFrom, 0);
                    if (bytesToCome < requestMaxSize)
                    {
                        sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = receiveBytes(bytesToCome);
                        if (fetchVideoSeriesInPackage(Encoding.UTF8.GetString(received))) return 1;
                        return -1;
                    }
                    else
                    {
                        sendProtocolCode(PROTOCOL_CODES.DENY);
                        return 1;
                    }

                case PROTOCOL_CODES.GET_VIDEOS_IN_SERIES:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    stream.Read(bytesFrom, 0, 4); //read how many bytes are incoming
                    bytesToCome = BitConverter.ToInt32(bytesFrom, 0);
                    if (bytesToCome < requestMaxSize)
                    {
                        sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = receiveBytes(bytesToCome);
                        if (fetchVideoSeriesInPackage(Encoding.UTF8.GetString(received))) return 1;
                        return -1;
                    }
                    else
                    {
                        sendProtocolCode(PROTOCOL_CODES.DENY);
                        return 1;
                    }

                /**
                 * HERE IS WHERE YOUR CODE IS NEEDED AND SIMPLE USE CASE INTEGRATES IN 
                 * 
                 * 
                 **/
                case PROTOCOL_CODES.UPLOAD_VIDEO:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    stream.Read(bytesFrom, 0, 4); //read how many bytes are incoming (size of videos name)
                    bytesToCome = BitConverter.ToInt32(bytesFrom, 0);
                    string filename = Encoding.UTF8.GetString(receiveBytes(bytesToCome));

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


                case PROTOCOL_CODES.REQUEST_VIEW_VIDEO:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    stream.Read(bytesFrom, 0, 4);
                    string videoID = Encoding.UTF8.GetString(bytesFrom); //user sends the ID of the video he wants to view
                    /*
                    byte[] videoData = db.getVideoData(videoID); //we get the data that is needed to generate the SAS key that lets user view it

                    if (videoData != null)
                    {
                        sendBytes(generateSASkeytoWatch(, file))
                    }
                    */
                    return -1;


                case PROTOCOL_CODES.SENDJSON:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    Console.WriteLine("awaiting reply");
                    stream.Read(bytesFrom, 0, 4); //read how many bytes are incoming
                    bytesToCome = BitConverter.ToInt32(bytesFrom, 0);
                    Console.WriteLine("got reply");
                    if (bytesToCome < requestMaxSize)
                    {
                        sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                        Byte[] received = receiveBytes(bytesToCome);
                    }
                    else
                    {
                        sendProtocolCode(PROTOCOL_CODES.DENY);
                    }
                    return 1;
                case PROTOCOL_CODES.QUIT:
                    sendProtocolCode(PROTOCOL_CODES.ACCEPT);
                    return 0;

                default:
                    sendProtocolCode(PROTOCOL_CODES.ERROR);
                    Console.WriteLine("Cannot handle request: " + ((PROTOCOL_CODES)request).ToString());
                    return -1;
            }

        }

        PROTOCOL_CODES getRequest()
        {
            if (clientSocket == null)
            {
                return PROTOCOL_CODES.ERROR;
            }
            try
            {
                stream.Read(bytesFrom, 0, 4); //read the replycode
                Int32 request = BitConverter.ToInt32(bytesFrom, 0);
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

        bool sendProtocolCode(PROTOCOL_CODES code)
        {
            if (clientSocket == null)
            {

                return false;
            }
            try
            {
                // Get a stream object for writing. 			
                if (stream.CanWrite)
                {
                    byte[] message = BitConverter.GetBytes((int)code);
                    stream.Write(message, 0, 4); //read the replycode
                    Console.WriteLine("Sent protocol code:" + ((PROTOCOL_CODES)code).ToString());
                    return true;
                }
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                returnCode = -1;
                status = STATUS.ERROR;
                return false;
            }
        }

        PROTOCOL_CODES sendRequest(PROTOCOL_CODES code)
        {
            if (clientSocket == null)
            {
                return PROTOCOL_CODES.ERROR;
            }
            try
            {
                // Get a stream object for writing. 			
                if (stream.CanWrite)
                {
                    byte[] message = BitConverter.GetBytes((int)code);
                    stream.Write(message, 0, 4);
                    stream.Read(bytesFrom, 0, 4); //read the replycode
                    Int32 reply = BitConverter.ToInt32(bytesFrom, 0);
                    Console.WriteLine("Client sent request. Received reply:" + ((PROTOCOL_CODES)reply).ToString());
                    return (PROTOCOL_CODES)reply;
                }
            }
            catch (Exception socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
                status = STATUS.ERROR;
                return PROTOCOL_CODES.ERROR;
            }
            return PROTOCOL_CODES.ERROR;
        }


        public void SendBytes(Byte[] clientMessageAsByteArray)
        {
            if (clientSocket == null)
            {
                return;
            }
            try
            {
                // Get a stream object for writing. 			
                if (stream.CanWrite)
                {
                    byte[] header = BitConverter.GetBytes(clientMessageAsByteArray.Length);
                    stream.Write(header, 0, header.Length); //send the size of array
                    stream.Flush();

                    stream.Read(bytesFrom, 0, 4); //read the replycode
                    Int32 reply = BitConverter.ToInt32(bytesFrom, 0);
                    if (reply == (int)PROTOCOL_CODES.ACCEPT)
                    {
                        stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                        stream.Flush();
                    }
                    else
                    {
                        Console.WriteLine("Server denied request to send something so large!");
                        //TODO : handle not acccepting
                    }
                    Console.WriteLine("Client sent his message - should be received by server");
                }
            }
            catch (Exception socketException)
            {
                status = STATUS.ERROR;
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        public void sendListString(string[][] obj)
        {
            int len = obj.Length;
            byte[] bytes = new byte[len];

            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, obj);
            stream.Flush();
        }

        public string[][] receiveListString()
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
                // CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename); TÄMÄ KOODI KUTSUTAAN UNITY APPLIKAATIOSTA
                // cloudBlockBlob.UploadFromByteArray(bytesFrom, 0, 4);
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



        private string generateSASkeytoWatch(string uploader, string filename)
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


                var accessPolicy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
                };
                // upload files
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);

                // Now we are ready to create a shared access signature based on the stored access policy
                return cloudBlobContainer.GetSharedAccessSignature(accessPolicy, policyName + "access", null, new IPAddressOrRange(clientName));
            }
            return "";
        }
    }

}
