using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Hook;
using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
namespace LimbusCompanyUnnerfed;

internal sealed class PrivacyTextHooks : IDisposable
{
 readonly List<INativeDetour> hooks=new();
 readonly List<Delegate> delegates=new();
 readonly ConfigEntry<bool> enabled;
 readonly ConfigEntry<string> managerId;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SetText(IntPtr obj,IntPtr text,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SetFormattedText(IntPtr obj,IntPtr text,byte sync,IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Enable(IntPtr obj,IntPtr method);
 public PrivacyTextHooks(ConfigEntry<bool> enabled,ConfigEntry<string> managerId)
 {
  this.enabled=enabled;this.managerId=managerId;
  var klass=U.Class("TMP_Text","TMPro","Unity.TextMeshPro.dll");
  SetText original=null!;
  SetText replacement=(obj,text,method)=>original!(obj,Redact(text),method);
  Install(Signature.Check(klass,"set_text","System.Void","System.String"),replacement,out original);
  SetFormattedText formattedOriginal=null!;
  SetFormattedText formattedReplacement=(obj,text,sync,method)=>formattedOriginal!(obj,Redact(text),sync,method);
  Install(Signature.Check(klass,"SetText","System.Void","System.String","System.Boolean"),formattedReplacement,out formattedOriginal);
  Enable enableOriginal=null!;
  Enable enableReplacement=(obj,method)=>{
   try{var text=U.Call(obj,"get_text");var hidden=Redact(text);
   if(hidden!=text)U.ObjectArg(obj,"set_text",hidden);}catch{}
   enableOriginal!(obj,method);
  };
  Install(Signature.Check(U.Class("TextMeshProUGUI","TMPro","Unity.TextMeshPro.dll"),"OnEnable","System.Void"),enableReplacement,out enableOriginal);
 }
 IntPtr Redact(IntPtr text)
 {
  if(!enabled.Value||text==IntPtr.Zero||string.IsNullOrWhiteSpace(managerId.Value))return text;
  var value=IL2CPP.Il2CppStringToManaged(text)??"";
  var id=managerId.Value.Trim();
  if(!value.Contains(id,StringComparison.Ordinal))return text;
  return IL2CPP.ManagedStringToIl2Cpp(value.Replace(id,"[Hidden]",StringComparison.Ordinal));
 }
 void Install<T>(IntPtr method,T replacement,out T original)where T:Delegate
 {
  hooks.Add(INativeDetour.CreateAndApply(Marshal.ReadIntPtr(method),replacement,out original));
  delegates.Add(replacement);delegates.Add(original);
 }
 public void Dispose(){foreach(var h in hooks.AsEnumerable().Reverse())h.Dispose();hooks.Clear();delegates.Clear();}
}
