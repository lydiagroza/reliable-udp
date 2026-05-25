using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using Shared;

class Client
{
    static void Main()
    {
        string serverIp = "127.0.0.1";
        int serverPort = 5001;
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

        //Prepare to send data
        string message = "Hello, this is a message from the client!";
        byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        int packetSize = 10;
        int totalPackets = (int)Math.Ceiling((double)messageBytes.Length / packetSize);
        
        Packet ack = new Packet
        {
            Type = PacketType.ACK,
            SequenceNumber = 0,
            WindowSize = agreedWindow,
            TotalPackets = totalPackets
        };
        
        udpClient.Send(ack.Serialize(),serverEndPoint);
        Console.WriteLine("Sent ACK. Handshake complete!");

        Console.WriteLine($"Sending message in {totalPackets} packets...");

        //SR variables
        int base_ = 0;
        int nextSeq = 0;
        Dictionary<int, bool> acked = new Dictionary<int, bool>();
        Dictionary<int, System.Timers.Timer> timers = new Dictionary<int, System.Timers.Timer>();
        Dictionary<int, Packet> sentPackets = new Dictionary<int, Packet>();
        object lockObj = new object();

        //packet loss simulation 
        Random random = new Random();
        double lossProb = 0.15;

        //Helper to send a packet with loss simulation 
        void SendWithLoss(Packet packet)
        {
            if(random.NextDouble() < lossProb)
            {
                Console.WriteLine($"[LOSS SIMULATED] Packet {packet.SequenceNumber} dropped");
                return;
            }
            udpClient.Send(packet.Serialize(), serverEndPoint);
        }

        //Helper to start timer for a packet
        void StartTimer(int seqNum)
        {
            var timer = new System.Timers.Timer(1000); 
            timer.AutoReset = false;
            timer.Elapsed += (sender, e) =>
            {
                lock (lockObj)
                {
                    if(!acked.ContainsKey(seqNum) || !acked[seqNum])
                    {
                        Console.WriteLine($"[TIMEOUT] Retransmitting packet {seqNum}");
                        SendWithLoss(sentPackets[seqNum]);
                        StartTimer(seqNum);
                    }
                }
            };
            timers[seqNum] = timer;
            timer.Start();
        }

        bool transferDone = false;
        Task askListener = Task.Run(() =>
        {
            while(!transferDone)
            {
                try
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] ackBytes = udpClient.Receive(ref ep);
                    Packet ackPacket = Packet.Deserialize(ackBytes);

                    if(ackPacket.Type == PacketType.ACK)
                    {
                        lock (lockObj)
                        {
                            int seq = ackPacket.SequenceNumber;
                            Console.WriteLine($"[ACK] Received ACK for packet {seq}");
                            acked[seq] = true;

                            //Stop timer for this packet
                            if(timers.ContainsKey(seq))
                            {
                                timers[seq].Stop();
                                timers[seq].Dispose();
                            }

                            //Slide the window
                            while (acked.ContainsKey(base_) && acked[base_])
                            {
                                base_++;
                            }
                        }
                    }
                }
                catch { }
            }
        });

        while(base_ < totalPackets)
        {
            lock (lockObj)
            {
                while (nextSeq < base_ + agreedWindow && nextSeq < totalPackets)
                {
                    // Slice the message into chunks
                    int start = nextSeq * packetSize;
                    int length = Math.Min(packetSize, messageBytes.Length - start);
                    byte[] chunk = new byte[length];
                    Array.Copy(messageBytes, start, chunk, 0, length);

                    Packet dataPacket = new Packet
                    {
                        Type = PacketType.DATA,
                        SequenceNumber = nextSeq,
                        WindowSize = agreedWindow,
                        Data = chunk
                    };

                    sentPackets[nextSeq] = dataPacket;
                    acked[nextSeq] = false;

                    Console.WriteLine($"[SEND] Sending packet {nextSeq}");
                    SendWithLoss(dataPacket);
                    StartTimer(nextSeq);

                    nextSeq++;
                }       
            }
            Thread.Sleep(100);
        }
        transferDone = true;
        Console.WriteLine("All packets sent and acknowledged!");

    }
}
