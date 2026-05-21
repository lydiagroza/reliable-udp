using System.Net;
using System.Net.Sockets;
using Shared;

class Server
{
    static void Main()
    {
        int port = 5000;
        UdpClient udpServer = new UdpClient(port);
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        Console.WriteLine($"Server listening on port {port}...");

        byte[] synByte = udpServer.Receive(ref clientEndPoint);
        Packet synPacket = Packet.Deserialize(synByte);


        //step 1 -> Recieved SYN
        if (synPacket.Type != PacketType.SYN)
        {
            Console.WriteLine("Expected SYN, got something else. Aborting.");
            return;
        }

        Console.WriteLine($"Recieved SYN. Client wants window size: {synPacket.WindowSize}");

        //step 2 -> Send SYN-ACK 
        int agreedWindow = Math.Min(synPacket.WindowSize, 5);
        Packet synAck = new Packet
        {
            Type = PacketType.SYNACK,
            SequenceNumber = 0,
            WindowSize = agreedWindow,
        };

        udpServer.Send(synAck.Serialize(), clientEndPoint);
        Console.WriteLine($"Sent SYN-ACK. Agreed window size: {agreedWindow}");

        //step 3 -> Recieve ACK
        byte[] ackBytes = udpServer.Receive(ref clientEndPoint);
        Packet ackPacket = Packet.Deserialize(ackBytes);

        if(ackPacket.Type != PacketType.ACK)
        {
            Console.WriteLine("Expected ACK, got something else. Aborting.");
            return;
        }

        Console.WriteLine("Handshake complete! Ready to receive data.");

    }

}