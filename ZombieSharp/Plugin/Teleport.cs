using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZombieSharp.Models;

namespace ZombieSharp.Plugin;

public class Teleport(ZombieSharp core, ILogger<ZombieSharp> logger)
{
    private readonly ZombieSharp _core = core;
    private readonly ILogger<ZombieSharp> _logger = logger;

    public void TeleportOnLoad()
    {
        _core.AddCommand("css_ztele", "ZTele Command", TeleportCommand);
    }

    public void TeleportOnPlayerSpawn(CCSPlayerController client)
    {
        if(client == null || !client.PawnIsAlive)
            return;

        if(PlayerData.PlayerSpawnData == null)
        {
            _logger.LogCritical("[TeleportCommand] SpawnData is null!");
            return;
        }

        if(!PlayerData.PlayerSpawnData.TryGetValue(client, out var data))
        {
            _logger.LogCritical("[TeleportCommand] client {0} is not in PlayerSpawnData", client.PlayerName);
            return;
        }

        var pos = client.PlayerPawn.Value?.AbsOrigin ?? new(0, 0, 0);
        var angle = client.PlayerPawn.Value?.AbsRotation ?? new(0, 0, 0);

        // saving dev
        data.PlayerPosition = new(pos.X, pos.Y, pos.Z);
        data.PlayerAngle = new(angle.X, angle.Y, angle.Z);
    }

    [CommandHelper(0, "", CommandUsage.CLIENT_ONLY)]
    public void TeleportCommand(CCSPlayerController? client, CommandInfo info)
    {
        if(client == null || !client.PawnIsAlive)
        {
            info.ReplyToCommand($" {_core.Localizer["Prefix"]} {_core.Localizer["Core.MustBeAlive"]}");
            return;
        }

        if(PlayerData.PlayerSpawnData == null)
        {
            _logger.LogCritical("[TeleportCommand] SpawnData is null!");
            return;
        }

        if(!PlayerData.PlayerSpawnData.ContainsKey(client))
        {
            _logger.LogCritical("[TeleportCommand] client {0} is not in PlayerSpawnData", client.PlayerName);
            return;
        }

        var pos = PlayerData.PlayerSpawnData[client].PlayerPosition;
        var angle = PlayerData.PlayerSpawnData[client].PlayerAngle;
        var timer = 5;

        info.ReplyToCommand($" {_core.Localizer["Prefix"]} {_core.Localizer["Teleport.Countdown", timer]}");

        _core.AddTimer(timer, () => 
        {
            if(client == null || !client.PawnIsAlive)
                return;

            client.PlayerPawn.Value?.Teleport(pos, angle);
            client.PrintToChat($" {_core.Localizer["Prefix"]} {_core.Localizer["Teleport.Success", timer]}");
        });
    }
}