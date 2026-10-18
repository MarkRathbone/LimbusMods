using BepInEx.Configuration;
namespace LimbusCompanyUnnerfed;
internal sealed class CapSettings
{
 readonly ConfigEntry<bool> levelEnabled,uptieEnabled,threadEnabled,uptieSync,threadSync;
 readonly ConfigEntry<int> level,uptie,thread;
 public CapSettings(ConfigFile config)
 {
  levelEnabled=config.Bind("Combat Caps","LevelEnabled",false,"Cap combat identity levels.");
  level=config.Bind("Combat Caps","Level",0,new ConfigDescription("Maximum combat level; 0 uses the current encounter difficulty.",new AcceptableValueRange<int>(0,int.MaxValue)));
  uptieEnabled=config.Bind("Combat Caps","UptieEnabled",false,"Cap combat identity Uptie tiers.");
  uptie=config.Bind("Combat Caps","Uptie",4,new ConfigDescription("Maximum identity Uptie tier.",new AcceptableValueRange<int>(1,5)));
  threadEnabled=config.Bind("Combat Caps","ThreadspinEnabled",false,"Cap combat E.G.O Threadspin tiers.");
  thread=config.Bind("Combat Caps","Threadspin",4,new ConfigDescription("Maximum E.G.O Threadspin tier.",new AcceptableValueRange<int>(1,5)));
  uptieSync=config.Bind("Combat Caps","UptieSync",false,"Sync identity Uptie to III for encounters released before July 20, 2023; IV otherwise.");
  threadSync=config.Bind("Combat Caps","ThreadspinSync",false,"Sync E.G.O Threadspin to III for encounters released before July 20, 2023; IV otherwise. Select manual V to allow Threadspin V.");
 }
 public CombatCaps Value=>new(levelEnabled.Value,level.Value,uptieEnabled.Value,uptie.Value,threadEnabled.Value,thread.Value,uptieSync.Value,threadSync.Value);
 public void Save(CombatCaps value)
 {
  if(!value.Valid)throw new ArgumentOutOfRangeException(nameof(value));
  levelEnabled.Value=value.LevelEnabled;level.Value=value.Level;
  uptieEnabled.Value=value.UptieEnabled;uptie.Value=value.Uptie;
  threadEnabled.Value=value.ThreadspinEnabled;thread.Value=value.Threadspin;
  uptieSync.Value=value.UptieSync;threadSync.Value=value.ThreadspinSync;
 }
}
