namespace Miningcore.Api.Responses;

public class BestMinerStats
{
    public string Miner { get; set; }
    public double Hashrate { get; set; }
    public double SharesPerSecond { get; set; }
    public DateTime LastSeen { get; set; }
}
