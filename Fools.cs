using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;

namespace SimpleTweaksPlugin; 

public static unsafe class Fools {
    public static bool IsFoolsDay => DateTime.Now is { Month: 4, Day: 1 };
    private static readonly ushort[] statuses = { 3, 227, 937 };
    
    public static void FrameworkUpdate() {
        noticeIsVisible = false;
        if (!IsFoolsDay) return;
        if (Service.Condition[ConditionFlag.OccupiedInQuestEvent]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene78]) return;
        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;
        for (var i = 200; i < 243; i++) {
            if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
            var o = (BattleChara*) GameObjectManager.GetGameObjectByIndex(i);
            if (o == null) continue;
            if (statuses.Any(s => o->StatusManager.HasStatus(s))) {
                noticeIsVisible = true;
                continue;
            }
            var r = (ushort)new Random().Next(0, 100);
            if (r >= statuses.Length) r = (ushort)(r % 2);
            o->StatusManager.AddStatus(statuses[r % statuses.Length]);
            Service.PluginInterface.UiBuilder.Draw -= BigBabyWarningForBigBabiesWhoLikeToCryAllDayLongAboutSimpleJokesThatDontChangeAnythingAboutHowTheFuckingGameIsPlayedGetOverYourself;
            Service.PluginInterface.UiBuilder.Draw += BigBabyWarningForBigBabiesWhoLikeToCryAllDayLongAboutSimpleJokesThatDontChangeAnythingAboutHowTheFuckingGameIsPlayedGetOverYourself;
        }
    }


    private static bool noticeIsVisible = false;
    public static void BigBabyWarningForBigBabiesWhoLikeToCryAllDayLongAboutSimpleJokesThatDontChangeAnythingAboutHowTheFuckingGameIsPlayedGetOverYourself() {
        if (!noticeIsVisible) return;
        if (SimpleTweaksPlugin.Plugin.PluginConfig.NotBaby) return;
        var text = "YOU ARE VIEWING AN APRIL FOOLS JOKE FROM SIMPLE TWEAKS\nYOU CAN DISABLE THIS IN THE SIMPLE TWEAKS CONFIG\nNOTICES MAKE JOKES EVEN MORE FUN, SEE";
        var dl = ImGui.GetForegroundDrawList();

        for (var x = -5; x <= 5; x++) {
            for (var y = -5; y <= 5; y++) {
                dl.AddText(UiBuilder.DefaultFont, UiBuilder.DefaultFont.FontSize * 1.5f,  ImGui.GetMainViewport().Pos + ImGui.GetMainViewport().Size * 0.05f + new Vector2(x, y), 0xFF000000, text);
            }
        }
        
        ImGui.ColorConvertHSVtoRGB((Service.PluginInterface.UiBuilder.FrameCount % 512) / 512f, 1, 1, out var r, out var g, out var b);
        var shadowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, 1));
        for (var x = -3; x <= 3; x++) {
            for (var y = -3; y <= 3; y++) {

                dl.AddText(UiBuilder.DefaultFont, UiBuilder.DefaultFont.FontSize * 1.5f,  ImGui.GetMainViewport().Pos + ImGui.GetMainViewport().Size * 0.05f + new Vector2(x, y), shadowColor, text);
            }
        }
        
        for (var x = -1; x <= 1; x++) {
            for (var y = -1; y <= 1; y++) {
                dl.AddText(UiBuilder.DefaultFont, UiBuilder.DefaultFont.FontSize * 1.5f,  ImGui.GetMainViewport().Pos + ImGui.GetMainViewport().Size * 0.05f + new Vector2(x, y), 0xFF000000, text);
            }
        }
        
        dl.AddText(UiBuilder.DefaultFont, UiBuilder.DefaultFont.FontSize * 1.5f,  ImGui.GetMainViewport().Pos + ImGui.GetMainViewport().Size * 0.05f, 0xFFFFFFFF, text);
    }

    public static void Reset() {
        Service.PluginInterface.UiBuilder.Draw -= BigBabyWarningForBigBabiesWhoLikeToCryAllDayLongAboutSimpleJokesThatDontChangeAnythingAboutHowTheFuckingGameIsPlayedGetOverYourself;
        for (var i = 200; i < 243; i++) {
            if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
            var o = (BattleChara*) GameObjectManager.GetGameObjectByIndex(i);
            if (o == null) continue;
            foreach (var s in statuses) {
                if (o->StatusManager.HasStatus(s)) {
                    o->StatusManager.RemoveStatus(s);
                }
            }
        }
    }
}
