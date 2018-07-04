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

public enum PROTOCOL_CODES
{
    ERROR = -1, ACCEPT, DENY, SENDIMAGE, SENDVIDEO, SENDJSON, SENDLOCATION, QUIT
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
    List<Client> clients = new List<Client>();
    TcpListener serverSocket;
    TcpClient clientSocket;
    databaseConnection db;

    int max_acceptedSend = int.MaxValue;

    public STATUS status = STATUS.ERROR;

    void startServing()
    {
        serverSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), 8052);
        clientSocket = default(TcpClient);
        serverSocket.Start();


        Console.WriteLine(" >> " + "Server Started");

       


        while (true)
        {
            clientSocket = serverSocket.AcceptTcpClient();
            Console.WriteLine(" >> " + "Client No:" + clients.Count + 1 + " started!");


            //TODO: ask database for the max send size for this client
            Client client = new Client(max_acceptedSend);

            clients.Add(client);
            client.startClient(clientSocket, Convert.ToString(clients.Count));
        }
    }

    void stop()
    {
        foreach (Client c in clients)
        {
            //TODO: sanotaan heipat
            c.clientSocket.Close();
        }
        serverSocket.Stop();
    }
    static void Main(string[] args)
    {
        Server server = new Server();

        Thread servingThread = new Thread(server.startServing);
        servingThread.Start();
        

        while (true) ;

        server.stop();
        Console.WriteLine(" >> " + "exit");
        Console.ReadLine();
    }
}
public class Client
{
    public TcpClient clientSocket;
    public string clientName;
    public STATUS status = STATUS.RUNNING;
    public int returnCode = 0; //set error code here
    public int requestCount = 0;

    int requestMaxSize;

    private Client() { } //hide default constructor
    public Client(int requestMaxSize)
    {
        this.requestMaxSize = requestMaxSize;
    }


    public byte[] bytesFrom = new byte[10025];
    public Byte[] sendBytes = null;
    public string dataFromClient = null;
    public string serverResponse = null;

    public string rCount = null;
    NetworkStream stream;
    public MemoryStream message = new MemoryStream();




    public void startClient(TcpClient inClientSocket, string clineNo)
    {
        this.clientSocket = inClientSocket;
        this.clientName = clineNo;
        Thread ctThread = new Thread(serveClient);
        ctThread.Start();
        stream = clientSocket.GetStream();
    }

    private void serveClient()
    {
        while ((true))
        {
            try
            {
                requestCount = requestCount + 1;
                while (true)
                {
                    PROTOCOL_CODES code = getRequest();
                    requestCount = requestCount + 1;
                    int requestResult = handleRequest(code);
                    if (requestResult == 0)
                    { //client wanted to quit
                        return;
                    }
                    if(requestResult == -1 && status == STATUS.ERROR)
                    {//error in handling request that wasent trivial
                        return;
                    }
                }

                //DIS IS GUD clientSocket.Client.Send()
               /* while (true) {
                    clientSocket.Client.Receive(bytesFrom);
                    string clientMessage = Encoding.ASCII.GetString(bytesFrom);
                    Console.WriteLine("client message received as: " + clientMessage + "|||| ");
                    SendMessage();
                }*/

                //Byte[] bytes = new Byte[1024];
                int length;
                // Read incomming stream into byte arrary. 						
                while ((length = stream.Read(bytesFrom, 0, bytesFrom.Length)) > 0)
                {
                    //var incommingData = new byte[length];
                    //Array.Copy(bytesFrom, incommingData, length);
                    message.Write(bytesFrom, 0, length);

                    //Image image = byteArrayToImage(incommingData);

                    // Convert byte array to string message. 							
                    string clientMessage = Encoding.ASCII.GetString(bytesFrom,0,length);
                    Console.WriteLine("client message received as: " + clientMessage + "|||| " + length);

                    //image.Save("C:/Users/SimlabitPasi/Documents/Visual Studio 2017/Projects/artoolserver/image.png", ImageFormat.Png);
                    SendMessage();
                }
                Console.WriteLine("ended");





                stream.Read(bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);



                dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                Console.WriteLine(clientName + ": "+ dataFromClient);

                rCount = Convert.ToString(requestCount);
                serverResponse = "Server to clinet(" + clientName + ") " + rCount;
                sendBytes = Encoding.ASCII.GetBytes(serverResponse);
                stream.Write(sendBytes, 0, sendBytes.Length);
                stream.Flush();
                Console.WriteLine(" >> " + serverResponse);
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
    /// <summary> 	
    /// Send message to client using socket connection. 	
    /// </summary> 	
    private void SendMessage()
    {
        if (clientSocket == null)
        {
            return;
        }
        
        try
        {
            // Get a stream object for writing. 			
            NetworkStream stream = clientSocket.GetStream();
            if (stream.CanWrite)
            {
                string serverMessage = "This is a message from your server.";
                // Convert string message to byte array.                 
                byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(serverMessage);
                // Write byte array to socketConnection stream.               
                stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
                Console.WriteLine("Server sent his message - should be received by client");
            }
        }
        catch (SocketException socketException)
        {
            Console.WriteLine("Socket exception: " + socketException);
            returnCode = -1;
            status = STATUS.ERROR;
        }
    }

    byte[] receiveBytes(int lenght)
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
        catch (SocketException socketException)
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
        catch (SocketException socketException)
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
        catch (SocketException socketException)
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
        catch (SocketException socketException)
        {
            Console.WriteLine("Socket exception: " + socketException);
        }
    }


}

/*public class TCPTestServer
{
    #region private members 	
    /// <summary> 	
    /// TCPListener to listen for incomming TCP connection 	
    /// requests. 	
    /// </summary> 	
    private TcpListener tcpListener;
    /// <summary> 
    /// Background thread for TcpServer workload. 	
    /// </summary> 	
    private Thread tcpListenerThread;
    /// <summary> 	
    /// Create handle to connected tcp client. 	
    /// </summary> 	
    private TcpClient connectedTcpClient;
    #endregion



    static void Main(string[] args)
    {

        TCPTestServer s = new TCPTestServer();
        s.Start();
        while (true) ;


    }
    // Use this for initialization
    void Start()
    {

        // Start TcpServer background thread 		
        tcpListenerThread = new Thread(new ThreadStart(ListenForIncommingRequests));
        tcpListenerThread.IsBackground = true;
        tcpListenerThread.Start();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SendMessage();
        }
    }

    public Image byteArrayToImage(byte[] bytesArr)
    {
        Image img;
        using (MemoryStream memstr = new MemoryStream(bytesArr))
        {
            img = Image.FromStream(memstr);
            img.Save("C:\\Users\\SimlabitPasi\\Documents\\Visual Studio 2017\\Projects\\artoolserver\\image.png", ImageFormat.Png);
        }


        return img;
    }
    /// <summary> 	
    /// Runs in background TcpServerThread; Handles incomming TcpClient requests 	
    /// </summary> 	
    private void ListenForIncommingRequests()
    {
        try
        {
            // Create listener on localhost port 8052. 			
            tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8052);
            tcpListener.Start();
            Console.WriteLine("Server is listening");
            Byte[] bytes = new Byte[1024];
            while (true)
            {
                using (connectedTcpClient = tcpListener.AcceptTcpClient())
                {
                    // Get a stream object for reading 					
                    using (NetworkStream stream = connectedTcpClient.GetStream())
                    {
                        int length;
                        // Read incomming stream into byte arrary. 						
                        while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            var incommingData = new byte[length];
                            Array.Copy(bytes, 0, incommingData, 0, length);

                            //Image image = byteArrayToImage(incommingData);

                            // Convert byte array to string message. 							
                            string clientMessage = Encoding.ASCII.GetString(incommingData);
                            Console.WriteLine("client message received as: " + clientMessage + "|||| " +length);

                            //image.Save("C:/Users/SimlabitPasi/Documents/Visual Studio 2017/Projects/artoolserver/image.png", ImageFormat.Png);

                        }
                        Console.WriteLine("ended");
                    }
                }
            }
        }
        catch (SocketException socketException)
        {
            Console.WriteLine("SocketException " + socketException.ToString());
        }
    }


    /// <summary> 	
    /// Send message to client using socket connection. 	
    /// </summary> 	
    private void SendMessage()
    {
        if (connectedTcpClient == null)
        {
            return;
        }

        try
        {
            // Get a stream object for writing. 			
            NetworkStream stream = connectedTcpClient.GetStream();
            if (stream.CanWrite)
            {
                string serverMessage = "This is a message from your server.";
                // Convert string message to byte array.                 
                byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(serverMessage);
                // Write byte array to socketConnection stream.               
                stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
                Console.WriteLine("Server sent his message - should be received by client");
            }
        }
        catch (SocketException socketException)
        {
            Console.WriteLine("Socket exception: " + socketException);
        }
    }


}*/
