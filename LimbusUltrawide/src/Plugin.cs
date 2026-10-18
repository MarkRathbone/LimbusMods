using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using BepInEx.Unity.IL2CPP.Hook;
using System.Runtime.InteropServices;

namespace UltrawideLimbus;

[BepInPlugin("local.limbus.verticalui", "Limbus Ultrawide", "0.9.57")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal ConfigEntry<bool> Enabled = null!, Battle = null!, Panels = null!, Navigation = null!, Popups = null!, Camera = null!, Results = null!, Background = null!;
    internal ConfigEntry<float> MenuSize = null!, BattleSize = null!, MenuWeight = null!, BattleWeight = null!, PopupGap = null!, EdgeInset = null!, PanelInset = null!, PanelOffset = null!, ShadeOffset = null!, CameraOffset = null!, ResultsWidth = null!, ResultsOffset = null!;
    internal ConfigEntry<string> MenuCanvases = null!, BattleCanvases = null!;
    internal ConfigEntry<bool> MenuSafeArea = null!, MenuBackgrounds = null!;
    internal ConfigEntry<float> MenuAspect = null!;
    internal ConfigEntry<bool> AutoMenus = null!, MenuOverlays = null!;
    internal ConfigEntry<string> ExcludedCanvases = null!, ScreenEdgeCanvases = null!;
    internal ConfigEntry<string> BackdropPanels = null!;
    internal ConfigEntry<bool> OpaquePanelBackdrops = null!;
    internal ConfigEntry<bool> PreservePanelShades = null!;
    internal ConfigEntry<bool> PreservePanelBackgrounds = null!;
    internal ConfigEntry<bool> WideGiftGrid = null!;
    internal ConfigEntry<bool> EventLayout = null!;
    internal ConfigEntry<bool> ShopBackground = null!;
    internal ConfigEntry<float> GiftMargin = null!, GiftGap = null!;
    internal ConfigEntry<int> GiftRows = null!;
    internal ConfigEntry<float> GiftMinimumScale = null!;
    internal ConfigEntry<bool> ArtworkConstrain = null!;
    internal ConfigEntry<bool> TraceLargeGraphics = null!;
    internal ConfigEntry<float> StorySize = null!;
    internal ConfigEntry<bool> StoryLayout = null!;
    internal ConfigEntry<bool> EdgePanels = null!;
    internal ConfigEntry<bool> MenuSlidePanels = null!;
    internal ConfigEntry<bool> CinematicViewport = null!;
    internal ConfigEntry<bool> TraceEgo = null!;
    internal ConfigEntry<bool> RpgViewport = null!;
    readonly EgoDiagnostics egoDiagnostics = new();
    internal ConfigEntry<bool> TraceTransition = null!;
    readonly TransitionDiagnostics transitionDiagnostics = new();
    internal ConfigEntry<string> EdgePanelRoots = null!;
    readonly List<INativeDetour> detours = new();
    readonly List<Delegate> delegates = new();
    readonly List<CanvasState> canvases = new();
    readonly Layout layout = new();
    SettingsControl? settings;
    internal bool ActiveLayout => Enabled.Value && settings?.IsUltrawide == true;
    bool inside, failed;
    internal bool Discover = true;
    int frames;
    ConfigEntry<string> hookFilter = null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void InstanceAction(IntPtr obj, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StaticAction(IntPtr method);

    public override void Load()
    {
        Instance = this;
        TraceTransition = Config.Bind("Diagnostics", "TraceTransition", true, "Bounded read-only capture of live Sisyphe formation and scene fade layers, including recreated retry instances.");
        RpgViewport = Config.Bind("RPG", "ViewportPanels", true, "Use full-window RPG HUD and popup dimming bounds while preserving native inner frames. Initial asset-based support for Grand Magasin Sisyphe.");
        TraceEgo = Config.Bind("Diagnostics", "TraceEgo", false, "Temporarily log EGO transition and banner geometry; read-only and capped.");
        hookFilter = Config.Bind("Diagnostics", "Hooks", "Handle|OnEnable|SendPreWillRenderCanvases|Press|LateUpdate", "Enabled Unity hook names. Keep the default for normal use.");
        TraceLargeGraphics = Config.Bind("Diagnostics", "TraceLargeGraphics", false, "Log newly observed large UI graphics with their hierarchy, color and sprite to diagnose background ownership.");
        Enabled = Config.Bind("General", "Enabled", true, "Allow automatic ultrawide layout when the applied Resolution is wider than 16:9.");
        Battle = Config.Bind("Battle", "Enabled", true);
        CinematicViewport = Config.Bind("Battle", "CinematicViewport", true, "Keep attack-frame effects and TOTAL counters in one full-viewport layout, using the battle scaling profile.");
        Panels = Config.Bind("Inspection", "ResponsivePanels", true);
        Navigation = Config.Bind("Inspection", "NavigationArrows", true);
        Popups = Config.Bind("Inspection", "AttachSkillPopups", true);
        Camera = Config.Bind("Inspection", "CenterCamera", true);
        Results = Config.Bind("Results", "Enabled", true);
        Background = Config.Bind("Results", "OpaqueFullScreenBackground", true);
        MenuSize = Float("Menu", "SizeMultiplier", .8f, .25f, 4);
        BattleSize = Float("Battle", "SizeMultiplier", .8f, .25f, 4);
        MenuWeight = Float("Menu", "HeightWeight", 1, 0, 1);
        BattleWeight = Float("Battle", "HeightWeight", 1, 0, 1);
        StoryLayout = Config.Bind("Story", "PreserveStageComposition", true, "Use the story's authored stage and aspect handling instead of generic menu containment/background expansion.");
        StorySize = Float("Story", "SizeMultiplier", 1, .25f, 4);
        PopupGap = Float("Inspection", "PopupGap", 40, 0, 500);
        EdgeInset = Float("Inspection", "EdgeArrowInset", 64, 0, 500);
        PanelInset = Float("Inspection", "PanelArrowInset", 109, 0, 500);
        PanelOffset = Float("Inspection", "PanelOffset", 0, -3000, 3000);
        ShadeOffset = Float("Inspection", "ShadingOffset", 0, -3000, 3000);
        CameraOffset = Float("Inspection", "CameraViewportOffset", 0, -.2f, .2f);
        ResultsWidth = Float("Results", "ContentWidth", 0, 0, 16000);
        ResultsOffset = Float("Results", "HorizontalOffset", 0, -3000, 3000);
        MenuSafeArea = Config.Bind("Menu", "CenteredContent", true, "Keep menu content in a centered reference aspect ratio; backgrounds can fill the screen.");
        MenuSlidePanels = Config.Bind("Menu", "ResponsiveSlidePanels", true, "Scale off-screen travel of full-frame sliding menu panels to the viewport, preserving native animation positions and visible content size.");
        MenuBackgrounds = Config.Bind("Menu", "FillBackgrounds", true, "Extend background artwork beyond the content area.");
        MenuAspect = Float("Menu", "ContentAspectRatio", 16f / 9f, 1, 4);
        MenuOverlays = Config.Bind("Menu", "FullScreenOverlays", true, "Keep large vignettes, fades and dimming overlays at the actual screen edges.");
        EdgePanels = Config.Bind("Menu", "ScreenEdgePanels", true, "Allow shared stage-detail panels to anchor to the actual screen edge, including their shading and controls.");
        EdgePanelRoots = Config.Bind("Targets", "ScreenEdgePanels", "[UIPanel]StageInfoUIRenewal2", "Shared panel layout roots receiving viewport bounds instead of centered menu bounds. Exact names separated by |.");
        PreservePanelShades = Config.Bind("Menu", "PreservePanelShades", true, "Keep oversized shading graphics attached to fixed panels anchored at a screen edge, rather than stretching their gradients across the viewport.");
        PreservePanelBackgrounds = Config.Bind("Menu", "PreservePanelBackgrounds", true, "Keep decorative backgrounds inside fixed-size popup panels at their authored size. Full-screen dimming overlays remain separate.");
        WideGiftGrid = Config.Bind("Gift selection", "UseAvailableSpace", true, "Expand the starting gift-category grid into available screen space and keep scrolling only when required.");
        EventLayout = Config.Bind("Events", "ResponsiveChoices", true, "Center story and choices together and expand the choices list when its contents fit on screen.");
        ShopBackground = Config.Bind("Menu", "ShopBackground", true, "Center and cover the viewport with dungeon-shop room artwork, with opaque backing behind transparent edges.");
        GiftMargin = Float("Gift selection", "ScreenMargin", 120, 0, 500);
        GiftGap = Float("Gift selection", "DescriptionGap", 80, 0, 500);
        GiftRows = Config.Bind("Gift selection", "PreferredRows", 3, new ConfigDescription("Preferred number of category rows in the centered gift-selection group.", new AcceptableValueRange<int>(1, 6)));
        GiftMinimumScale = Float("Gift selection", "MinimumGridScale", .85f, .5f, 1);
        ArtworkConstrain = Config.Bind("Artwork", "ConstrainToContentArea", true, "Preserve default identity/E.G.O artwork framing and cover the areas outside the centered content aspect ratio with solid black.");
        OpaquePanelBackdrops = Config.Bind("Menu", "OpaquePanelBackdrops", true, "Place an opaque full-screen black layer behind supported standalone panels, preserving their artwork above it.");
        BackdropPanels = Config.Bind("Targets", "BackdropPanels", "RailwayDungeonEntranceUIPanel_MultipleLine(Clone)|[Script]UnitInformationController_Renewal", "Panel roots receiving a black backing layer behind their existing children. Battle inspection is excluded. Exact names separated by |.");
        AutoMenus = Config.Bind("Targets", "AutomaticScreenSpaceUI", true, "Apply the menu profile to root screen-space canvases using Scale With Screen Size, including separate mode-specific menus.");
        ExcludedCanvases = Config.Bind("Targets", "ExcludedCanvases", "", "Exact canvas names excluded from automatic scaling, separated by |.");
        ScreenEdgeCanvases = Config.Bind("Targets", "ScreenEdgeCanvases", "Map Canvas|[Canvas]RailwayDungeonPanels|[Script]UIFrontCanvas|[Script]FixedUICanvas", "Keep authored full-screen anchors on these canvases while retaining height-based scaling. Railway map/detail layers use this profile; team selection remains centered. Exact names separated by |.");
        MenuCanvases = Config.Bind("Targets", "MenuCanvases", "[Canvas]RatioMainUI", "Exact canvas names separated by |.");
        BattleCanvases = Config.Bind("Targets", "BattleCanvases", "[Canvas,Script]BattleUIController|[Canvas]BattleFrontUI|[Canvas]TutorialUIManager|[Canvas]PerspectiveUI|[Canvas]SafeAreaBorder");
        var scaler = U.Class("CanvasScaler", "UnityEngine.UI", "UnityEngine.UI.dll");
        HookInstance(scaler, "Handle", (obj, original) =>
        {
            if (inside || failed) { original(); return; }
            inside = true;
            try
            {
                var state = Observe(obj);
                if (state != null)
                {
                    if (Discover && state.Menu) state.Discover();
                    ApplyScale(state);
                }
            }
            catch (Exception ex) { Failure("CanvasScaler.Handle", ex); }
            finally { inside = false; }
            original();
        });
        HookInstance(scaler, "OnEnable", (obj, original) => { Discover = true; original(); });
        HookInstance(U.Class("Graphic", "UnityEngine.UI", "UnityEngine.UI.dll"), "OnEnable", (obj, original) => { Discover = true; original(); });
        HookStatic(U.Class("Canvas", "UnityEngine", "UnityEngine.UIModule.dll"), "SendPreWillRenderCanvases", BeforeLayout);
        settings = new SettingsControl(this);
        InstallCameraHook();
        InstallPartProjectionHook();
        InspectionBuffs.ValidateArrayInterop();
        Log.LogInfo("Launch-loaded UI mod ready. External memory tool is not required.");
    }

    ConfigEntry<float> Float(string section, string name, float value, float min, float max)
        => Config.Bind(section, name, value, new ConfigDescription("UI reference units unless stated otherwise.", new AcceptableValueRange<float>(min, max)));

    internal void HookInstance(IntPtr klass, string method, Action<IntPtr, Action> action, bool featureHook = false)
    {
        if (!featureHook && !hookFilter.Value.Split('|').Contains(method)) return;
        InstanceAction? original = null;
        InstanceAction replacement = (obj, info) => action(obj, () => original!(obj, info));
        var detour = INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass, method)), replacement, out original);
        detours.Add(detour); delegates.Add(replacement); delegates.Add(original!);
        Log.LogInfo($"Hooked {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass))}.{method} at {Marshal.ReadIntPtr(U.Method(klass, method)):X}");
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ObjectAction(IntPtr obj, IntPtr arg, IntPtr method);
    internal void HookObjectAction(IntPtr klass, string method, Action<IntPtr, IntPtr> after)
    {
        ObjectAction? original = null;
        ObjectAction replacement = (obj, arg, info) => { original!(obj, arg, info); after(obj, arg); };
        var hook = INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass, method, 1)), replacement, out original);
        detours.Add(hook); delegates.Add(replacement); delegates.Add(original!);
        Log.LogInfo($"Hooked story event {method}");
    }
    void HookStatic(IntPtr klass, string method, Action action)
    {
        if (!hookFilter.Value.Split('|').Contains(method)) return;
        StaticAction? original = null;
        StaticAction replacement = info => { action(); original!(info); };
        var detour = INativeDetour.CreateAndApply(Marshal.ReadIntPtr(U.Method(klass, method)), replacement, out original);
        detours.Add(detour); delegates.Add(replacement); delegates.Add(original!);
    }
    CanvasState? Observe(IntPtr scaler)
    {
        int id = U.Id(scaler);
        var existing = canvases.FirstOrDefault(c => c.Id == id);
        if (existing != null) return existing;
        string name = U.Name(scaler);
        bool menu = MenuCanvases.Value.Split('|').Contains(name), battle = BattleCanvases.Value.Split('|').Contains(name);
        if (!battle && AutoMenus.Value && !ExcludedCanvases.Value.Split('|').Contains(name) && U.Read<int>(scaler, "m_UiScaleMode") == 1)
        {
            var nativeCanvas = U.Component(U.GO(scaler), U.Class("Canvas", "UnityEngine", "UnityEngine.UIModule.dll"));
            if (nativeCanvas != IntPtr.Zero && U.Get<int>(nativeCanvas, "renderMode") != 2 && U.Get<byte>(nativeCanvas, "isRootCanvas") != 0) menu = true;
        }
        bool storyCanvas = name is "[Rect]PostProcessCanvas" or "[Rect]NonPostProcessCanvas" or "[Rect]IntermediateEffectCanvas";
        var state = new CanvasState(scaler, id, menu, battle, storyCanvas);
        canvases.Add(state); Discover = true;
        if (menu || battle) Log.LogInfo($"Registered UI canvas: {name}; reference {state.Original.Reference}");
        return state;
    }
    void ApplyScale(CanvasState state)
    {
        if (!state.Menu && !state.Battle) return;
        bool cinematic = state.Cinematic && CinematicViewport.Value;
        bool egoStage = state.EgoStage;
        bool battle = state.Battle || cinematic || egoStage || (Results.Value && state.HasResults);
        bool enabled = ActiveLayout && (!(state.Battle || cinematic || egoStage) || Battle.Value) && state.Original.Mode == 1;
        bool creditsProfile = U.Name(state.Object) == "CreditsUIManager";
        bool storyProfile = state.Story && StoryLayout.Value;
        bool egoProfile = U.Name(state.Object) == "BattleCutSceneUI";
        var reference = state.Original.Reference;
        if (storyProfile || creditsProfile)
            reference = reference with { Y = LayoutMath.StoryReferenceHeight(reference.X) };
        var desired = enabled
            ? new Scale(1, reference / (egoProfile || creditsProfile ? 1 : storyProfile ? StorySize.Value : battle ? BattleSize.Value : MenuSize.Value), 0, egoProfile || creditsProfile || storyProfile ? 1 : battle ? BattleWeight.Value : MenuWeight.Value)
            : state.Original;
        var obj = state.Object;
        if (U.Read<int>(obj, "m_UiScaleMode") != desired.Mode) U.Write(obj, "m_UiScaleMode", desired.Mode);
        if (U.Read<V2>(obj, "m_ReferenceResolution") != desired.Reference) U.Write(obj, "m_ReferenceResolution", desired.Reference);
        if (U.Read<int>(obj, "m_ScreenMatchMode") != desired.Match) U.Write(obj, "m_ScreenMatchMode", desired.Match);
        if (U.Read<float>(obj, "m_MatchWidthOrHeight") != desired.Weight) U.Write(obj, "m_MatchWidthOrHeight", desired.Weight);
    }
    void BeforeLayout()
    {
        if (inside || failed) return;
        inside = true;
        try
        {
            frames++;
            settings?.Refresh();
            transitionDiagnostics.BeginFrame(canvases, this);
            foreach (var state in canvases.ToArray())
            {
                if (!U.Alive(state.Object)) { state.Dispose(); canvases.Remove(state); Discover = true; continue; }
                if (!U.Active(state.Object)) continue;
                if (Discover) state.Discover();
                if (state.Menu || state.Battle) layout.Apply(state, this);
                egoDiagnostics.Observe(state, this);
                transitionDiagnostics.Observe(state, this);
            }
            egoDiagnostics.EndFrame();
            Discover = false;
        }
        catch (Exception ex) { Failure("pre-layout", ex); }
        finally { inside = false; }
    }
    internal void Failure(string context, Exception ex)
    {
        failed = true;
        Log.LogError($"UI mod paused after {context} failure: {ex}. Restart with Enabled=false to restore normal UI.");
    }
    internal void LogMessage(string message, bool error = false)
    { if (error) Log.LogError(message); else Log.LogInfo(message); }
    void InstallCameraHook()
    {
        var klass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "BattleCamManager");
        if (klass == IntPtr.Zero) { Log.LogWarning("BattleCamManager not resolved; inspection camera port unavailable."); return; }
        HookInstance(klass, "LateUpdate", (obj, original) =>
        {
            try { layout.Camera(obj, this); }
            catch (Exception ex) { Log.LogError($"Inspection camera disabled: {ex.Message}"); Camera.Value = false; }
            original();
        });
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate byte IteratorStep(IntPtr obj, IntPtr method);
    void InstallPartProjectionHook()
    {
        try
        {
            var partClass = U.Class("AbnormalityPartStatusUI", "", "Assembly-CSharp.dll");
            IntPtr iterator = IntPtr.Zero, iter = IntPtr.Zero, nested;
            while ((nested = IL2CPP.il2cpp_class_get_nested_types(partClass, ref iter)) != IntPtr.Zero)
                if ((Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(nested)) ?? "").StartsWith("<UpdatePosition>")) { iterator = nested; break; }
            if (iterator == IntPtr.Zero) throw new MissingMemberException("Part position coroutine");
            var moveNext = U.Method(iterator, "MoveNext");
            if (IL2CPP.il2cpp_method_get_param_count(moveNext) != 0 ||
                Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_method_get_return_type(moveNext)))) != "Boolean")
                throw new InvalidOperationException("Part position coroutine signature changed");
            U.Field(iterator, "<>4__this"); U.Field(partClass, "_canvasScaler");
            IteratorStep original = null!;
            IteratorStep replacement = (obj, method) =>
            {
                if (!ActiveLayout || !Battle.Value || !Panels.Value) return original(obj, method);
                IntPtr scaler = IntPtr.Zero; V2 reference = default; bool changed = false;
                try
                {
                    var owner = U.Read<IntPtr>(obj, "<>4__this");
                    if (owner != IntPtr.Zero) scaler = U.Read<IntPtr>(owner, "_canvasScaler");
                    if (scaler != IntPtr.Zero && U.Alive(scaler))
                    {
                        reference = U.Read<V2>(scaler, "m_ReferenceResolution");
                        var bounds = U.Get<R4>(U.Transform(scaler), "rect");
                        var dimensions = LayoutMath.PartProjectionReference((reference.X, reference.Y), bounds.Width, bounds.Height);
                        var projection = new V2(dimensions.X, dimensions.Y);
                        if (projection != reference)
                        {
                            U.Write(scaler, "m_ReferenceResolution", projection); changed = true;
                        }
                    }
                }
                catch (Exception ex) { Log.LogWarning("Part projection unavailable: " + ex.Message); }
                try { return original(obj, method); }
                finally { if (changed) U.Write(scaler, "m_ReferenceResolution", reference); }
            };
            detours.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(moveNext), replacement, out original));
            delegates.Add(replacement); delegates.Add(original);
            Log.LogInfo("Part-label projection uses actual canvas bounds; native offsets and connectors preserved.");
        }
        catch (Exception ex) { Log.LogWarning("Part projection hook unavailable: " + ex.Message); }
    }
    public override bool Unload()
    {
        Enabled.Value = false;
        BeforeLayout();
        foreach (var state in canvases) { if (U.Alive(state.Object)) ApplyScale(state); state.Dispose(); }
        foreach (var hook in detours.AsEnumerable().Reverse()) hook.Dispose();
        detours.Clear(); delegates.Clear(); canvases.Clear(); settings?.Dispose();
        return true;
    }
}

internal readonly record struct Scale(int Mode, V2 Reference, int Match, float Weight);
internal sealed class CanvasState : IDisposable
{
    readonly nint handle;
    public IntPtr Object => IL2CPP.il2cpp_gchandle_get_target(handle);
    public IntPtr Root => U.Transform(Object);
    public readonly int Id;
    public readonly bool Menu, Battle, Story;
    public bool Cinematic => U.Name(Object) == "[Canvas]BattleRetterBoxUI";
    public bool EgoStage => U.Name(Object) == "BattleCutSceneUI" || U.Find(Root, "[Script]SkillViewCanvas", 1) != IntPtr.Zero;
    public readonly Scale Original;
    nint inspection, results;
    public IntPtr Inspection => inspection == 0 ? IntPtr.Zero : IL2CPP.il2cpp_gchandle_get_target(inspection);
    public IntPtr Results => results == 0 ? IntPtr.Zero : IL2CPP.il2cpp_gchandle_get_target(results);
    public bool HasResults => Results != IntPtr.Zero && U.Alive(Results) && U.Active(Results);
    public CanvasState(IntPtr obj, int id, bool menu, bool battle, bool story)
    {
        Id = id; Menu = menu; Battle = battle; Story = story; handle = IL2CPP.il2cpp_gchandle_new(obj, false);
        Original = new(U.Read<int>(obj, "m_UiScaleMode"), U.Read<V2>(obj, "m_ReferenceResolution"), U.Read<int>(obj, "m_ScreenMatchMode"), U.Read<float>(obj, "m_MatchWidthOrHeight"));
    }
    public void Discover()
    {
        if (Menu) Replace(ref results, U.Find(Root, "BattleResultPanel_renewal(Clone)", 4));
        if (Battle) Replace(ref inspection, U.Find(Root, "[Script]UnitInformationController_Renewal", 5));
    }
    static void Replace(ref nint handle, IntPtr obj)
    {
        if (handle != 0) IL2CPP.il2cpp_gchandle_free(handle);
        handle = obj == IntPtr.Zero ? 0 : IL2CPP.il2cpp_gchandle_new(obj, false);
    }
    public void Dispose()
    {
        if (inspection != 0) IL2CPP.il2cpp_gchandle_free(inspection);
        if (results != 0) IL2CPP.il2cpp_gchandle_free(results);
        IL2CPP.il2cpp_gchandle_free(handle);
    }
}
