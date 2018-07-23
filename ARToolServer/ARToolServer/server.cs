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

public enum PROTOCOL_CODES
{
    ERROR = -1, ERROR_NO_DBCONNECTION
    ,ACCEPT, DENY, SENDIMAGE, SENDVIDEO, SENDJSON, SENDLOCATION, QUIT
        
    ,GET_MY_CONTENTPACKS, SEARCH_CONTENT_PACKS, SEARCH_CONTENTPACKS_BY_USER
    ,GET_SERIES_IN_PACKAGE,GET_VIDEOS_IN_SERIES
    ,REQUEST_VIEW_VIDEO, REQUEST_EDIT_VIDEO
    ,POST_EDITS,UPLOAD_VIDEO,
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


    List<string>[] allContentPacks;

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

            Client client = new Client(max_acceptedSend);

            clients.Add(client);
            client.startClient(clientSocket, Convert.ToString(clients.Count)); //start servering the client
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



        }
        server.stop();
        Console.WriteLine(" >> " + "exit");
        Console.ReadLine();
    }
}
