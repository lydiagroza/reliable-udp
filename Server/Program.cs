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

        // SR variables
        Dictionary<int, Packet> buffer = new Dictionary<int, Packet>();
        Dictionary<int, bool> received = new Dictionary<int, bool>();
        int expectedBase = 0;
        int totalPackets = -1;
        Random random = new Random();
        double lossProb = 0.15;

        // Helper to send ACK with loss simulation
        void SendAck(int seqNum)
        {
            if (random.NextDouble() < lossProb)
            {
                Console.WriteLine($"[LOSS SIMULATED] ACK {seqNum} dropped");
                return;
            }

            Packet ack = new Packet
            {
                Type = PacketType.ACK,
                SequenceNumber = seqNum,
                WindowSize = agreedWindow
            };

            udpServer.Send(ack.Serialize(), clientEndPoint);
            Console.WriteLine($"[ACK] Sent ACK for packet {seqNum}");
        }

                // Receiving loop
        Console.WriteLine("Waiting for data...");
        while (true)
        {
            byte[] dataBytes = udpServer.Receive(ref clientEndPoint);
            Packet dataPacket = Packet.Deserialize(dataBytes);

            if (dataPacket.Type == PacketType.DATA)
            {
                int seq = dataPacket.SequenceNumber;
                Console.WriteLine($"[RECV] Received packet {seq}");

                // Store in buffer if not already received
                if (!received.ContainsKey(seq) || !received[seq])
                {
                    buffer[seq] = dataPacket;
                    received[seq] = true;
                }

                // Always send ACK for this packet
                SendAck(seq);

                // Deliver in-order packets to application
                while (received.ContainsKey(expectedBase) && received[expectedBase])
                {
                    Console.WriteLine($"[DELIVER] Delivering packet {expectedBase} to application");
                    expectedBase++;
                }

                // Check if all packets received
                if (totalPackets != -1 && expectedBase >= totalPackets)
                {
                    break;
                }
            }
        }

        // Reassemble the message
        Console.WriteLine("Reassembling message...");
        List<byte> fullMessage = new List<byte>();
        
        for (int i = 0; i < expectedBase; i++)
        {
            fullMessage.AddRange(buffer[i].Data);
        }
        
        string result = System.Text.Encoding.UTF8.GetString(fullMessage.ToArray());
        Console.WriteLine($"Received message: {result}");

    }

}