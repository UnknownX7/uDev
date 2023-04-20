using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace uDev;

[HypostasisInjection]
public static unsafe class GameDebug
{
    [HypostasisDebuggable]
    private static GameObject* LocalPlayer => Common.GetGameObjectFromPronounID(PronounID.Me);

    [HypostasisDebuggable]
    private static GameObject* Target => Common.GetGameObjectFromPronounID(PronounID.Target);

    [HypostasisDebuggable]
    private static GameObject* FocusTarget => Common.GetGameObjectFromPronounID(PronounID.FocusTarget);
}