namespace UltrawideLimbus;
internal static class ResolutionChoices
{
 public static bool IsWide(int width,int height)=>height>0&&(double)width/height>16d/9d+.01;
 public static IEnumerable<(int Width,int Height)> ForDisplay(int width,int height,int currentWidth,int currentHeight)
 {
  var sizes=new[]{(2560,1080),(3440,1440),(3840,1600),(3840,1080),(5120,1440),(5120,2160),(7680,2160),(width,height),(currentWidth,currentHeight)};
  return sizes.Where(s=>s.Item1>0&&s.Item2>0&&IsWide(s.Item1,s.Item2)&&
   ((s.Item1<=width&&s.Item2<=height)||(s.Item1==currentWidth&&s.Item2==currentHeight)))
   .Distinct().OrderBy(s=>s.Item1).ThenBy(s=>s.Item2);
 }
}
