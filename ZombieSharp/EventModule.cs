namespace ZombieSharp
{
    public class EventModule : IEventModule
    {
        private readonly ZombieSharp _core;
        private IZombiePlayer _player;
        private IZTeleModule _zTeleModule;
        private IWeaponModule _weaponModule;

        public EventModule(ZombieSharp plugin, IZombiePlayer player, IZTeleModule zTeleModule, IWeaponModule weapon)
        {
            _core = plugin;
            _player = player;
            _zTeleModule = zTeleModule;
            _weaponModule = weapon;
        }

        public void Initialize()
        {
            _core.RegisterEventHandler<EventRoundStart>(OnRoundStart);
            _core.RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            _core.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            _core.RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            _core.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            _core.RegisterEventHandler<EventPlayerSpawned>(OnPlayerSpawned);
            _core.RegisterEventHandler<EventItemPickup>(OnItemPickup, HookMode.Pre);

            _core.RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            _core.RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
            _core.RegisterListener<Listeners.OnMapStart>(OnMapStart);
        }

        private void OnClientConnected(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            int clientindex = player.UserId ?? 0;

            _core.ZombiePlayers[clientindex] = new ZombiePlayer();
            _core.ZombiePlayers[clientindex].IsZombie = false;
            _core.ZombiePlayers[clientindex].MotherZombieStatus = ZombiePlayer.MotherZombieFlags.NONE;
        }

        private void OnClientDisconnected(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            int clientindex = player.UserId ?? 0;

            _core.ZombiePlayers.Remove(clientindex);
            _zTeleModule.ClientSpawnDatas.Remove(clientindex);
        }

        private void OnMapStart(string mapname)
        {
            _weaponModule.Initialize();
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            RemoveRoundObjective();

            Server.ExecuteCommand("mp_ignore_round_win_conditions 1");
            Server.PrintToChatAll($"{ChatColors.Green}[Z:Sharp]{ChatColors.Default} The current game mode is the Human vs. Zombie, the zombie goal is to infect all human before time is running out.");

            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            _core.InfectOnRoundFreezeEnd();
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            // Reset Zombie Spawned here.
            _core.ZombieSpawned = false;

            // Reset Client Status
            _core.AddTimer(0.3f, Timer_ResetZombieStatus);

            return HookResult.Continue;
        }

        // avoiding zombie status glitch on human class like in zombie:reloaded
        private void Timer_ResetZombieStatus()
        {
            List<CCSPlayerController> clientlist = Utilities.GetPlayers();

            // Reset Client Status
            foreach (var client in clientlist)
            {
                // Reset Client Status.
                _core.ZombiePlayers[client.UserId ?? 0].IsZombie = true;

                // if they were chosen as motherzombie then let's make them not to get chosen again.
                if (_core.ZombiePlayers[client.UserId ?? 0].MotherZombieStatus == ZombiePlayer.MotherZombieFlags.CHOSEN)
                    _core.ZombiePlayers[client.UserId ?? 0].MotherZombieStatus = ZombiePlayer.MotherZombieFlags.LAST;
            }
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            var client = @event.Userid;
            var attacker = @event.Attacker;

            var weapon = @event.Weapon;
            var dmgHealth = @event.DmgHealth;

            if (_core.ZombiePlayers[attacker.UserId ?? 0].IsZombie && !_core.ZombiePlayers[client.UserId ?? 0].IsZombie && string.Equals(weapon, "knife"))
                _core.InfectClient(client, attacker);

            _core.KnockbackClient(client, attacker, dmgHealth, weapon);

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            _core.CheckGameStatus();

            _core.AddTimer(5.0f, () =>
            {
                var clientPawn = @event.Userid.PlayerPawn.Value;

                // Respawn the client.
                clientPawn.Respawn();
            });

            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawned(EventPlayerSpawned @event, GameEventInfo info)
        {
            var client = @event.Userid;
            var clientPawn = client.PlayerPawn.Value;
            var spawnPos = clientPawn.AbsOrigin!;
            var spawnAngle = clientPawn.AbsRotation!;

            _zTeleModule.ZTele_GetClientSpawnPoint(client, spawnPos, spawnAngle);

            // if zombie already spawned then they become zombie.
            if (_core.ZombieSpawned)
                _core.InfectClient(client);

            // else they're human!
            else
                _core.HumanizeClient(client);

            return HookResult.Continue;
        }

        private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
        {          
            var client = @event.Userid;
            var weapon = @event.Item;

            // if client is zombie and it's not a knife, then no pickup
            if (_core.ZombiePlayers[client.UserId ?? 0].IsZombie && !string.Equals(weapon, "knife"))
                return HookResult.Handled;

            return HookResult.Continue;
        }

        private void RemoveRoundObjective()
        {
            var objectivelist = new List<string>() {"func_bomb_target", "func_hostage_rescue", "hostage_entity", "c4"};

            foreach (string objectivename in objectivelist)
            {
                var entityIndex = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(objectivename);

                foreach(var entity in entityIndex)
                {
                    entity.Remove();
                }
            }
        }
    }
}