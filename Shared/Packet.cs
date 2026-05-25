using System.Text;
using System.Text.Json;

namespace Shared;

public enum PacketType
{
    SYN,
    SYNACK,
    ACK,
    DATA
}

public class Packet
{
    public PacketType Type { get; set; }
    public int SequenceNumber { get; set; }
    public int WindowSize { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int TotalPackets { get; set; } = 0;

    public byte[] Serialize()
    {
        string json = JsonSerializer.Serialize(this);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public static Packet Deserialize(byte[] data)
    {
        string json = System.Text.Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<Packet>(json)!;
    }
}