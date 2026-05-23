using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.Stratum.UI;

// Lives in VintagestoryData/ModConfig/stratumui-client.json. Created on first launch with defaults.
public class StratumClientConfig
{
    private const string FileName = "stratumui-client.json";

    // Echo player-action results (kick/mute/warn/etc.) to the local chat log. Off by default since
    // the server already prints its own confirmation when the underlying command runs.
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
            // Corrupt file - fall through and rewrite with defaults.
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
