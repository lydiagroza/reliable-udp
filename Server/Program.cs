using System.Net;
using System.Net.Sockets;
using Shared;

class Server
{
    static void Main()
    {
        int port = 5001;
        UdpClient udpServer = new UdpClient(port);
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        Console.WriteLine($"Server listening on port {port}...");

        // Step 1 -> Receive SYN
        byte[] synByte = udpServer.Receive(ref clientEndPoint);
        Packet synPacket = Packet.Deserialize(synByte);

        if (synPacket.Type != PacketType.SYN)
        {
            Console.WriteLine("Expected SYN, got something else. Aborting.");
            return;
        }

        Console.WriteLine($"Received SYN. Client wants window size: {synPacket.WindowSize}");

        // Step 2 -> Send SYN-ACK
        int agreedWindow = Math.Min(synPacket.WindowSize, 5);
        Packet synAck = new Packet
        {
            Type = PacketType.SYNACK,
            SequenceNumber = 0,
            WindowSize = agreedWindow,
        };

        udpServer.Send(synAck.Serialize(), clientEndPoint);
        Console.WriteLine($"Sent SYN-ACK. Agreed window size: {agreedWindow}");

        // Step 3 -> Receive ACK
        byte[] ackBytes = udpServer.Receive(ref clientEndPoint);
        Packet ackPacket = Packet.Deserialize(ackBytes);

        if (ackPacket.Type != PacketType.ACK)
        {
            Console.WriteLine("Expected ACK, got something else. Aborting.");
            return;
        }

        // SR variables
        Dictionary<int, Packet> buffer = new Dictionary<int, Packet>();
        Dictionary<int, bool> received = new Dictionary<int, bool>();
        int expectedBase = 0;
        int totalPackets = ackPacket.TotalPackets;  // read from handshake ACK
        Random random = new Random();
        double lossProb = 0.15;

        Console.WriteLine($"Handshake complete! Expecting {totalPackets} packets.");

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
                if (expectedBase >= totalPackets)
                {
                    break;
                }
            }
        }

        // Linger phase: keep re-ACKing retransmitted packets until client stops sending.
        // Without this, a dropped ACK leaves the client retransmitting to a dead server.
        udpServer.Client.ReceiveTimeout = 3000;
        while (true)
        {
            try
            {
                byte[] dataBytes = udpServer.Receive(ref clientEndPoint);
                Packet dataPacket = Packet.Deserialize(dataBytes);
                if (dataPacket.Type == PacketType.DATA && received.TryGetValue(dataPacket.SequenceNumber, out bool alreadyReceived) && alreadyReceived)
                {
                    SendAck(dataPacket.SequenceNumber);
                }
            }
            catch (SocketException)
            {
                break;
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