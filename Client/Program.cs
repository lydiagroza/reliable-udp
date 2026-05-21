using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
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


        //step 2 -> receive SYN-ACK
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] synAckBytes = udpClient.Receive(ref remoteEP);
        Packet synAck = Packet.Deserialize(synAckBytes);

        if(synAck.Type != PacketType.SYNACK)
            {
                Console.WriteLine("Expected SYN-ACK, got something else.    Aborting");
                return;
            }

        int agreedWindow = synAck.WindowSize;
        Console.WriteLine($"Received SYN-ACK. Agreed Window size {agreedWindow}");

        Packet ack = new Packet
        {
            Type = PacketType.ACK,
            SequenceNumber = 0,
            WindowSize = agreedWindow
        };

        udpClient.Send(ack.Serialize(),serverEndPoint);
        Console.WriteLine("Sent ACK. Handshake complete!");

    }
}
