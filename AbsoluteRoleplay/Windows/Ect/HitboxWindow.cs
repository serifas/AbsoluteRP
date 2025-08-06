using AbsoluteRoleplay;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using static Lumina.Data.Parsing.Layer.LayerCommon;

public class HitboxWindow : Window
{
    private string content;
    public static Plugin plugin;
    public static IPlayerCharacter Player;
    public static IGameGui gameGui;
    public HitboxWindow(string title, IPlayerCharacter player, IGameGui gui) : base(title)
    {
        this.Size = new Vector2(200, 200);
        gameGui = gui;
        Player = player;
    }

    public override void Draw()
    {
        try
        {
            if (Player != null && gameGui != null)
            {
                DrawTooltipHitbox(Player, gameGui, 0.300f);
            }
        }catch(Exception ex)
        {
            Plugin.logger.Error("HitboxWindow Draw Error: " + ex.Message);
        }
    }
    public static void DrawTooltipHitbox(IPlayerCharacter player, IGameGui gui, float radius)
    {
        if (player == null) return;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
        ImGui.Begin("Hitbox",
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing);
        ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);

    }
}
