using LimbusZoom;

static void Near(float actual, float expected)
{
    if (Math.Abs(actual - expected) > .0001f) throw new Exception($"Expected {expected}, got {actual}");
}

float scale = 1;
for (int i = 0; i < 100; i++) scale = ZoomMath.Next(scale, -1, .12f, .25f, 1);
Near(scale, .25f);
for (int i = 0; i < 100; i++) scale = ZoomMath.Next(scale, 1, .12f, .25f, 1);
Near(scale, 1);
Near(ZoomMath.Next(1, float.NaN, .12f, .25f, 1), 1);
var outPosition = ZoomMath.Position(100, -50, 20, 30, .5f);
Near(outPosition.X, 60); Near(outPosition.Y, -10);
var backPosition = ZoomMath.Position(outPosition.X, outPosition.Y, 20, 30, 2);
Near(backPosition.X, 100); Near(backPosition.Y, -50);
Near((20 - outPosition.X) / .5f, 20 - 100);
Near(ZoomMath.Cover(3000, 1350, 6000, 2700), .5f);
Near(ZoomMath.Cover(3000, 1350, 6000, 1350), 1);
Near(ZoomMath.Correction(-100, 1200, 0, 1000), 0);
Near(ZoomMath.Correction(100, 1200, 0, 1000), -100);
Near(ZoomMath.Correction(-300, 1200, 0, 1000), 100);
Near(ZoomMath.Correction(0, 500, 0, 1000), 250);
// Restoring native scale keeps the map point under the chosen viewport anchor.
var normalPosition = ZoomMath.Position(-500, -200, 1500, 675, 2);
Near((1500 - normalPosition.X), (1500 + 500) / .5f);
Near((675 - normalPosition.Y), (675 + 200) / .5f);
Console.WriteLine("PASS: zoom limits, artwork coverage, edge clamping, cursor anchoring and focus-coordinate restoration.");
