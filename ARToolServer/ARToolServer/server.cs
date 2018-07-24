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
    ,POST_EDITS,UPLOAD_VIDEO

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
        for(int i = 0; i<clients.Count; i++)
        {
            if(clients[i].clientName == name)
            {

                clients.Remove(clients[i]);
            }
        }
    }

    public void sendPings()
    {

        Console.WriteLine("tykkäät sä pelaa pinki ponkia");
        while (true)
        {
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            foreach (Client c in clients)
            {
                if (c.sendping() == false)
                {
                    clients.Remove(c);
                }

            }
            Int32 unixTimestamp2 = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Console.WriteLine(2000 - (unixTimestamp - unixTimestamp2) + " c count: " + clients.Count);
            Thread.Sleep(2000 - (unixTimestamp - unixTimestamp2));
        }
    }

    void startServing()
    {
        serverSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), 8052);
        serverPingSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), 8051);
        clientSocket = default(TcpClient);
        serverSocket.Start();
        serverPingSocket.Start();

        Console.WriteLine(" >> " + "Server Started");


        Thread pinging = new Thread(sendPings);
        pinging.Start();
        while (true)
        {
            clientSocket = serverSocket.AcceptTcpClient();
            clientPingSocket = serverPingSocket.AcceptTcpClient();
            Console.WriteLine(" >> " + "Client No:" + clients.Count + 1 + " started!");

            Client client = new Client(max_acceptedSend, this);

            clients.Add(client);
            client.startClient(clientSocket,clientPingSocket, Convert.ToString(clients.Count)); //start servering the client
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

        Console.WriteLine(" >> " + "exit");
        Console.ReadLine();
        server.stop();
    }
}
