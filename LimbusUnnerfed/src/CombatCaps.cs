namespace LimbusCompanyUnnerfed;

internal readonly record struct CombatCaps(bool LevelEnabled,int Level,bool UptieEnabled,int Uptie,bool ThreadspinEnabled,int Threadspin,bool UptieSync=false,bool ThreadspinSync=false)
{
 public static bool ValidLevelInsertion(string text,int position,char character)
 {
  if(character<'0'||character>'9'||position<0||position>text.Length)return false;
  var candidate=text.Insert(position,character.ToString());
  return candidate[0]!='0'&&int.TryParse(candidate,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var level)&&level>0;
 }
 public int ResolveLevel(int difficulty)=>LevelEnabled?Math.Max(0,Level==0?difficulty:Level):0;
 public int ResolveUptie(bool beforeTier4)=>UptieEnabled?(UptieSync?(beforeTier4?3:4):Uptie):0;
 public int ResolveThreadspin(bool beforeTier4)=>ThreadspinEnabled?(ThreadspinSync?(beforeTier4?3:4):Threadspin):0;
 public static int Limit(int current,int cap)=>cap>0?Math.Min(current,cap):current;
 public bool Valid=>Level>=0&&Uptie is >=1 and <=5&&Threadspin is >=1 and <=5;
}
