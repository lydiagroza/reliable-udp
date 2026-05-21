using System.Net;
using System.Net.Sockets;
using Shared;

class Client
{
    static void Main()
    {
        string serverIp = "127.0.0.1";
        int serverPort = 5000;
        int desiredWindow = 10;

        UdpClient udpClient = new UdpClient();
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp),serverPort);

        Console.WriteLine("Starting handshake...");

        //step 1 -> Send SYN
        Packet syn = new Packet
        {
            Type = PacketType.SYN,
            SequenceNumber = 0,
            WindowSize = desiredWindow 
        };

    udpClient.Send(syn.Serialize(), serverEndPoint);
    Console.WriteLine($"Sent SYN with window size: {desiredWindow}");

    //


    }
}
