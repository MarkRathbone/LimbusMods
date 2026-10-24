using UltrawideLimbus;

static void Equal(float expected, float actual)
{
    if (Math.Abs(expected - actual) > .01f) throw new Exception($"Expected {expected}, got {actual}");
}
Equal(2560, LayoutMath.ContentWidth(3440, 1440, 16f / 9f));
Equal(2560, LayoutMath.ContentWidth(2560, 1440, 16f / 9f));
Equal(1920, LayoutMath.ContentWidth(1920, 1440, 16f / 9f));
Equal(3000, LayoutMath.ContentWidth(4240.321f, 1687.5f, 16f / 9f));
float fraction = 2560f / 3440;
Equal(440f / 3440, LayoutMath.ContentAnchor(0, fraction));
Equal(3000f / 3440, LayoutMath.ContentAnchor(1, fraction));
Equal(.5f, LayoutMath.ContentAnchor(.5f, fraction));
Equal(.4f, LayoutMath.ContentAnchor(.4f, 1));
Equal(3440f / 2560, LayoutMath.BackgroundCover(3440, 1440, 2560, 1440));
Equal(1, LayoutMath.BackgroundCover(3440, 1440, 4000, 3000));
Equal(2, LayoutMath.BackgroundCover(3440, 1440, 2000, 720));
if (!LayoutMath.IsSidePanel(1, 1, 1650, 3000) || !LayoutMath.IsSidePanel(0, 0, 1650, 3000)
    || LayoutMath.IsSidePanel(.5f, .5f, 1650, 3000) || LayoutMath.IsSidePanel(0, 1, 1650, 3000)
    || LayoutMath.IsSidePanel(1, 1, 3000, 3000)) throw new Exception("Side-panel shade classification failed");
float viewport = 4240.321f;
float parked = 2319 + LayoutMath.SlideAnchorOffset(2319, 3000, viewport) * viewport;
if (parked - 800 <= viewport / 2) throw new Exception("Parked popup exposed by ultrawide viewport");
Equal(0, LayoutMath.SlideAnchorOffset(0, 3000, viewport));
Equal(0, LayoutMath.SlideAnchorOffset(2319, 3000, 3000));
Equal(0, LayoutMath.SlideAnchorOffset(2319, 3000, 2400));
Equal(-parked, -2319 + LayoutMath.SlideAnchorOffset(-2319, 3000, viewport) * viewport);
Equal(parked / 2, 1159.5f + LayoutMath.SlideAnchorOffset(1159.5f, 3000, viewport) * viewport);
float bottom = LayoutMath.CornerOffset(-256.52f, 720, 900);
Equal(-346.52f, bottom);
Equal(360 - 256.52f, 450 + bottom);
float top = LayoutMath.CornerOffset(274.6f, 720, 900);
Equal(360 - 274.6f, 450 - top);
Equal(-256.52f, LayoutMath.CornerOffset(-256.52f, 720, 720));
Equal(-256.52f, LayoutMath.CornerOffset(-256.52f, 0, 900));
Equal(0, LayoutMath.CornerOffset(0, 720, 900));
Equal(-256.52f, LayoutMath.CornerOffset(bottom, 900, 720));
float storyHeight = LayoutMath.StoryReferenceHeight(3000);
Equal(1687.5f, storyHeight);
Equal(2560f / 3000, 1440f / storyHeight);
Equal(2048, 2400 * (1440f / storyHeight));
Equal(0.8f, 1350 / storyHeight);
Console.WriteLine("PASS: menu bounds, backgrounds, slide panels, cinematic corners, and VN reference scale.");

Equal(1687.5f, LayoutMath.StoryReferenceHeight(3000));
Equal(3000, LayoutMath.ContentWidth(4031.25f, 1687.5f, 16f / 9f));
Equal(3000, LayoutMath.ContentWidth(3000, 1687.5f, 16f / 9f));
Equal(515.625f, (4031.25f - 3000) / 2);
if (ResolutionChoices.IsWide(2560,1440) || !ResolutionChoices.IsWide(3440,1440) || ResolutionChoices.IsWide(0,0)) throw new Exception("Resolution aspect detection failed");
var choices=ResolutionChoices.ForDisplay(3440,1440,3440,1440).ToArray();
if (!choices.Contains((3440,1440)) || choices.Contains((5120,1440)) || choices.Distinct().Count()!=choices.Length) throw new Exception("Ultrawide resolution choices failed");
if (ResolutionChoices.ForDisplay(1920,1080,1920,1080).Any()) throw new Exception("Oversized resolution offered");
if (!ResolutionChoices.ForDisplay(1920,1080,3440,1440).Contains((3440,1440))) throw new Exception("Current resolution lost");
Console.WriteLine("PASS: automatic aspect detection and display-sized ultrawide choices.");

var projection = LayoutMath.PartProjectionReference((3750,1687.5f),4031.25f,1687.5f);
Equal(4031.25f,projection.X); Equal(1687.5f,projection.Y);
Equal(2015.625f,1720f * projection.X / 3440f);
Equal(843.75f,720f * projection.Y / 1440f);
var invalidProjection=LayoutMath.PartProjectionReference(projection,0,0);
Equal(projection.X,invalidProjection.X);Equal(projection.Y,invalidProjection.Y);
Console.WriteLine("PASS: part projection uses canvas dimensions independently of scaler reference dimensions.");

Equal(1, LayoutMath.TransitionWidthScale(2560,1440));
Equal(3440f/2560, LayoutMath.TransitionWidthScale(3440,1440));
Equal(2, LayoutMath.TransitionWidthScale(5120,1440));
Equal(1, LayoutMath.TransitionWidthScale(0,0));
Equal(1, LayoutMath.TransitionWidthScale(1920,1440));
Console.WriteLine("Sisyphe transition stage width checks passed for 16:9, ultrawide, super-ultrawide and uninitialized bounds.");

foreach (var name in new[] { "Prefab_Walpu4UIPanel", "Prefab_Walpu5UIPanel(Clone)", "Prefab_Walpu6UIPanel(Clone)", "Prefab_Walpu8UIPanel(Clone)" })
    if (!LayoutMath.IsWalpurgisPanel(name)) throw new Exception("Walpurgis menu not recognized: " + name);
foreach (var name in new[] { "Prefab_Walpu6StageSlot", "Prefab_WalpuUIPanel", "Prefab_Walpu6DetailsUIPanel", "Prefab_BossRaidUIPanel(Clone)" })
    if (LayoutMath.IsWalpurgisPanel(name)) throw new Exception("Unrelated UI classified as event menu: " + name);
var squareBackground = LayoutMath.SpriteDrawingSize(3000, 8437.5f, 1, true);
Equal(3000, squareBackground.Width); Equal(3000, squareBackground.Height);
var wideBackground = LayoutMath.SpriteDrawingSize(3000, 3000, 16f / 9, true);
Equal(3000, wideBackground.Width); Equal(1687.5f, wideBackground.Height);
Console.WriteLine("PASS: Walpurgis menu scope and aspect-preserving background edge geometry.");
