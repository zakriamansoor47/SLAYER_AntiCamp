using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.Json.Serialization;
using System.Drawing;
using Microsoft.Extensions.Logging;

namespace SLAYER_AntiCamp;
// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8604
#pragma warning disable CS8619
public class SLAYER_AntiCampConfig : BasePluginConfig
{
    [JsonPropertyName("PluginEnabled")] public bool PluginEnabled { get; set; } = true;
    [JsonPropertyName("CampTime")] public int CampTime { get; set; } = 15;
    [JsonPropertyName("SlapORSlayPunishment")] public int SlapORSlayPunishment { get; set; } = 1;
    [JsonPropertyName("SlapDamage")] public int SlapDamage { get; set; } = 5;
    [JsonPropertyName("MinHealthReserve")] public int MinHealthReserve { get; set; } = 15;
    [JsonPropertyName("FirstPunishDelay")] public float FirstPunishDelay { get; set; } = 2.0f;
    [JsonPropertyName("PunishFrequency")] public float PunishFrequency { get; set; } = 2.0f;
    [JsonPropertyName("CampCheckRadius")] public int CampCheckRadius { get; set; } = 120;
    [JsonPropertyName("PunishBeacon")] public bool PunishBeacon { get; set; } = true;
    [JsonPropertyName("PunishAnyway")] public bool PunishAnyway { get; set; } = true;
    [JsonPropertyName("AllowTCampHostageMap")] public bool AllowTCamp { get; set; } = true;
    [JsonPropertyName("AllowTCampBombPlanted")] public bool AllowTCampBombPlanted { get; set; } = true;
    [JsonPropertyName("AllowCTCampBombMap")] public bool AllowCTCamp { get; set; } = true;
    [JsonPropertyName("AllowCtCampBombDropped")] public bool AllowCtCampBombDropped { get; set; } = true;
}
public class SLAYER_AntiCamp : BasePlugin, IPluginConfig<SLAYER_AntiCampConfig>
{
    public override string ModuleName => "SLAYER_AntiCamp";
    public override string ModuleVersion => "1.2.2";
    public override string ModuleAuthor => "SLAYER";
    public override string ModuleDescription => "Detect and Punish Campers";
    public required SLAYER_AntiCampConfig Config {get; set;}
    public void OnConfigParsed(SLAYER_AntiCampConfig config)
    {
        Config = config;
    }
    
    public bool IsBombMap = false, IsHostageMap = false;
    public bool TeamsHaveAlivePlayers = false;
    Dictionary<CCSPlayerController, bool> IsPlayerAFK = new Dictionary<CCSPlayerController, bool>();
    Dictionary<CCSPlayerController, int> PlayertimerCount = new Dictionary<CCSPlayerController, int>();
    Dictionary<CCSPlayerController, Vector> PlayersLastPos = new Dictionary<CCSPlayerController, Vector>();
    Dictionary<CCSPlayerController, Vector> PlayerSlapPosition = new Dictionary<CCSPlayerController, Vector>();
    Dictionary<CCSPlayerController, Vector> PlayerEyeAngleSpawn = new Dictionary<CCSPlayerController, Vector>();
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>? CampersTimer = new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>? CampersPunishTimer = new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>? CampersDelayTimer = new Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer>();
    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnMapStart>((mapname)=>
        {
            IsPlayerAFK.Clear();
            PlayertimerCount.Clear();
            PlayersLastPos.Clear();
            PlayerSlapPosition.Clear();
            PlayerEyeAngleSpawn.Clear();
            CampersTimer.Clear();
            CampersPunishTimer.Clear();
            CampersDelayTimer.Clear();
            return;
        });
        RegisterEventHandler<EventRoundStart>((@event, info)=>
        {
            if(!Config.PluginEnabled)return HookResult.Continue;
            foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBombTarget>("func_bomb_target"))
            {
                IsBombMap = true;
            }
            foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CHostageRescueZone>("func_hostage_rescue"))
            {
                IsHostageMap = true;
            }
            return HookResult.Continue;
        });
        
    }
    [GameEventHandler]
    public HookResult EventRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if(!Config.PluginEnabled)return HookResult.Continue;
        // Check if booth Teams have alive players
        TeamsHaveAlivePlayers = CheckAliveTeams();

        foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum > 1 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if(TeamsHaveAlivePlayers && CampersTimer?[player] != null)
            {
                ResetTimer(player);
            }
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if(!Config.PluginEnabled|| player == null || !player.IsValid || player.TeamNum < 2 )return HookResult.Continue;
        
        TeamsHaveAlivePlayers = CheckAliveTeams();
        PlayertimerCount[player] = 0;
        PlayerEyeAngleSpawn[player] = new Vector(0,0,0);
        PlayerSlapPosition[player] = new Vector(0,0,0);
        ResetTimer(player);
        // Allow camping for t on cs maps if enabled
        if(IsHostageMap && Config.AllowTCamp && player.TeamNum == 2)
            return HookResult.Continue;

        // Allow camping for ct on de maps if enabled
        if(IsBombMap && Config.AllowCTCamp && player.TeamNum == 3)
            return HookResult.Continue;

        // get the players position and start the timing cycle
        PlayersLastPos[player] = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);
        CampersTimer[player] = AddTimer(1.0f, ()=>CheckCamperTimer(player), TimerFlags.REPEAT);
        
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult EventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if(!Config.PluginEnabled)return HookResult.Continue;

        // Check if booth Teams have alive players
        TeamsHaveAlivePlayers = CheckAliveTeams();
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult EventBombDropped(EventBombDropped @event, GameEventInfo info)
    {
        if(!Config.PluginEnabled)return HookResult.Continue;
        
        if(Config.AllowCtCampBombDropped && !Config.AllowCTCamp)
        {
            foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum == 3 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
            {
                if(CampersTimer.ContainsKey(player) && CampersTimer[player] != null)
                {
                    ResetTimer(player);
                }
            }
        }

        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult EventBombPickup(EventBombPickup @event, GameEventInfo info)
    {
        if(!Config.PluginEnabled)return HookResult.Continue;

        if(Config.AllowCtCampBombDropped && !Config.AllowCTCamp)
        {
            foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum == 3 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
            {
                if(!CampersTimer.ContainsKey(player) || (CampersTimer.ContainsKey(player) && CampersTimer[player] == null))
                {
                    PlayersLastPos[player] = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);
                    CampersTimer[player] = AddTimer(1.0f, ()=> CheckCamperTimer(player), TimerFlags.REPEAT);
                }
            }
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult EventBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        if(!Config.PluginEnabled)return HookResult.Continue;

        if(Config.AllowTCampBombPlanted)
        {
            foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum == 2 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
            {
                if(CampersTimer.ContainsKey(player) && CampersTimer[player] != null)
                {
                    ResetTimer(player);
                }
            }
        }

        return HookResult.Continue;
    }
    
    private bool IsCamping(CCSPlayerController? player)
    {
        if(player == null || !player.IsValid)return false;
        
        if(CalculateDistance(PlayersLastPos[player], player.PlayerPawn.Value.AbsOrigin) < Config.CampCheckRadius)
        {
            if(!IsPlayerAFK[player])
                if(player.PlayerPawn.Value.Health > Config.MinHealthReserve || Config.PunishAnyway)
                    {return true;}
        }
        else if(IsPlayerAFK[player])
            IsPlayerAFK[player] = false;

        PlayersLastPos[player] = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);
        return false;
    }
    private bool CheckAliveTeams()
    {
        int alivect = 0, alivet = 0;
        foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum > 1 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if(player.TeamNum == 3)alivect++;
            else if(player.TeamNum == 2)alivet++;
        }
        if(alivect > 0 && alivet > 0)return true;
        else return false;
    }
    public void CheckCamperTimer(CCSPlayerController? player)
    {
        // check to make sure the client is still connected and there are players in both teams
        if(!TeamsHaveAlivePlayers || !player.IsValid || player.IsHLTV || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            ResetTimer(player);
            return;
        }
        // store client's eye angle for afk checking
        if(PlayerEyeAngleSpawn[player].X == 0.0)
        {
            IsPlayerAFK[player] = true;
            PlayerEyeAngleSpawn[player] = new Vector(player.PlayerPawn.Value.EyeAngles.X, player.PlayerPawn.Value.EyeAngles.Y, player.PlayerPawn.Value.EyeAngles.Z) ;
        }
        else
        {
            Vector ClientEyeAng  = new Vector(player.PlayerPawn.Value.EyeAngles.X, player.PlayerPawn.Value.EyeAngles.Y, player.PlayerPawn.Value.EyeAngles.Z) ;
            if(Math.Abs(PlayerEyeAngleSpawn[player].X - ClientEyeAng.X) > 15.0)
                IsPlayerAFK[player] = false;
        }

        if(PlayersLastPos[player].X != 0.0 && IsCamping(player))
        {
            // it looks like this person may be camping, time to get serious
            if(CampersTimer.ContainsKey(player))CampersTimer[player].Kill();
            CampersTimer[player] = AddTimer(1.0f, ()=>CaughtCampingTimer(player), TimerFlags.REPEAT);
        }
        return;
    }
    public void CaughtCampingTimer(CCSPlayerController? player)
    {
        // check to make sure the client is still connected and there are players in both teams
        if(!TeamsHaveAlivePlayers || player == null || !player.IsValid || player.IsHLTV || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2 || !PlayertimerCount.ContainsKey(player))
        {
            ResetTimer(player);
            return;
        }
        PlayerSlapPosition[player] = new Vector(0,0,0);
        if(PlayertimerCount[player] < Config.CampTime)
        {
            if(IsCamping(player))
            {
                PlayertimerCount[player]++;
                return;
            }
            else
            {
                ResetTimer(player);
                PlayertimerCount[player] = 0;
                CampersTimer[player] = AddTimer(1.0f, ()=>CheckCamperTimer(player), TimerFlags.REPEAT);
                return;
            }
        }
        else
        {
            if(player.PlayerPawn.Value.WeaponServices != null && player.PlayerPawn.Value!.WeaponServices.ActiveWeapon.Value != null)
            {
                if(string.IsNullOrEmpty(player.PlayerPawn.Value.LastPlaceName))Server.PrintToChatAll($"{Localizer["Chat.Tag"]} {Localizer["Chat.Camping", player.PlayerName, player.PlayerPawn.Value!.WeaponServices!.ActiveWeapon!.Value!.DesignerName]}");
                else Server.PrintToChatAll($"{Localizer["Chat.Tag"]} {Localizer["Chat.Camping", player.PlayerName, player.PlayerPawn.Value!.WeaponServices!.ActiveWeapon!.Value!.DesignerName]} {ChatColors.Gold}@{player.PlayerPawn.Value.LastPlaceName}");
            }
            
            // reset camp counter
            PlayertimerCount[player] = 0;

            // start beacon timer
            if(Config.FirstPunishDelay == Config.PunishFrequency)
            {
                CampersPunishTimer[player] = AddTimer(Config.FirstPunishDelay, ()=> PunishTimer(player), TimerFlags.REPEAT);
            }
            else if(Config.FirstPunishDelay <= 0)
            {
                CampersPunishTimer[player] = AddTimer(0.1f, ()=> PunishTimer(player), TimerFlags.REPEAT);
                CampersDelayTimer[player] = AddTimer(0.1f, ()=> PunishDelayTimer(player));
            }
            else
            {
                CampersPunishTimer[player] = AddTimer(Config.FirstPunishDelay, ()=> PunishTimer(player), TimerFlags.REPEAT);
                CampersDelayTimer[player] = AddTimer(Config.FirstPunishDelay, ()=> PunishDelayTimer(player));
            }

            // start camp timer
            if(CampersTimer.ContainsKey(player))CampersTimer[player].Kill();
            CampersTimer[player] = AddTimer(1.0f, ()=> CamperTimer(player), TimerFlags.REPEAT);
        }    
        return;
    }
    public void CamperTimer(CCSPlayerController? player)
    {
        // check to make sure the client is still connected and there are players in both teams
        if(!TeamsHaveAlivePlayers || !player.IsValid || player.IsHLTV || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            ResetTimer(player);
            return;
        }
        // After one Punish again check if still camping
        if(PlayerSlapPosition[player].X != 0.0 && CalculateDistance(PlayerSlapPosition[player], player.PlayerPawn.Value.AbsOrigin) > Config.CampCheckRadius && !IsCamping(player))
        {
            ResetTimer(player);
            CampersTimer[player] = AddTimer(1.0f, ()=>CheckCamperTimer(player), TimerFlags.REPEAT);
        }
        return;
    }
    public void PunishTimer(CCSPlayerController? player)
    {
        // check to make sure the client is still connected and there are players in both teams
        if(!TeamsHaveAlivePlayers || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            ResetTimer(player);
            return;
        }
        if(Config.PunishBeacon)DrawBeaconOnPlayer(player);
        int ClientHealth = player.PlayerPawn.Value.Health;
        switch(Config.SlapORSlayPunishment)
        {
            case 1:
            {
                if(ClientHealth > Config.MinHealthReserve || Config.MinHealthReserve <= 0)
                {
                    PerformSlap(player, Config.SlapDamage);
                }
                else if(Config.PunishAnyway && Config.MinHealthReserve > 0)
                {
                    PerformSlap(player, 0);
                }
                    
                break;
            }
            case 2:
            {
                // slay player
                if(ClientHealth > Config.MinHealthReserve)
                {
                    player.PlayerPawn.Value.CommitSuicide(false, true);
                }
                break;
            }
        }

        return;
    }
    public void PunishDelayTimer(CCSPlayerController? player)
    {
        // check to make sure the client is still connected and there are players in both teams
        if(!TeamsHaveAlivePlayers || !player.IsValid || player.IsHLTV || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
        {
            ResetTimer(player);
            return;
        }
        if(CampersPunishTimer.ContainsKey(player))CampersPunishTimer[player].Kill();
        CampersPunishTimer[player] = AddTimer(Config.PunishFrequency, ()=> PunishTimer(player), TimerFlags.REPEAT);
        
        return;
    }

    private void ResetTimer(CCSPlayerController? player)
    {
        if(CampersTimer.ContainsKey(player) && CampersTimer[player] != null){CampersTimer[player].Kill();CampersTimer.Remove(player);}
        if(CampersDelayTimer.ContainsKey(player) && CampersDelayTimer[player] != null){CampersDelayTimer[player].Kill();CampersDelayTimer.Remove(player);}
        if(CampersPunishTimer.ContainsKey(player) && CampersPunishTimer[player] != null) // The reason of adding timer on it because otherwise it crash server
        {
            AddTimer(0.2f, ()=>{CampersPunishTimer[player].Kill();CampersPunishTimer.Remove(player);});
            
        }
    }
    private void PerformSlap(CCSPlayerController? player, int damage = 0)
    {
        if(player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return;

        PlayerSlapPosition[player] = new Vector(0,0,0);
        // Saving Player Position Before Slap
        PlayersLastPos[player] = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);
        var pawn = player.Pawn.Value;
        if (pawn.LifeState != (int)LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        /* Teleport in a random direction - thank you, Mani!*/
	    /* Thank you AM & al!*/
        var random = new Random();
        var vel = new Vector(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
        
        vel.X += random.Next(100, 200) * ((random.Next(2) == 1) ? -1 : 1);
        vel.Y += random.Next(100, 200) * ((random.Next(2) == 1) ? -1 : 1);
        vel.Z += random.Next(200, 300);
        
        pawn.Teleport(pawn.AbsOrigin!, pawn.AbsRotation!, vel);
        
        if (damage > 0)
        {
            pawn.Health -= damage;

            if (pawn.Health <= 0)
                pawn.CommitSuicide(false, true);
        }
        // Saving Player Position after Slap
        AddTimer(0.5f, ()=> {PlayerSlapPosition[player] = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z);});
    }
    private Vector angle_on_circle(float angle, float radius, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (radius * Math.Cos(angle))),(float)(mid.Y + (radius * Math.Sin(angle))), mid.Z + 6.0f);
    }
    public void DrawBeaconOnPlayer(CCSPlayerController? player)
    {
        if(player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return;
        
        Vector mid =  new Vector(player?.PlayerPawn.Value.AbsOrigin.X,player?.PlayerPawn.Value.AbsOrigin.Y,player?.PlayerPawn.Value.AbsOrigin.Z);

        int lines = 20;
        int[] ent = new int[lines];
        CBeam[] beam_ent = new CBeam[lines];

        // draw piecewise approx by stepping angle
        // and joining points with a dot to dot
        float step = (float)(2.0f * Math.PI) / (float)lines;
        float radius = 20.0f;

        float angle_old = 0.0f;
        float angle_cur = step;

        float BeaconTimerSecond = 0.0f;

        
        for(int i = 0; i < lines; i++) // Drawing Beacon Circle
        {
            Vector start = angle_on_circle(angle_old, radius, mid);
            Vector end = angle_on_circle(angle_cur, radius, mid);

            if(player.TeamNum == 2)
            {
                var result = DrawLaserBetween(start, end, Color.Red, 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            } 
            if(player.TeamNum == 3)
            {
                var result = DrawLaserBetween(start, end, Color.Blue, 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            }

            angle_old = angle_cur;
            angle_cur += step;
        }
        
        AddTimer(0.1f, ()=>
        {
            if (BeaconTimerSecond >= 0.9f)
            {
                return;
            }
            for(int i = 0; i < lines; i++) // Moving Beacon Circle
            {
                Vector start = angle_on_circle(angle_old, radius, mid);
                Vector end = angle_on_circle(angle_cur, radius, mid);

                TeleportLaser(beam_ent[i], start, end);

                angle_old = angle_cur;
                angle_cur += step;
            }
            radius += 10;
            BeaconTimerSecond += 0.1f;
        }, TimerFlags.REPEAT);
        PlaySoundOnPlayer(player, "sounds/tools/sfm/beep.vsnd_c");
        return;
    }
    private void PlaySoundOnPlayer(CCSPlayerController? player, String sound)
    {
        if(player == null || !player.IsValid)return;
        player.ExecuteClientCommand($"play {sound}");
    }
    

    private static readonly Vector VectorZero = new Vector(0, 0, 0);
    private static readonly QAngle RotationZero = new QAngle(0, 0, 0);
    public (int, CBeam) DrawLaserBetween(Vector startPos, Vector endPos, Color color, float life, float width)
    {
        if (startPos == null || endPos == null)
            return (-1, null);

        CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");

        if (beam == null || !beam.IsValid)
        {
            Logger.LogError($"Failed to create beam...");
            return (-1, null);
        }

        beam.Render = color;
        beam.Width = width;

        beam.Teleport(startPos, RotationZero, VectorZero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        AddTimer(life, () => { beam.Remove(); }); // destroy beam after specific time

        return ((int)beam.Index, beam);
    }
    public void TeleportLaser(CBeam? laser,Vector start, Vector end)
    {
        if(laser == null || !laser.IsValid)return;
        // set pos
        laser.Teleport(start, RotationZero, VectorZero);
        // end pos
        // NOTE: we cant just move the whole vec
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;
        Utilities.SetStateChanged(laser,"CBeam", "m_vecEndPos");
    }
    private float CalculateDistance(Vector point1, Vector point2)
    {
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;
        float dz = point2.Z - point1.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}