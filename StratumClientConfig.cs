using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.Stratum.UI;

// VintagestoryData/ModConfig/stratumui-client.json. Created with defaults on first launch.
public class StratumClientConfig
{
    private const string FileName = "stratumui-client.json";

    // Echo /kick /mute /warn results to local chat. Off by default; the server already prints its own.
    public bool ShowActionResultsInChat { get; set; } = false;

    public static StratumClientConfig LoadOrCreate(ICoreClientAPI api)
    {
        StratumClientConfig cfg = null;
        try
        {
            cfg = api.LoadModConfig<StratumClientConfig>(FileName);
        }
        catch
        {
            // Corrupt file; rewrite with defaults.
            cfg = null;
        }

        if (cfg == null)
        {
            cfg = new StratumClientConfig();
            api.StoreModConfig(cfg, FileName);
        }

        return cfg;
    }
}
