using BepInEx.Configuration;
using Il2CppInterop.Runtime;

namespace RetainAutoplay;

internal sealed class StoryPreferences
{
    readonly Plugin plugin;
    readonly ConfigEntry<bool> remember, hasChoice;
    readonly ConfigEntry<int> mode;
    IntPtr scenario;
    string storyId = "";
    bool failed;

    public StoryPreferences(Plugin plugin)
    {
        this.plugin = plugin;
        remember = plugin.Config.Bind("Story", "RememberAuto", true, "Restore the last manually selected Auto mode once at the beginning of each story node. Independent of UI scaling.");
        hasChoice = plugin.Config.Bind("Story", "HasAutoPreference", false, "Whether the player has selected Auto since installing this feature.");
        mode = plugin.Config.Bind("Story", "LastAutoMode", 0, new ConfigDescription("Last manually selected Auto mode, including the game's faster Auto mode.", new AcceptableValueRange<int>(0, 2)));
    }

    public void Install()
    {
        Guard(() =>
        {
            plugin.HookObjectAction(GameClass("StoryRightTopUIController"), "OnClickStoryUIButton", (controller, button) => Guard(() =>
            {
                if (!remember.Value || button == IntPtr.Zero || U.Read<int>(button, "_storyUIButtonType") != 1) return;
                mode.Value = U.Read<int>(button, "_autoButtonState");
                hasChoice.Value = true;
                plugin.Config.Save();
                plugin.LogMessage($"Story Auto preference saved: {mode.Value}");
            }));
            plugin.HookInstance(GameClass("StoryManager"), "Update", (manager, original) =>
            {
                original();
                Guard(() => RestoreNode(manager));
            }, featureHook: true);
        });
    }

    void RestoreNode(IntPtr manager)
    {
        var current = U.Read<IntPtr>(manager, "_readingScenario");
        if (current == IntPtr.Zero) { scenario = IntPtr.Zero; storyId = ""; return; }
        var idPointer = U.Read<IntPtr>(manager, "_storyID");
        string id = idPointer == IntPtr.Zero ? "" : IL2CPP.Il2CppStringToManaged(idPointer) ?? "";
        if (scenario == current && storyId == id) return;
        if (U.Get<int>(manager, "CurrentDialogIndex") < 0) return;
        var controller = U.Read<IntPtr>(manager, "_rightTopButton");
        var auto = U.Read<IntPtr>(manager, "_autoManager");
        if (controller == IntPtr.Zero || auto == IntPtr.Zero) return;
        var buttonTransform = U.Find(U.Transform(controller), "[Button]Auto", 5);
        if (buttonTransform == IntPtr.Zero) return;
        var button = U.Component(U.GO(buttonTransform), GameClass("StoryUIButton"));
        if (button == IntPtr.Zero) return;
        scenario = current; storyId = id;
        if (!remember.Value || !hasChoice.Value) return;
        U.Write(button, "_autoButtonState", mode.Value);
        U.Call(button, "ChangeButtonImage");
        U.ValueArg(auto, "SetAuto", mode.Value);
        plugin.LogMessage($"Story Auto preference restored for {id}: {mode.Value}");
    }

    static IntPtr GameClass(string name) => U.Class(name, name == "StoryUIButton" ? "" : "StorySystem", "Assembly-CSharp.dll");
    void Guard(Action action)
    {
        if (failed) return;
        try { action(); }
        catch (Exception ex)
        {
            failed = true;
            plugin.LogMessage($"Story Auto persistence disabled after an error: {ex}. ", true);
        }
    }
}
