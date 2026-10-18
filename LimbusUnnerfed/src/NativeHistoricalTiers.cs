namespace LimbusCompanyUnnerfed;
internal static class NativeHistoricalTiers
{
 public static bool BeforeTier4(IntPtr stage)
 {
  int id=U.Read<int>(stage,"id");
  if(!HistoricalTiers.MayMatch(id))return false;
  var waves=U.Read<IntPtr>(stage,"waveList");
  if(waves==IntPtr.Zero)return false;
  return HistoricalTiers.BeforeTier4(id,N.Items(waves).Select(w=>
  {
   var units=U.Read<IntPtr>(w,"unitList");
   return units==IntPtr.Zero?Array.Empty<int>():N.Items(units).Select(u=>U.Read<int>(u,"unitID")).ToArray();
  }));
 }
}
