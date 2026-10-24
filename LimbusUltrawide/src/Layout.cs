using Il2CppInterop.Runtime;

namespace UltrawideLimbus;

internal sealed class Layout
{
    readonly Dictionary<int, SavedRect> changed = new();
    readonly Dictionary<int, (float Left, float Right)> cameras = new();
    float frameWidth, canvasWidth;
    readonly Dictionary<int, HashSet<int>> backgroundsByCanvas = new();
    readonly Dictionary<int, HashSet<int>> shopBackgroundsByCanvas = new();
    readonly Dictionary<int, HashSet<int>> overlaysByCanvas = new();
    readonly Dictionary<int, Dictionary<int, SavedRect>> backdropsByCanvas = new();
    readonly HashSet<int> panelShades = new();
    readonly ArtworkCurtains artworkCurtains = new();
    readonly WalpurgisLayout walpurgis = new();
    readonly HashSet<int> tracedGraphics = new();
    readonly Dictionary<int, Dictionary<int, ScreenGraphic>> containerBackings = new();
    readonly Dictionary<int, HashSet<int>> edgePanelsByCanvas = new();
    readonly HashSet<int> slidingRoots = new();
    readonly Dictionary<int, GiftGrid> giftGrids = new();
    readonly Dictionary<int, EventChoices> eventChoices = new();
    readonly Dictionary<int, HashSet<int>> edgeStripsByCanvas = new();
    readonly RpgLayout rpgLayout = new();
    readonly Dictionary<int, CultivationFade> cultivationFades = new();
    readonly Dictionary<int, HashSet<int>> outcomeStrips = new();
    readonly Dictionary<int, byte> outcomePreserveAspect = new();
    readonly Dictionary<int, ScreenGraphic> transitionBackings = new();
    readonly HashSet<int> tracedTransitions = new();
    readonly Dictionary<int, int> extractionRoots = new();
    readonly Dictionary<int, InspectionBuffs> inspectionBuffs = new();

    public void Apply(CanvasState canvas, Plugin plugin)
    {
        bool enabled = plugin.ActiveLayout;
        ViewportTransitions(canvas, enabled);
        if (U.Name(canvas.Object) == "CreditsUIManager")
        {
            Credits(canvas, enabled);
            return;
        }
        bool rpg = canvas.Menu && rpgLayout.Apply(canvas, plugin, enabled && plugin.RpgViewport.Value);
        if (canvas.Menu && !rpg && !canvas.EgoStage && !(canvas.Story && plugin.StoryLayout.Value)) Menu(canvas, plugin, enabled);
        if (canvas.Menu) LoadingArtPopup(canvas, plugin, enabled);
        if (canvas.Menu) FormationSelectorBacking(canvas, enabled && plugin.MenuSafeArea.Value);
        if (canvas.Menu) Extraction(canvas, plugin, enabled);
        if (canvas.Menu) walpurgis.Apply(canvas, plugin, enabled && plugin.WalpurgisFraming.Value);
        if (canvas.Menu) CultivationBackground(canvas, plugin, enabled);
        Events(canvas, plugin, enabled && (!canvas.Battle || plugin.Battle.Value));
        FloorLabel(canvas, enabled);
        OutcomeStrips(canvas, plugin, enabled && plugin.Battle.Value);
        if (canvas.EgoStage) EgoStage(canvas, enabled);
        if (canvas.Cinematic) CinematicInsets(canvas, plugin, enabled && plugin.Battle.Value && plugin.CinematicViewport.Value);
        var panel = canvas.Inspection;
        if (panel != IntPtr.Zero && U.Alive(panel) && U.Active(panel))
            Inspection(panel, canvas, plugin, enabled && plugin.Battle.Value && plugin.Panels.Value);
        else if (canvas.Battle && U.Name(canvas.Object) == "[Canvas]BattleFrontUI") frameWidth = canvasWidth = 0;
        var results = canvas.Results;
        if (results != IntPtr.Zero && U.Alive(results) && U.Active(results))
            Result(results, canvas, plugin, enabled && plugin.Results.Value);
        foreach (var entry in changed.Where(e => !U.Alive(e.Value.Object)).ToArray())
        { entry.Value.Dispose(); changed.Remove(entry.Key); }
        if (!enabled)
            foreach (var rect in changed.Values) rect.Restore();
    }
    public void ClearWalpurgis() => walpurgis.Dispose();

    SavedRect Save(IntPtr obj)
    {
        int id = U.Id(obj);
        if (!changed.TryGetValue(id, out var saved)) { saved = new SavedRect(obj); changed[id] = saved; }
        return saved;
    }
    void Extraction(CanvasState canvas, Plugin plugin, bool enabled)
    {
        SavedRect? saved = null;
        if (extractionRoots.TryGetValue(canvas.Id, out int id))
        {
            if (!changed.TryGetValue(id, out saved) || !U.Alive(saved.Object))
            {
                extractionRoots.Remove(canvas.Id);
                saved = null;
            }
        }
        if (saved == null && plugin.Discover)
        {
            var root = U.Find(canvas.Root, "GachaResultUI(Clone)", 4);
            if (root != IntPtr.Zero)
            {
                saved = Save(root);
                extractionRoots[canvas.Id] = U.Id(root);
            }
        }
        if (saved == null || !U.Active(saved.Object)) return;
        // The reveal applies its own artwork framing inside this root.
        if (enabled && plugin.ExtractionViewport.Value) saved.HorizontalViewport(canvas.Root);
        else saved.Restore();
    }
    void FormationSelectorBacking(CanvasState canvas, bool enabled)
    {
        var panel = U.Find(canvas.Root, "[UIPanel]FormationSwitchablePersonalityUIPanel", 4);
        if (panel == IntPtr.Zero || !U.Active(panel)) return;
        var roster = U.Find(panel, "rect_bothGroup", 1);
        var frame = U.Find(panel, "[Image]Deco", 1);
        if (roster == IntPtr.Zero || frame == IntPtr.Zero) return;
        var saved = Save(frame);
        if (enabled)
        {
            var min = U.Get<V2>(roster, "anchorMin");
            var max = U.Get<V2>(roster, "anchorMax");
            if (Math.Abs(min.X - max.X) < .00001f)
                saved.ReanchorX(min.X, canvas.Original.Reference.X);
        }
        else saved.Restore();
    }
    void Credits(CanvasState canvas, bool enabled)
    {
        var stage = U.Find(canvas.Root, "[Rect]CreditsAnimation", 1);
        if (stage == IntPtr.Zero) return;
        var opaque = U.Find(stage, "Ultrawide credits backing", 1);
        if (opaque == IntPtr.Zero && enabled)
        {
            var go = U.NewGO("Ultrawide credits backing", stage);
            var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
            U.Set(image, "color", new Color(0, 0, 0, 1));
            U.Set(image, "raycastTarget", (byte)0);
            opaque = U.Transform(image);
            U.Call(opaque, "SetAsFirstSibling");
        }
        if (opaque != IntPtr.Zero)
        {
            U.ActiveSelf(U.GO(opaque), enabled);
            if (enabled) Save(opaque).Viewport(canvas.Root);
        }
        var stageRect = Save(stage);
        if (enabled)
        {
            stageRect.Anchors(.5f);
            stageRect.Width(canvas.Original.Reference.X);
            stageRect.CenterX(canvas.Root, 0);
        }
        else stageRect.Restore();
        var making = U.Find(stage, "[Rect]Credit_Making", 1);
        if (making != IntPtr.Zero)
        {
            var scroll = Save(making);
            if (enabled) scroll.OffsetX(ExtraWidth() / 2);
            else scroll.Restore();
        }
        var content = U.Find(stage, "[Rect]CreditsContent", 1);
        if (content != IntPtr.Zero)
            foreach (var sequence in U.Children(content).ToArray())
            {
                var fade = U.Find(sequence, "[Image]BlackImage", 1);
                if (fade != IntPtr.Zero)
                {
                    if (enabled) Save(fade).Viewport(canvas.Root);
                    else Save(fade).Restore();
                }
                var backing = U.Find(sequence, "[Image]Background", 1);
                if (backing != IntPtr.Zero)
                {
                    if (enabled) Save(backing).OffsetX(ExtraWidth() / 2);
                    else Save(backing).Restore();
                }
            }
        float ExtraWidth() => Math.Max(0, U.Get<R4>(canvas.Root, "rect").Width - canvas.Original.Reference.X);
        var black = U.Find(canvas.Root, "[Image]BlackScreend", 1);
        if (black != IntPtr.Zero)
        {
            if (enabled) Save(black).Viewport(canvas.Root);
            else Save(black).Restore();
        }
    }
    void ViewportTransitions(CanvasState canvas, bool enabled)
    {
        foreach (var child in U.Children(canvas.Root))
        {
            bool formation = IsFormationEntryFade(canvas, child);
            bool sanguine = IsSanguineBackdrop(canvas, child);
            if (!formation && !sanguine) continue;
            if (enabled) Save(child).Viewport(canvas.Root); else Save(child).Restore();
            if (enabled && tracedTransitions.Add(U.Id(child)))
                Plugin.Instance.LogMessage(formation
                    ? "Viewport formation entry fade: TempFormation/Canvas/Image; native opacity and input retained."
                    : "Viewport Sanguine Desire blood backdrop: native 20906_BloodMat and animation retained.");
        }
        foreach(var pair in transitionBackings.Where(x=>!U.Alive(x.Value.Source)).ToArray())
        { pair.Value.Dispose();transitionBackings.Remove(pair.Key); }
        if (U.Name(canvas.Object) == "[Canvas] GlobalTopCanvas")
        {
            var fade = U.Find(canvas.Root, "GlobalFadeEffect", 1);
            if (fade != IntPtr.Zero)
            {
                var controller=U.Component(U.GO(fade),U.Class("GlobalFadeEffect","","Assembly-CSharp.dll"));
                var image=controller==IntPtr.Zero?IntPtr.Zero:U.Read<IntPtr>(controller,"_image");
                if(image!=IntPtr.Zero)
                {
                    int id=U.Id(image);
                    if(!transitionBackings.TryGetValue(id,out var backing)&&enabled)
                    {
                        transitionBackings[id]=backing=new ScreenGraphic(image,solidOnly:true);
                        Plugin.Instance.LogMessage("Viewport global fade: native alpha with solid full-window rendering.");
                    }
                    backing?.Apply(canvas.Root,enabled);
                }
            }
        }
    }
    static bool IsFormationEntryFade(CanvasState canvas, IntPtr child)
    {
        if (U.Name(canvas.Object) != "Canvas" || U.Name(U.Parent(canvas.Root)) != "TempFormation"
            || U.Name(child) != "Image") return false;
        var go = U.GO(child);
        return U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll")) != IntPtr.Zero
            && U.Component(go, U.Class("CanvasGroup", "UnityEngine", "UnityEngine.UIModule.dll")) != IntPtr.Zero
            && U.Component(go, U.Class("Button", "UnityEngine.UI", "UnityEngine.UI.dll")) != IntPtr.Zero;
    }
    static bool IsSanguineBackdrop(CanvasState canvas, IntPtr child)
    {
        if (U.Name(canvas.Object) != "[Canvas]Canvas" || U.Name(child) != "RawImage") return false;
        var etc = U.Parent(canvas.Root);
        if (U.Name(etc) != "[Transform]Etc") return false;
        string skill = U.Name(U.Parent(etc)).Replace("(Clone)", "");
        if (skill is not ("SkillViewEGO_2090611" or "SkillViewEGO_2090621")) return false;
        var raw = U.Component(U.GO(child), U.Class("RawImage", "UnityEngine.UI", "UnityEngine.UI.dll"));
        return raw != IntPtr.Zero && U.Name(U.Call(raw, "get_material")).StartsWith("20906_BloodMat", StringComparison.Ordinal);
    }
    void LoadingArtPopup(CanvasState canvas, Plugin plugin, bool enabled)
    {
        var popup = U.Find(canvas.Root, "Prefab_UISettingsPopup(Clone)", 3);
        if (popup == IntPtr.Zero || !U.Active(popup)) return;
        bool expand = enabled && plugin.MenuSafeArea.Value;
        var saved = Save(popup);
        if (!expand) { saved.Restore(); var oldBacking=U.Find(popup, "Ultrawide BGM backing", 3); if(oldBacking!=IntPtr.Zero) U.ActiveSelf(U.GO(oldBacking),false); return; }
        saved.Viewport(canvas.Root);
        float extra = Math.Max(0, U.Get<R4>(popup, "rect").Width - 3000);
        var art = U.Find(popup, "Prefab_LoadingCGSelectUI", 1);
        if (art != IntPtr.Zero && U.Active(art))
        {
            var backing = U.Find(art, "Ultrawide customization background", 1);
            if (backing == IntPtr.Zero)
            {
                var go = U.NewGO("Ultrawide customization background", art);
                var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
                U.Set(image, "color", new Color(0, 0, 0, 1));
                U.Set(image, "raycastTarget", (byte)0);
                backing = U.Transform(image);
                U.Call(backing, "SetAsFirstSibling");
            }
            Save(backing).Viewport(canvas.Root);
            var scroll = U.Find(art, "ScrollView", 3);
            var viewport = scroll == IntPtr.Zero ? IntPtr.Zero : U.Find(scroll, "Viewport", 1);
            if (viewport != IntPtr.Zero)
            {
                float cellWidth = Math.Max(345, (U.Get<R4>(viewport, "rect").Width - 16) / 7);
                var content = U.Find(viewport, "Content", 1);
                if (content != IntPtr.Zero)
                    foreach (var row in U.Children(content).ToArray())
                    {
                        if (!U.Name(row).StartsWith("Prefab_LoadingCGSelectRowUI")) continue;
                        Save(row).Width(cellWidth * 7);
                        for (int i = 0; i < 7; i++)
                        {
                            var cell = U.Find(row, "Cell_" + i, 2);
                            if (cell == IntPtr.Zero) continue;
                            Save(cell).Width(cellWidth);
                            Save(cell).PositionX(cellWidth * i);
                            var image = U.Component(U.GO(cell), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
                            if (image != IntPtr.Zero) U.Set(image, "preserveAspect", (byte)1);
                        }
                    }
            }
            Place(art, "tmp_random", 1, -395, 250);
            Place(art, "btn_random", 1, -125, 238);
            Place(art, "button_setDefault", 1, -600, 100);
        }
        var bgm = U.Find(popup, "Prefab_LobbyBGMSelectUI", 1);
        if (bgm != IntPtr.Zero && U.Active(bgm))
        {
            Shift(bgm, "Prefab_BGMListUI", 0);
            Shift(bgm, "script_BGMPlaylistToggleUI", 0);
            Shift(bgm, "Prefab_BGMCassetteTapeUI", 0);
            Shift(bgm, "script_lobbyBGMSelectUIPersonality", 0);
            var background = U.Find(bgm, "Image", 1);
            if (background != IntPtr.Zero)
            {
                var backing=U.Find(bgm,"Ultrawide BGM backing",1);
                if(backing==IntPtr.Zero)
                {
                    var go=U.NewGO("Ultrawide BGM backing",bgm);
                    var image=U.Component(go,U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"),true);
                    U.Set(image,"color",new Color(0,0,0,1));U.Set(image,"raycastTarget",(byte)0);
                    backing=U.Transform(image);U.Call(backing,"SetAsFirstSibling");
                }
                U.ActiveSelf(U.GO(backing),true);Save(backing).Viewport(canvas.Root);
                var imageComponent=U.Component(U.GO(background),U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"));
                var sprite=imageComponent==IntPtr.Zero?IntPtr.Zero:U.Call(imageComponent,"get_overrideSprite");
                if(sprite!=IntPtr.Zero)
                {
                    var pixels=U.Get<R4>(sprite,"rect");var screen=U.Get<R4>(canvas.Root,"rect");
                    if(pixels.Height>0&&pixels.Width>0)
                    {
                        float height=Math.Max(screen.Height,screen.Width*pixels.Height/pixels.Width);
                        var rect=Save(background);rect.Viewport(canvas.Root);
                        rect.Size(new V2(height*pixels.Width/pixels.Height,height));
                    }
                }
                else Save(background).Viewport(canvas.Root);
            }
        }
        void Shift(IntPtr root, string name, float offset)
        {
            var rect = U.Find(root, name, 2);
            if (rect != IntPtr.Zero) Save(rect).OffsetX(offset);
        }
        void Place(IntPtr root, string name, float anchor, float x, float width)
        {
            var rect = U.Find(root, name, 5);
            if (rect == IntPtr.Zero) return;
            var state = Save(rect); state.Anchors(anchor); state.Width(width); state.PositionX(x);
        }
    }

    void FloorLabel(CanvasState canvas, bool enabled)
    {
        var container = U.Find(canvas.Root, "[Rect]CurrentFloor", 1);
        if (container == IntPtr.Zero) return;
        var label = U.Find(container, "[Image]CurrentFloor", 1);
        if (label == IntPtr.Zero) return;
        var saved = Save(label);
        if (enabled) saved.LeftEdge(canvas.Root, 100); else saved.Restore();
    }
    void OutcomeStrips(CanvasState canvas, Plugin plugin, bool enabled)
    {
        if (!outcomeStrips.TryGetValue(canvas.Id, out var strips)) outcomeStrips[canvas.Id] = strips = new();
        if (plugin.Discover) Find(canvas.Root, 6);
        foreach (int id in strips.ToArray())
        {
            if (!changed.TryGetValue(id, out var saved) || !U.Alive(saved.Object)) { strips.Remove(id); continue; }
            var image = U.Component(U.GO(saved.Object), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
            if (image != IntPtr.Zero)
            {
                if (!outcomePreserveAspect.TryGetValue(id, out byte original))
                    outcomePreserveAspect[id] = original = U.Get<byte>(image, "preserveAspect");
                byte desired = enabled ? (byte)0 : original;
                if (U.Get<byte>(image, "preserveAspect") != desired) U.Set(image, "preserveAspect", desired);
            }
            if (enabled) { saved.Anchors(.5f); saved.WidthToViewport(canvas.Root); } else saved.Restore();
        }
        void Find(IntPtr root, int depth)
        {
            if (depth < 0 || !U.Alive(root)) return;
            var controller = U.Component(U.GO(root), U.Class("ActBattleEndTypoUI", "BattleUI.Typo", "Assembly-CSharp.dll"));
            if (controller != IntPtr.Zero)
            {
                var background = U.Read<IntPtr>(controller, "_bg");
                if (background != IntPtr.Zero && U.Alive(background))
                {
                    var rect = U.Component(background, U.Class("RectTransform"));
                    if (rect != IntPtr.Zero)
                    {
                        var saved = Save(rect);
                        if (strips.Add(U.Id(rect))) plugin.LogMessage("Viewport battle-outcome strip: " + U.Name(rect));
                    }
                }
                return;
            }
            foreach (var child in U.Children(root).ToArray()) Find(child, depth - 1);
        }
    }
    void CultivationBackground(CanvasState canvas, Plugin plugin, bool enabled)
    {
        var panel = U.Find(canvas.Root, "Prefab_ProjectGSMainUI", 2);
        if (panel == IntPtr.Zero || !U.Active(panel)) return;
        var background = U.Find(panel, "img_background", 1);
        if (background == IntPtr.Zero) return;
        var image = U.Component(U.GO(background), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (image == IntPtr.Zero) return;
        var sprite = U.Call(image, "get_overrideSprite");
        if (!U.Alive(sprite)) sprite = U.Call(image, "get_sprite");
        if (!U.Alive(sprite)) return; // The room sprite loads asynchronously.
        var saved = Save(background);
        int id = U.Id(background);
        foreach (var old in cultivationFades.Where(e => !U.Alive(e.Value.Root)).ToArray())
        { old.Value.Dispose(); cultivationFades.Remove(old.Key); }
        if (!cultivationFades.TryGetValue(id, out var fade)) cultivationFades[id] = fade = new CultivationFade(panel, background);
        bool show = enabled && plugin.MenuBackgrounds.Value;
        if (!show) { saved.Restore(); fade.Apply(canvas.Root, false); return; }
        var art = U.Get<R4>(sprite, "rect");
        if (art.Width <= 0 || art.Height <= 0) return;
        saved.Viewport(canvas.Root);
        saved.Size(new V2(art.Width, art.Height));
        saved.CoverViewport(canvas.Root, 16f / 9f);
        fade.Apply(canvas.Root, true);
        if (plugin.Discover)
            plugin.LogMessage($"Cultivation room: rect {U.Get<R4>(background, "rect")}; scale {U.Get<V3>(background, "localScale")}; viewport {U.Get<R4>(canvas.Root, "rect")}");
    }
    void EgoStage(CanvasState canvas, bool enabled)
    {
        if (U.Name(canvas.Object) != "BattleCutSceneUI") return;
        var viewport = U.Get<R4>(canvas.Root, "rect");
        foreach (var root in U.Children(canvas.Root))
        {
            if (!U.Name(root).StartsWith("FX_Scene_EGOStartBroken", StringComparison.Ordinal)) continue;
            if (U.Component(U.GO(root), U.Class("RectTransform")) == IntPtr.Zero) continue;
            var saved = Save(root);
            if (enabled) saved.StageScale(viewport.Width / canvas.Original.Reference.X,
                viewport.Height / canvas.Original.Reference.Y);
            else saved.Restore();
        }
    }
    void CinematicInsets(CanvasState canvas, Plugin p, bool enabled)
    {
        var controller = U.Find(canvas.Root, "[Script]BattleEncounterDamageTypoController", 1);
        if (controller == IntPtr.Zero) return;
        var viewport = U.Get<R4>(canvas.Root, "rect");
        foreach (var corner in U.Children(controller))
        {
            var rect = U.Component(U.GO(corner), U.Class("RectTransform"));
            if (rect == IntPtr.Zero) continue;
            var min = U.Get<V2>(rect, "anchorMin"); var max = U.Get<V2>(rect, "anchorMax");
            if (min.X != max.X || (min.X != 0 && min.X != 1)) continue;
            var saved = Save(rect);
            if (enabled) saved.CornerInsets(canvas.Original.Reference.Y, viewport.Height); else saved.Restore();
        }
    }
    void Menu(CanvasState canvas, Plugin p, bool enabled)
    {
        var viewport = U.Get<R4>(canvas.Root, "rect");
        if (!backgroundsByCanvas.TryGetValue(canvas.Id, out var menuBackgrounds)) backgroundsByCanvas[canvas.Id] = menuBackgrounds = new();
        if (!overlaysByCanvas.TryGetValue(canvas.Id, out var menuOverlays)) overlaysByCanvas[canvas.Id] = menuOverlays = new();
        float width = LayoutMath.ContentWidth(viewport.Width, viewport.Height, p.MenuAspect.Value);
        bool cinematic = canvas.Cinematic && p.CinematicViewport.Value;
        bool centered = enabled && p.MenuSafeArea.Value && !cinematic && !p.ScreenEdgeCanvases.Value.Split('|').Contains(U.Name(canvas.Object));
        var stagePreview = U.Find(canvas.Root, "[Script]StageEnemyInformation", 2);
        bool previewOpen = enabled && p.MenuSafeArea.Value && !canvas.Battle
            && stagePreview != IntPtr.Zero && U.Active(stagePreview);
        bool sharedRoots = U.Children(canvas.Root).Any(c => U.Name(c) is "[Rect]PresenterRoot" or "[Rect]PanelRoot");
        foreach (var root in U.Children(canvas.Root))
        {
            if (IsFormationEntryFade(canvas, root) || IsSanguineBackdrop(canvas, root)) continue;
            if (sharedRoots && U.Name(root) is not ("[Rect]PresenterRoot" or "[Rect]PanelRoot")) continue;
            if (U.Component(U.GO(root), U.Class("RectTransform")) == IntPtr.Zero) continue;
            if (!sharedRoots && IsVisualRoot(root)) continue;
            var saved = Save(root);
            bool previewRoot = previewOpen && U.Name(root) == "[Script]UnitInformationController";
            if (centered || previewRoot)
            {
                saved.AcceptGameAnchors();
                var position = U.Get<V2>(root, "anchoredPosition");
                int id = U.Id(root);
                if (saved.AuthoredMax.X - saved.AuthoredMin.X > .9f && Math.Abs(position.X) > width * .5f)
                    slidingRoots.Add(id);
                float slide = p.MenuSlidePanels.Value && slidingRoots.Contains(id)
                    ? LayoutMath.SlideAnchorOffset(position.X, width, viewport.Width) : 0;
                saved.SafeRange(width / viewport.Width, slide);
            }
            else saved.Restore();
        }
        if (Plugin.Instance.Discover)
        {
            discoveringCanvas = canvas.Id;
            FindMenuBackgrounds(canvas.Root, width, viewport.Height, 12, menuBackgrounds, menuOverlays);
            FindBackdrops(canvas.Root, canvas.Id, p.BackdropPanels.Value.Split('|'), 8);
        }
        if (edgePanelsByCanvas.TryGetValue(canvas.Id, out var edgePanels))
            foreach (int id in edgePanels.ToArray())
            {
                if (!changed.TryGetValue(id, out var panel) || !U.Alive(panel.Object)) { edgePanels.Remove(id); continue; }
                if (enabled && p.EdgePanels.Value && p.EdgePanelRoots.Value.Split('|').Contains(U.Name(panel.Object))) panel.Viewport(canvas.Root);
                else panel.Restore();
            }
        if (edgeStripsByCanvas.TryGetValue(canvas.Id, out var edgeStrips))
            foreach (int id in edgeStrips.ToArray())
            {
                if (!changed.TryGetValue(id, out var strip) || !U.Alive(strip.Object)) { edgeStrips.Remove(id); continue; }
                if (enabled && p.MenuOverlays.Value) strip.HorizontalViewport(canvas.Root);
                else strip.Restore();
            }
        if (containerBackings.TryGetValue(canvas.Id, out var visualBackings))
            foreach (var entry in visualBackings.ToArray())
            {
                if (!U.Alive(entry.Value.Source)) { entry.Value.Dispose(); visualBackings.Remove(entry.Key); continue; }
                entry.Value.Apply(canvas.Root, enabled && p.MenuOverlays.Value);
            }
        if (backdropsByCanvas.TryGetValue(canvas.Id, out var backdrops))
            foreach (var entry in backdrops.ToArray())
            {
                var backing = entry.Value;
                if (!U.Alive(backing.Object)) { backing.Dispose(); backdrops.Remove(entry.Key); continue; }
                U.ActiveSelf(U.GO(backing.Object), enabled && p.OpaquePanelBackdrops.Value);
                if (enabled && p.OpaquePanelBackdrops.Value) backing.Viewport(canvas.Root);
            }
        foreach (int id in menuBackgrounds.ToArray())
        {
            if (!changed.TryGetValue(id, out var saved) || !U.Alive(saved.Object)) { menuBackgrounds.Remove(id); continue; }
            if (enabled && p.MenuBackgrounds.Value)
            {
                saved.Cover(viewport.Width, viewport.Height);
            }
            else saved.RestoreScale();
        }
        foreach (int id in menuOverlays.ToArray())
        {
            if (!changed.TryGetValue(id, out var saved) || !U.Alive(saved.Object)) { menuOverlays.Remove(id); continue; }
            if (enabled && p.MenuOverlays.Value) saved.Viewport(canvas.Root);
            else saved.Restore();
        }
        if (shopBackgroundsByCanvas.TryGetValue(canvas.Id, out var shopBackgrounds))
            foreach (int id in shopBackgrounds.ToArray())
            {
                if (!changed.TryGetValue(id, out var saved) || !U.Alive(saved.Object)) { shopBackgrounds.Remove(id); continue; }
                if (enabled && p.ShopBackground.Value && p.MenuBackgrounds.Value) saved.CoverViewport(canvas.Root);
                else saved.Restore();
            }
        artworkCurtains.Apply(canvas, p);
        if (p.Discover)
        {
            var giftRoot = U.Find(canvas.Root, "Prefab_SelectEgoGiftCategorySetUIPanel(Clone)", 4);
            if (giftRoot != IntPtr.Zero && !giftGrids.ContainsKey(U.Id(giftRoot)))
            {
                try { giftGrids[U.Id(giftRoot)] = new GiftGrid(giftRoot); }
                catch (Exception ex) { p.LogMessage("Gift-grid expansion unavailable: " + ex.Message, true); }
            }
        }
        foreach (var pair in giftGrids.ToArray())
        {
            if (!U.Alive(pair.Value.Root)) { pair.Value.Dispose(); giftGrids.Remove(pair.Key); continue; }
            if (pair.Value.CanvasId == canvas.Id || pair.Value.CanvasId == 0)
            {
                try { pair.Value.Apply(canvas, p, enabled && p.WideGiftGrid.Value); }
                catch (Exception ex) { p.LogMessage("Gift-grid expansion disabled: " + ex.Message, true); p.WideGiftGrid.Value = false; }
            }
        }
    }
    int discoveringCanvas;
    void FindBackdrops(IntPtr root, int canvasId, string[] names, int depth)
    {
        if (depth < 0 || !U.Alive(root) || !U.Active(root)) return;
        if (Plugin.Instance.EdgePanelRoots.Value.Split('|').Contains(U.Name(root)))
        {
            if (!edgePanelsByCanvas.TryGetValue(canvasId, out var panels)) edgePanelsByCanvas[canvasId] = panels = new();
            panels.Add(U.Id(root)); Save(root);
        }
        bool shop = Plugin.Instance.ShopBackground.Value && U.Name(root).StartsWith("MirrorDungeonShopUI_", StringComparison.Ordinal);
        if (shop)
        {
            var products = U.Find(root, "[Script]ProductListScrollView", 2);
            var frame = products == IntPtr.Zero ? IntPtr.Zero : U.Find(products, "Image", 1);
            var room = frame == IntPtr.Zero ? IntPtr.Zero : U.Find(frame, "Image (6)", 1);
            if (room != IntPtr.Zero && !ContainsSelectable(room, 6))
            {
                if (!shopBackgroundsByCanvas.TryGetValue(canvasId, out var rooms)) shopBackgroundsByCanvas[canvasId] = rooms = new();
                if (rooms.Add(U.Id(room))) Plugin.Instance.LogMessage("Viewport shop room artwork: " + U.Name(root));
                Save(room);
            }
        }
        if (names.Contains(U.Name(root)) || shop)
        {
            if (!backdropsByCanvas.TryGetValue(canvasId, out var backing)) backdropsByCanvas[canvasId] = backing = new();
            int id = U.Id(root);
            if (!backing.ContainsKey(id))
            {
                var go = U.NewGO("Ultrawide opaque backdrop", root);
                var image = U.Component(go, U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"), true);
                U.Set(image, "color", new Color(0, 0, 0, 1));
                U.Set(image, "raycastTarget", (byte)0);
                var rect = U.Transform(image);
                U.Call(rect, "SetAsFirstSibling");
                backing[id] = new SavedRect(rect);
                Plugin.Instance.LogMessage($"Opaque backing behind panel artwork: {U.Name(root)}");
            }
        }
        foreach (var child in U.Children(root).ToArray()) FindBackdrops(child, canvasId, names, depth - 1);
    }
    static bool IsVisualRoot(IntPtr root)
    {
        string name = U.Name(root);
        bool visualName = name.Contains("BG", StringComparison.OrdinalIgnoreCase) || name.Contains("Background", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Vignette", StringComparison.OrdinalIgnoreCase) || name.Contains("Fade", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cover", StringComparison.OrdinalIgnoreCase) || name.Contains("Block", StringComparison.OrdinalIgnoreCase);
        return visualName && !ContainsSelectable(root, 6);
    }
    void FindMenuBackgrounds(IntPtr root, float width, float height, int depth, HashSet<int> menuBackgrounds, HashSet<int> menuOverlays)
    {
        if (depth < 0 || !U.Alive(root) || !U.Active(root)) return;
        string name = U.Name(root);
        if (Plugin.Instance.WalpurgisFraming.Value && LayoutMath.IsWalpurgisPanel(name)) return;
        var scrollParent = U.Parent(root);
        if (scrollParent != IntPtr.Zero)
        {
            var scroll = U.Component(U.GO(scrollParent), U.Class("ScrollRect", "UnityEngine.UI", "UnityEngine.UI.dll"));
            if (scroll != IntPtr.Zero && U.Call(scroll, "get_viewport") == root) return;
        }
        if (name == "img_background" && U.Name(U.Parent(root)) == "Prefab_ProjectGSMainUI") return;
        if (name.StartsWith("Ultrawide ", StringComparison.Ordinal)) return;
        if (name is "BattleResultPanel_renewal(Clone)" or "[Script]UnitInformationController_Renewal") return;
        if (name is "Prefab_BossRaidUIPanel" or "Prefab_BossRaidUIPanel(Clone)")
        {
            var glass = U.Find(root, "[Image]mirror_piece (12)", 1);
            if (glass != IntPtr.Zero && !ContainsSelectable(glass, 4))
            {
                int id = U.Id(glass);
                if (menuBackgrounds.Add(id)) Plugin.Instance.LogMessage("Viewport Reflectrial shattered glass.");
                Save(glass);
            }
            foreach (string stripName in new[] { "[Image]UpperCaution", "[Image]BelowCaution" })
            {
                var strip = U.Find(root, stripName, 3);
                if (strip == IntPtr.Zero || ContainsSelectable(strip, 4)) continue;
                if (!edgeStripsByCanvas.TryGetValue(discoveringCanvas, out var strips)) edgeStripsByCanvas[discoveringCanvas] = strips = new();
                if (strips.Add(U.Id(strip))) Plugin.Instance.LogMessage("Viewport Reflectrial caution: " + stripName);
                Save(strip);
            }
        }
        if (name is "rect_caution_up" or "rect_caution_down" && !ContainsSelectable(root, 6))
        {
            if (!edgeStripsByCanvas.TryGetValue(discoveringCanvas, out var strips)) edgeStripsByCanvas[discoveringCanvas] = strips = new();
            if (strips.Add(U.Id(root))) Plugin.Instance.LogMessage("Viewport caution ribbon: " + name);
            Save(root);
        }
        bool overlay = name.Contains("Vignette", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Fade", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cover", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Block", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Dim", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BlackPanel", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BackPanel", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BlackScreen", StringComparison.OrdinalIgnoreCase);
        overlay |= name.Contains("Outline", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ScreenGlow", StringComparison.OrdinalIgnoreCase)
            || (name == "image_red" && U.Name(U.Parent(root)) == "MirrorDungeon_SelectTheme_Panel");
        bool background = name.Contains("BG", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Background", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Back", StringComparison.OrdinalIgnoreCase);
        bool semanticOverlay = overlay || name.Contains("NetworkingUI", StringComparison.Ordinal);
        var graphic = U.Component(U.GO(root), U.Class("Graphic", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (graphic != IntPtr.Zero && Plugin.Instance.TraceLargeGraphics.Value && !tracedGraphics.Contains(U.Id(root)))
        {
            var rect = U.Component(U.GO(root), U.Class("RectTransform"));
            if (rect != IntPtr.Zero)
            {
                var bounds = U.Get<R4>(rect, "rect");
                if (bounds.Width >= width * .5f && bounds.Height >= height * .5f)
                {
                    tracedGraphics.Add(U.Id(root));
                    var image = U.Component(U.GO(root), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
                    var sprite = image == IntPtr.Zero ? IntPtr.Zero : U.Call(image, "get_sprite");
                    string path = name;
                    for (var parent = U.Parent(root); parent != IntPtr.Zero; parent = U.Parent(parent)) path = U.Name(parent) + "/" + path;
                    Plugin.Instance.LogMessage($"Large graphic: {path}; rect {bounds}; color {U.Get<Color>(graphic, "color")}; sprite {(sprite == IntPtr.Zero ? "none" : U.Name(sprite))}; controls {ContainsSelectable(root, 6)}");
                }
            }
        }
        bool solid = false;
        if (graphic != IntPtr.Zero)
        {
            var image = U.Component(U.GO(root), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
            if (image != IntPtr.Zero)
            {
                var sprite = U.Call(image, "get_sprite");
                solid = sprite == IntPtr.Zero || U.Name(sprite).Equals("white", StringComparison.OrdinalIgnoreCase);
                if (solid) overlay = true;
            }
        }
        if (background || overlay)
        {
            var rect = U.Component(U.GO(root), U.Class("RectTransform"));
            if (rect != IntPtr.Zero)
            {
                var bounds = U.Get<R4>(rect, "rect");
                if (bounds.Width >= width * .8f && bounds.Height >= height * .7f)
                {
                    bool controls = ContainsSelectable(root, 6);
                    if (solid && controls && semanticOverlay && graphic != IntPtr.Zero && U.Get<byte>(graphic, "enabled") != 0)
                    {
                        if (!containerBackings.TryGetValue(discoveringCanvas, out var backings)) containerBackings[discoveringCanvas] = backings = new();
                        int id = U.Id(root);
                        if (!backings.ContainsKey(id))
                        {
                            backings[id] = new ScreenGraphic(graphic);
                            Plugin.Instance.LogMessage("Separated viewport backing from controls: " + name);
                        }
                    }
                    else if (graphic != IntPtr.Zero && !controls)
                    {
                        int id = U.Id(root);
                        bool panelBackground = background && !overlay && Plugin.Instance.PreservePanelBackgrounds.Value && IsPanelBackground(root, width, height);
                        if (panelBackground || (overlay && Plugin.Instance.PreservePanelShades.Value && IsPanelShade(root, width)))
                        {
                            if (panelShades.Add(id)) Plugin.Instance.LogMessage($"Panel-attached shade: {name}; parent {U.Name(U.Parent(root))}");
                            if (changed.TryGetValue(id, out var saved)) saved.Restore();
                            menuOverlays.Remove(id);
                            menuBackgrounds.Remove(id);
                            foreach (var child in U.Children(root)) FindMenuBackgrounds(child, width, height, depth - 1, menuBackgrounds, menuOverlays);
                            return;
                        }
                        if (!changed.ContainsKey(id)) Plugin.Instance.LogMessage($"Viewport {(overlay ? "overlay" : "background")}: {name}");
                        Save(root);
                        if (overlay) menuOverlays.Add(id); else menuBackgrounds.Add(id);
                    }
                }
            }
        }
        foreach (var child in U.Children(root)) FindMenuBackgrounds(child, width, height, depth - 1, menuBackgrounds, menuOverlays);
    }
    static bool IsPanelShade(IntPtr root, float designWidth)
    {
        string name = U.Name(root);
        if (!name.Contains("Cover", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("Panel", StringComparison.OrdinalIgnoreCase)) return false;
        var parent = U.Parent(root);
        if (parent == IntPtr.Zero || U.Component(U.GO(parent), U.Class("RectTransform")) == IntPtr.Zero) return false;
        var min = U.Get<V2>(parent, "anchorMin");
        var max = U.Get<V2>(parent, "anchorMax");
        var rect = U.Get<R4>(parent, "rect");
        return LayoutMath.IsSidePanel(min.X, max.X, rect.Width, designWidth);
    }
    static bool IsPanelBackground(IntPtr root, float designWidth, float viewportHeight)
    {
        var parent = U.Parent(root);
        if (!U.Alive(parent) || U.Component(U.GO(parent), U.Class("RectTransform")) == IntPtr.Zero) return false;
        var min = U.Get<V2>(parent, "anchorMin"); var max = U.Get<V2>(parent, "anchorMax");
        var bounds = U.Get<R4>(parent, "rect");
        return min == max && bounds.Width > 0 && bounds.Height > 0
            && bounds.Width <= designWidth * 1.05f && bounds.Height < viewportHeight * .95f;
    }
    static bool ContainsSelectable(IntPtr root, int depth)
    {
        var selectable = U.Component(U.GO(root), U.Class("Selectable", "UnityEngine.UI", "UnityEngine.UI.dll"));
        if (selectable != IntPtr.Zero) return true;
        if (depth <= 0) return U.Get<int>(root, "childCount") > 0;
        return U.Children(root).Any(child => ContainsSelectable(child, depth - 1));
    }
    void Events(CanvasState canvas, Plugin p, bool enabled)
    {
        if (p.Discover)
        {
            var eventRoot = U.Find(canvas.Root, "[Script]ChoiceEventController", 3);
            if (eventRoot == IntPtr.Zero)
                eventRoot = U.Find(canvas.Root, "Prefab_ChoiceEventController(Clone)", 3);
            if (eventRoot != IntPtr.Zero && !eventChoices.ContainsKey(U.Id(eventRoot)))
            {
                try
                {
                    eventChoices[U.Id(eventRoot)] = new EventChoices(eventRoot, canvas.Id);
                    p.LogMessage("Shared event choice layout registered: " + U.Name(eventRoot));
                }
                catch (Exception ex) { p.LogMessage("Event choice layout unavailable: " + ex.Message, true); }
            }
        }
        foreach (var pair in eventChoices.ToArray())
        {
            if (!U.Alive(pair.Value.Root)) { pair.Value.Dispose(); eventChoices.Remove(pair.Key); continue; }
            if (pair.Value.CanvasId != canvas.Id) continue;
            try { pair.Value.Apply(canvas, enabled && p.EventLayout.Value); }
            catch (Exception ex) { p.LogMessage("Event choice layout disabled: " + ex.Message, true); p.EventLayout.Value = false; }
        }
    }
    void Inspection(IntPtr panel, CanvasState canvas, Plugin p, bool enabled)
    {
        var partLayer = U.Find(panel, "[Script]AbnormalityStatusManager", 1);
        if (partLayer != IntPtr.Zero)
        {
            var parts = Save(partLayer);
            if (enabled) parts.Viewport(canvas.Root); else parts.Restore();
        }
        var spine = U.Find(panel, "[Canvas]AboveSpine", 1);
        if (spine == IntPtr.Zero) return;
        var frame = U.Find(spine, "[Image]RightSidePanel", 1);
        if (frame == IntPtr.Zero) return;
        var anchor = U.Get<V2>(frame, "anchorMin");
        if (anchor.X != 0 && anchor.X != 1) return;
        bool right = anchor.X == 1;
        float width = U.Get<R4>(canvas.Root, "rect").Width;
        float design = canvas.Original.Reference.X;
        float fw = U.Get<R4>(frame, "rect").Width;
        if (width <= fw || fw <= 0) return;
        frameWidth = fw; canvasWidth = width;
        var buffRoot = U.Find(spine, "[Script]BuffManager", 5);
        if (buffRoot != IntPtr.Zero)
        {
            int buffId = U.Id(buffRoot);
            if (!inspectionBuffs.TryGetValue(buffId, out var buffs))
                inspectionBuffs[buffId] = buffs = new InspectionBuffs(buffRoot);
            buffs.Apply(enabled && width / canvas.Original.Reference.X > 1.15f);
        }
        foreach (var old in inspectionBuffs.Where(e => !U.Alive(e.Value.Root)).ToArray())
        { old.Value.Dispose(); inspectionBuffs.Remove(old.Key); }
        foreach (var target in U.Children(spine))
        {
            string name = U.Name(target);
            if (name is "[Trigger]SkillSummaryPanel" or "[Trigger]EgoSummaryPanelBack" or "[Rect]MoveButtons")
            {
                foreach (var child in U.Children(target)) Special(child, frame, width, fw, right, p, enabled);
                continue;
            }
            if (name == "[Image]RightSidePanel") continue;
            var currentMin = U.Get<V2>(target, "anchorMin");
            var currentMax = U.Get<V2>(target, "anchorMax");
            if (currentMin.X != currentMax.X) continue;
            var saved = Save(target);
            if (!enabled) { saved.Restore(); continue; }
            saved.AcceptGameAnchors();
            var pos = U.Get<V2>(target, "anchoredPosition");
            var size = U.Get<R4>(target, "rect");
            var pivot = U.Get<V2>(target, "pivot");
            bool toRight = name == "[Script]SideButtonList" ? right
                : name == "[Image]UnitStatusPanel" ? !right
                : saved.AuthoredMin.X * design + pos.X + (.5f - pivot.X) * size.Width >= design / 2;
            float offset = name is "Prefab_UnitInformationTabManager" or "Prefab_UnitInfoUnitStatusContent" ? p.PanelOffset.Value : name == "[Image]UnitStatusPanel" ? p.ShadeOffset.Value : 0;
            float x = (saved.AuthoredMin.X * design + (toRight ? width - design : 0) + offset) / width;
            saved.Anchors(x);
        }
    }
    void Special(IntPtr target, IntPtr frame, float width, float fw, bool right, Plugin p, bool enabled)
    {
        string name = U.Name(target);
        bool popup = name is "Prefab_UnitInformationSkillSummaryPanel" or "Prefab_UnitInformationEgoSummaryPanel";
        bool arrow = name is "[Button]PrevButton" or "[Button]NextButton";
        if (!popup && !arrow) return;
        var saved = Save(target);
        if (!enabled || (popup ? !p.Popups.Value : !p.Navigation.Value)) { saved.Restore(); return; }
        saved.AcceptGameAnchors();
        if (arrow)
        {
            bool prev = name == "[Button]PrevButton";
            float x = prev ? p.EdgeInset.Value : width - p.EdgeInset.Value;
            saved.Anchors((x - U.Get<V2>(target, "anchoredPosition").X) / width);
        }
        else
        {
            var framePos = U.Get<V2>(frame, "anchoredPosition");
            var framePivot = U.Get<V2>(frame, "pivot");
            float scaleX = U.Get<V3>(frame, "localScale").X;
            float a = -fw * framePivot.X * scaleX, b = fw * (1 - framePivot.X) * scaleX;
            float pw = U.Get<R4>(target, "rect").Width, pivot = U.Get<V2>(target, "pivot").X;
            float x = right ? framePos.X + Math.Min(a, b) - p.PopupGap.Value - pw * (1 - pivot) : framePos.X + Math.Max(a, b) + p.PopupGap.Value + pw * pivot;
            saved.Anchors(right ? 1 : 0);
            saved.PositionX(x);
        }
    }
    void Result(IntPtr root, CanvasState canvas, Plugin p, bool enabled)
    {
        var saved = Save(root);
        var block = U.Find(root, "[Image]Block", 1);
        var shade = block == IntPtr.Zero ? null : Save(block);
        if (!enabled) { saved.Restore(); shade?.Restore(); return; }
        var rect = U.Get<R4>(canvas.Root, "rect");
        float design = p.ResultsWidth.Value == 0 ? canvas.Original.Reference.X : p.ResultsWidth.Value;
        saved.Anchors(.5f + p.ResultsOffset.Value / rect.Width);
        saved.Width(Math.Min(rect.Width, design));
        if (shade == null) return;
        if (!p.Background.Value) { shade.Restore(); return; }
        shade.Anchors(.5f);
        shade.Size(new(rect.Width + 2 * Math.Abs(p.ResultsOffset.Value), Math.Max(3000, rect.Height)));
        var image = U.Component(U.GO(block), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
        shade.Background(image);
    }
    public void Camera(IntPtr obj, Plugin p)
    {
        int id = U.Id(obj);
        bool focus = U.Read<byte>(obj, "_isFocusingInSelect") != 0 && U.Read<byte>(obj, "_isBattlePhase") == 0 && U.Read<byte>(obj, "_isProduce") == 0;
        if (p.ActiveLayout && p.Camera.Value && focus && canvasWidth > frameWidth && frameWidth > 0)
        {
            if (!cameras.ContainsKey(id)) cameras[id] = (U.Read<float>(obj, "_leftHalfCenterViewportX"), U.Read<float>(obj, "_rightHalfCenterViewportX"));
            float fraction = Math.Clamp(frameWidth / canvasWidth, 0, .8f);
            U.Write(obj, "_leftHalfCenterViewportX", Math.Clamp((1 - fraction) * .5f + p.CameraOffset.Value, .05f, .95f));
            U.Write(obj, "_rightHalfCenterViewportX", Math.Clamp((1 + fraction) * .5f + p.CameraOffset.Value, .05f, .95f));
        }
        else if (cameras.Remove(id, out var original))
        { U.Write(obj, "_leftHalfCenterViewportX", original.Left); U.Write(obj, "_rightHalfCenterViewportX", original.Right); }
    }
}

internal sealed class SavedRect : IDisposable
{
    readonly nint handle;
    public IntPtr Object => IL2CPP.il2cpp_gchandle_get_target(handle);
    public V2 AuthoredMin, AuthoredMax;
    V2 lastMin, lastMax, originalSize, originalPosition, lastPosition;
    readonly V3 originalScale;
    bool ownsScale;
    bool ownsSize, ownsPosition, ownsColor;
    Color originalColor;
    public SavedRect(IntPtr obj)
    {
        handle = IL2CPP.il2cpp_gchandle_new(obj, false);
        AuthoredMin = lastMin = U.Get<V2>(obj, "anchorMin");
        AuthoredMax = lastMax = U.Get<V2>(obj, "anchorMax");
        originalSize = U.Get<V2>(obj, "sizeDelta");
        originalPosition = lastPosition = U.Get<V2>(obj, "anchoredPosition");
        originalScale = U.Get<V3>(obj, "localScale");
    }
    public void AcceptGameAnchors()
    {
        var min = U.Get<V2>(Object, "anchorMin"); var max = U.Get<V2>(Object, "anchorMax");
        if (min != lastMin) AuthoredMin = min;
        if (max != lastMax) AuthoredMax = max;
    }
    public void Anchors(float x)
    {
        var min = U.Get<V2>(Object, "anchorMin"); var max = U.Get<V2>(Object, "anchorMax");
        lastMin = min with { X = x }; lastMax = max with { X = x };
        if (Math.Abs(min.X - x) > .00001f) U.Set(Object, "anchorMin", lastMin);
        if (Math.Abs(max.X - x) > .00001f) U.Set(Object, "anchorMax", lastMax);
    }
    public void SafeRange(float fraction, float offset = 0)
    {
        var min = U.Get<V2>(Object, "anchorMin"); var max = U.Get<V2>(Object, "anchorMax");
        lastMin = min with { X = LayoutMath.ContentAnchor(AuthoredMin.X, fraction) + offset };
        lastMax = max with { X = LayoutMath.ContentAnchor(AuthoredMax.X, fraction) + offset };
        if (Math.Abs(min.X - lastMin.X) > .00001f) U.Set(Object, "anchorMin", lastMin);
        if (Math.Abs(max.X - lastMax.X) > .00001f) U.Set(Object, "anchorMax", lastMax);
    }
    public void ReanchorX(float anchor, float referenceWidth)
    {
        if (Math.Abs(AuthoredMin.X - AuthoredMax.X) > .00001f) return;
        Anchors(anchor);
        OffsetX((AuthoredMin.X - anchor) * referenceWidth);
    }
    public void PositionX(float x)
    {
        var pos = U.Get<V2>(Object, "anchoredPosition");
        if (ownsPosition && pos != lastPosition) originalPosition = pos;
        lastPosition = pos with { X = x }; ownsPosition = true;
        if (Math.Abs(pos.X - x) > .01f) U.Set(Object, "anchoredPosition", lastPosition);
    }
    public void OffsetX(float offset)
    {
        var position = U.Get<V2>(Object, "anchoredPosition");
        if (ownsPosition && position != lastPosition) originalPosition = position;
        lastPosition = originalPosition with { X = originalPosition.X + offset };
        ownsPosition = true;
        if (position != lastPosition) U.Set(Object, "anchoredPosition", lastPosition);
    }
    public void WidthToViewport(IntPtr canvas)
    {
        var bounds = U.Get<R4>(canvas, "rect");
        var parent = U.Parent(Object);
        var low = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X, 0, 0)));
        var high = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X + bounds.Width, 0, 0)));
        var scale = U.Get<V3>(Object, "localScale");
        if (Math.Abs(scale.X) < .001f) return;
        Width(Math.Abs(high.X - low.X) / Math.Abs(scale.X));
        CenterX(canvas, bounds.X + bounds.Width * .5f);
    }
    public void StretchInsets(float factorX, float factorY)
    {
        var position = U.Get<V2>(Object, "anchoredPosition");
        if (ownsPosition && position != lastPosition) originalPosition = position;
        lastPosition = new(originalPosition.X * factorX, originalPosition.Y * factorY);
        ownsPosition = true;
        if (position != lastPosition) U.Set(Object, "anchoredPosition", lastPosition);
    }
    public void CornerInsets(float referenceHeight, float viewportHeight)
    {
        var position = U.Get<V2>(Object, "anchoredPosition");
        if (ownsPosition && position != lastPosition) originalPosition = position;
        lastPosition = new(originalPosition.X, LayoutMath.CornerOffset(originalPosition.Y, referenceHeight, viewportHeight));
        ownsPosition = true;
        if (position != lastPosition) U.Set(Object, "anchoredPosition", lastPosition);
    }
    public void Width(float width) => Size(U.Get<V2>(Object, "sizeDelta") with { X = width });
    public void Height(float height) => Size(U.Get<V2>(Object, "sizeDelta") with { Y = height });
    public void CenterX(IntPtr canvas, float x)
    {
        var bounds = U.Get<R4>(Object, "rect");
        var center = U.Map(U.Parent(Object), "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(x, 0, 0)));
        var scale = U.Get<V3>(Object, "localScale");
        float desired = center.X - (bounds.X + bounds.Width * .5f) * scale.X;
        var current = U.Get<V3>(Object, "localPosition");
        PositionX(U.Get<V2>(Object, "anchoredPosition").X + desired - current.X);
    }
    public void LeftEdge(IntPtr canvas, float inset)
    {
        var viewport = U.Get<R4>(canvas, "rect");
        var left = U.Map(U.Parent(Object), "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(viewport.X, 0, 0)));
        var bounds = U.Get<R4>(Object, "rect");
        var scale = U.Get<V3>(Object, "localScale");
        var current = U.Get<V3>(Object, "localPosition");
        float desired = left.X + inset - bounds.X * scale.X;
        PositionX(U.Get<V2>(Object, "anchoredPosition").X + desired - current.X);
    }
    public void CenterVisualX(IntPtr canvas, IntPtr visual)
    {
        var bounds = U.Get<R4>(visual, "rect");
        var parent = U.Parent(Object);
        var visibleCenter = U.Map(parent, "InverseTransformPoint", U.Map(visual, "TransformPoint",
            new V3(bounds.X + bounds.Width * .5f, bounds.Y + bounds.Height * .5f, 0)));
        var viewport = U.Get<R4>(canvas, "rect");
        var desired = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint",
            new V3(viewport.X + viewport.Width * .5f, 0, 0)));
        PositionX(U.Get<V2>(Object, "anchoredPosition").X + desired.X - visibleCenter.X);
    }
    public void Size(V2 size)
    {
        ownsSize = true;
        if (U.Get<V2>(Object, "sizeDelta") != size) U.Set(Object, "sizeDelta", size);
    }
    public void Background(IntPtr image)
    {
        if (image == IntPtr.Zero) return;
        if (!ownsColor) { originalColor = U.Get<Color>(image, "color"); ownsColor = true; }
        var black = new Color(0, 0, 0, 1);
        if (U.Get<Color>(image, "color") != black) U.Set(image, "color", black);
    }
    public void Cover(float width, float height, bool allowShrink = false)
    {
        var bounds = U.Get<R4>(Object, "rect");
        float cover = LayoutMath.BackgroundCover(width, height, bounds.Width * Math.Abs(originalScale.X), bounds.Height * Math.Abs(originalScale.Y));
        if (allowShrink && bounds.Width > 0 && bounds.Height > 0)
            cover = Math.Max(width / (bounds.Width * Math.Abs(originalScale.X)), height / (bounds.Height * Math.Abs(originalScale.Y)));
        ownsScale = true;
        var desired = new V3(originalScale.X * cover, originalScale.Y * cover, originalScale.Z);
        if (U.Get<V3>(Object, "localScale") != desired) U.Set(Object, "localScale", desired);
    }
    public void CoverViewport(IntPtr canvas, float aspect = 0)
    {
        var bounds = U.Get<R4>(canvas, "rect");
        if (aspect > 0)
        {
            float width = LayoutMath.ContentWidth(bounds.Width, bounds.Height, aspect);
            bounds = bounds with { X = bounds.X + (bounds.Width - width) / 2, Width = width };
        }
        var parent = U.Parent(Object);
        var low = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X, bounds.Y, 0)));
        var high = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X + bounds.Width, bounds.Y + bounds.Height, 0)));
        Cover(Math.Abs(high.X - low.X), Math.Abs(high.Y - low.Y), aspect > 0);
        var art = U.Get<R4>(Object, "rect");
        var scale = U.Get<V3>(Object, "localScale");
        var current = U.Get<V3>(Object, "localPosition");
        var desired = current with {
            X = (low.X + high.X) * .5f - (art.X + art.Width * .5f) * scale.X,
            Y = (low.Y + high.Y) * .5f - (art.Y + art.Height * .5f) * scale.Y
        };
        ownsPosition = true;
        if (Math.Abs(current.X - desired.X) > .01f || Math.Abs(current.Y - desired.Y) > .01f) U.Set(Object, "localPosition", desired);
        lastPosition = U.Get<V2>(Object, "anchoredPosition");
    }
    public void RestoreScale()
    {
        if (ownsScale) { U.Set(Object, "localScale", originalScale); ownsScale = false; }
    }
    public void UniformScale(float factor)
    {
        ownsScale = true;
        var desired = new V3(originalScale.X * factor, originalScale.Y * factor, originalScale.Z);
        if (U.Get<V3>(Object, "localScale") != desired) U.Set(Object, "localScale", desired);
    }
    public void StageScale(float x, float y)
    {
        ownsScale = true;
        var desired = new V3(originalScale.X * x, originalScale.Y * y, originalScale.Z);
        if (U.Get<V3>(Object, "localScale") != desired) U.Set(Object, "localScale", desired);
    }
    public void Viewport(IntPtr canvas)
    {
        var bounds = U.Get<R4>(canvas, "rect");
        var parent = U.Parent(Object);
        var low = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X, bounds.Y, 0)));
        var high = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X + bounds.Width, bounds.Y + bounds.Height, 0)));
        var pivot = U.Get<V2>(Object, "pivot");
        Anchors(.5f);
        var min = U.Get<V2>(Object, "anchorMin"); var max = U.Get<V2>(Object, "anchorMax");
        lastMin = min with { Y = .5f }; lastMax = max with { Y = .5f };
        if (min.Y != .5f) U.Set(Object, "anchorMin", lastMin);
        if (max.Y != .5f) U.Set(Object, "anchorMax", lastMax);
        var scale = U.Get<V3>(Object, "localScale");
        Size(new(Math.Abs(high.X - low.X) / Math.Max(.0001f, Math.Abs(scale.X)), Math.Abs(high.Y - low.Y) / Math.Max(.0001f, Math.Abs(scale.Y))));
        var position = new V3(low.X + (high.X - low.X) * pivot.X, low.Y + (high.Y - low.Y) * pivot.Y, low.Z);
        var current = U.Get<V3>(Object, "localPosition");
        ownsPosition = true;
        if (Math.Abs(current.X - position.X) > .01f || Math.Abs(current.Y - position.Y) > .01f) U.Set(Object, "localPosition", position);
        lastPosition = U.Get<V2>(Object, "anchoredPosition");
    }
    public void HorizontalViewport(IntPtr canvas)
    {
        var bounds = U.Get<R4>(canvas, "rect");
        var parent = U.Parent(Object);
        var parentBounds = U.Get<R4>(parent, "rect");
        if (parentBounds.Width <= 0) return;
        var low = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X, bounds.Y, 0)));
        var high = U.Map(parent, "InverseTransformPoint", U.Map(canvas, "TransformPoint", new V3(bounds.X + bounds.Width, bounds.Y, 0)));
        float left = (low.X - parentBounds.X) / parentBounds.Width;
        float right = (high.X - parentBounds.X) / parentBounds.Width;
        AcceptGameAnchors();
        SafeRange(right - left, (left + right) * .5f - .5f);
    }
    public void Restore()
    {
        RestoreScale();
        AcceptGameAnchors();
        if (U.Get<V2>(Object, "anchorMin") != AuthoredMin) U.Set(Object, "anchorMin", AuthoredMin);
        if (U.Get<V2>(Object, "anchorMax") != AuthoredMax) U.Set(Object, "anchorMax", AuthoredMax);
        lastMin = AuthoredMin; lastMax = AuthoredMax;
        if (ownsSize) { U.Set(Object, "sizeDelta", originalSize); ownsSize = false; }
        if (ownsPosition) { U.Set(Object, "anchoredPosition", originalPosition); lastPosition = originalPosition; ownsPosition = false; }
        if (ownsColor)
        {
            var image = U.Component(U.GO(Object), U.Class("Image", "UnityEngine.UI", "UnityEngine.UI.dll"));
            if (image != IntPtr.Zero) U.Set(image, "color", originalColor);
            ownsColor = false;
        }
    }
    public void Dispose() => IL2CPP.il2cpp_gchandle_free(handle);
}
