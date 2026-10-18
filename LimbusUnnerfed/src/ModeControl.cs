using Il2CppInterop.Runtime;
namespace LimbusCompanyUnnerfed;
internal sealed class ModeControl : IDisposable
{
 readonly IntPtr buttonHandle,labelHandle,hoverHandle,imageHandle;
 readonly IntPtr luxOverlayHandle,luxGridHandle,luxSourceHandle;
 readonly Color labelColor, imageColor;
 public IntPtr Owner {get;}
 public IntPtr Button=>IL2CPP.il2cpp_gchandle_get_target(buttonHandle);
 public IntPtr Artwork=>IL2CPP.il2cpp_gchandle_get_target(imageHandle);
 public IntPtr Label=>IL2CPP.il2cpp_gchandle_get_target(labelHandle);
 public bool Alive=>U.Alive(Button)&&U.Alive(IL2CPP.il2cpp_gchandle_get_target(labelHandle));
 public bool IsButton(IntPtr obj)=>Alive && obj==Button;
 public ModeControl(IntPtr ui,IntPtr source=default,bool lux=false)
 {
  Owner=ui;
  var enter=source==IntPtr.Zero?U.Call(ui,"get_Button_Enter"):source;
  var nativeRect=U.Transform(enter);var parent=U.Parent(nativeRect);
  if(lux)
  {
   var grid=U.Transform(U.Read<IntPtr>(ui,"_tabGridLayoutGroup"));
   luxGridHandle=IL2CPP.il2cpp_gchandle_new(grid,false);luxSourceHandle=IL2CPP.il2cpp_gchandle_new(nativeRect,false);
   var overlay=U.NewGO("Unnerfed Lux Controls",U.Transform(ui));
   luxOverlayHandle=IL2CPP.il2cpp_gchandle_new(overlay,false);parent=U.Transform(overlay);
   U.Set(overlay,"layer",U.Get<int>(U.GO(U.Transform(ui)),"layer"));
   U.Set(parent,"anchorMin",new V2(0,0));U.Set(parent,"anchorMax",new V2(1,1));
   U.Set(parent,"sizeDelta",new V2(0,0));U.Set(parent,"anchoredPosition",new V2(0,0));
   U.Set(parent,"localPosition",new V3(0,0,0));
   var group=U.Component(overlay,U.Class("CanvasGroup","UnityEngine","UnityEngine.UIModule.dll"),true);
   U.Set(group,"alpha",1f);U.Set(group,"interactable",(byte)1);U.Set(group,"blocksRaycasts",(byte)1);U.Set(group,"ignoreParentGroups",(byte)1);
  }
  var imageClass=U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll");
  var sourceImage=FindArtwork(nativeRect,imageClass,6);
  if(sourceImage==IntPtr.Zero)throw new MissingMemberException("Stage button artwork");
  var go=U.NewGO("LimbusCompanyUnnerfed_LevelCap",parent);var rect=U.Transform(go);
  U.Set(go,"layer",U.Get<int>(U.GO(parent),"layer"));
  U.Set(rect,"anchorMin",U.Get<V2>(nativeRect,"anchorMin"));U.Set(rect,"anchorMax",U.Get<V2>(nativeRect,"anchorMax"));
  U.Set(rect,"pivot",new V2(.5f,.5f));U.Set(rect,"sizeDelta",new V2(350,75));
  U.Set(rect,"anchoredPosition",new V2(-310,70));
  if(lux)
  {
   var layout=U.Component(go,U.Class("LayoutElement","UnityEngine.UI","UnityEngine.UI.dll"),true);
   U.Set(layout,"ignoreLayout",(byte)1);
   U.Set(rect,"anchorMin",new V2(.5f,0));U.Set(rect,"anchorMax",new V2(.5f,0));
   U.Set(rect,"pivot",new V2(.5f,1));U.Set(rect,"anchoredPosition",new V2(0,-18));
   U.Set(rect,"sizeDelta",new V2(U.Get<R4>(nativeRect,"rect").Width,75));
  }
  var image=U.Component(go,imageClass,true);
  U.Set(image,"raycastTarget",(byte)1);
  U.ObjectArg(image,"set_sprite",U.Call(sourceImage,"get_overrideSprite"));
  U.Set(image,"type",U.Get<int>(sourceImage,"type"));
  var nativeColor=U.Get<Color>(sourceImage,"color");imageColor=nativeColor with{A=1};U.Set(image,"color",imageColor);
  imageHandle=IL2CPP.il2cpp_gchandle_new(image,false);
  var sourceHover=U.Find(nativeRect,"[Image]Hover",3);
  if(sourceHover!=IntPtr.Zero)
  {
   var hoverGo=U.NewGO("Hover",rect);var hover=U.Component(hoverGo,imageClass,true);
   var hoverImage=U.Component(U.GO(sourceHover),imageClass);
   U.ObjectArg(hover,"set_sprite",U.Call(hoverImage,"get_sprite"));U.Set(hover,"type",U.Get<int>(hoverImage,"type"));
   U.Set(hover,"raycastTarget",(byte)0);
   var hr=U.Transform(hoverGo);U.Set(hr,"anchorMin",new V2(0,0));U.Set(hr,"anchorMax",new V2(1,1));U.Set(hr,"sizeDelta",new V2(0,0));U.Set(hr,"anchoredPosition",new V2(0,0));
   hoverHandle=IL2CPP.il2cpp_gchandle_new(hover,false);U.ActiveSelf(hoverGo,false);
  }
  var button=U.Component(go,U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),true);
  U.Set(button,"interactable",(byte)1);
  U.ObjectArg(button,"set_targetGraphic",image);
  buttonHandle=IL2CPP.il2cpp_gchandle_new(button,false);
  var tmp=U.Class("TextMeshProUGUI","TMPro","Unity.TextMeshPro.dll");
  var sample=FindComponent(nativeRect,tmp,5);
  labelColor=sample==IntPtr.Zero?new Color(.9f,.77f,.55f,1):U.Get<Color>(sample,"color");
  if(labelColor.A<.1f)labelColor=new Color(.9f,.77f,.55f,1);
  var textGo=U.NewGO("Label",rect);U.Set(textGo,"layer",U.Get<int>(go,"layer"));var text=U.Component(textGo,tmp,true);
  if(sample!=IntPtr.Zero){U.ObjectArg(text,"set_font",U.Call(sample,"get_font"));U.ObjectArg(text,"set_fontSharedMaterial",U.Call(sample,"get_fontSharedMaterial"));}
  if(text==IntPtr.Zero)throw new MissingMemberException("Stage button label");
  U.Set(text,"fontSize",30f);U.Set(text,"raycastTarget",(byte)0);
  U.Set(text,"richText",(byte)1);
  var tr=U.Transform(text);U.Set(tr,"anchorMin",new V2(0,0));U.Set(tr,"anchorMax",new V2(1,1));
  U.Set(tr,"pivot",new V2(.5f,.5f));U.Set(tr,"sizeDelta",new V2(-30,-10));U.Set(tr,"anchoredPosition",new V2(0,0));
  U.Set(text,"alignment",514);
  labelHandle=IL2CPP.il2cpp_gchandle_new(text,false);
 }
 public unsafe void Refresh(bool enabled,int difficulty=0)
 {
  var label=IL2CPP.il2cpp_gchandle_get_target(labelHandle);
  U.ActiveSelf(U.GO(label),true);U.Set(label,"enabled",(byte)1);U.Set(label,"color",labelColor);
  U.Text(label,enabled?$"<color=#FFB345>Level Cap: {(difficulty>0?difficulty.ToString():"On")}</color>":"Level Cap: Off");
  byte yes=1;void** a=stackalloc void*[2];a[0]=&yes;a[1]=&yes;
  U.Invoke(U.Method(IL2CPP.il2cpp_object_get_class(label),"ForceMeshUpdate",2),label,a);
 }
 public void Refresh(CombatCaps caps,int difficulty=0)
 {
  Refresh(caps.LevelEnabled,difficulty);
  var parts=new List<string>();
  if(caps.LevelEnabled)parts.Add($"Lv {(caps.Level==0?(difficulty>0?difficulty.ToString():"Auto"):caps.Level.ToString())}");
  if(caps.UptieEnabled)parts.Add(caps.UptieSync?"U Sync":$"U{caps.Uptie}");
  if(caps.ThreadspinEnabled)parts.Add(caps.ThreadspinSync?"T Sync":$"T{caps.Threadspin}");
  U.Text(Label,parts.Count==0?"Combat Caps: Off":string.Join(" / ",parts));
  U.Set(Label,"fontSize",26f);
 }
 public void SetVisible(bool visible){if(Alive){U.ActiveSelf(U.GO(Button),visible);if(visible){SyncLuxPosition();U.Call(U.Transform(Button),"SetAsLastSibling");}}}
 void SyncLuxPosition()
 {
  if(luxGridHandle==IntPtr.Zero)return;
  var grid=IL2CPP.il2cpp_gchandle_get_target(luxGridHandle);
  var source=IL2CPP.il2cpp_gchandle_get_target(luxSourceHandle);
  var rect=U.Transform(Button);var parent=U.Parent(rect);var bounds=U.Get<R4>(grid,"rect");
  var bottom=U.Map(parent,"InverseTransformPoint",U.Map(grid,"TransformPoint",new V3(bounds.X+bounds.Width*.5f,bounds.Y,0)));
  U.Set(rect,"anchorMin",new V2(.5f,.5f));U.Set(rect,"anchorMax",new V2(.5f,.5f));
  U.Set(rect,"pivot",new V2(.5f,1));U.Set(rect,"sizeDelta",new V2(U.Get<R4>(source,"rect").Width,75));
  U.Set(rect,"localPosition",bottom with{Y=bottom.Y-32,Z=0});
 }
 public void Hover(bool value){if(hoverHandle==IntPtr.Zero){var image=IL2CPP.il2cpp_gchandle_get_target(imageHandle);if(U.Alive(image))U.Set(image,"color",value?new Color(1,.72f,.3f,1):imageColor);return;}var hover=IL2CPP.il2cpp_gchandle_get_target(hoverHandle);if(U.Alive(hover))U.ActiveSelf(U.GO(hover),value);}
 static IntPtr FindArtwork(IntPtr root,IntPtr klass,int depth)
 {
  var image=U.Component(U.GO(root),klass);
  if(image!=IntPtr.Zero&&U.Call(image,"get_overrideSprite")!=IntPtr.Zero)return image;
  if(depth>0)foreach(var child in U.Children(root))
  {
   var name=U.Name(child);
   if(name.Contains("Hover",StringComparison.OrdinalIgnoreCase)||name.Contains("Icon",StringComparison.OrdinalIgnoreCase))continue;
   var found=FindArtwork(child,klass,depth-1);if(found!=IntPtr.Zero)return found;
  }
  return IntPtr.Zero;
 }
 internal static bool ArtworkReady(IntPtr source)=>source!=IntPtr.Zero&&FindArtwork(U.Transform(source),U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"),6)!=IntPtr.Zero;
 static IntPtr FindComponent(IntPtr root,IntPtr klass,int depth){var c=U.Component(U.GO(root),klass);if(c!=IntPtr.Zero||depth==0)return c;foreach(var child in U.Children(root)){c=FindComponent(child,klass,depth-1);if(c!=IntPtr.Zero)return c;}return IntPtr.Zero;}
 public void Dispose(){if(U.Alive(Button))U.ActiveSelf(U.GO(Button),false);if(luxOverlayHandle!=IntPtr.Zero){var overlay=IL2CPP.il2cpp_gchandle_get_target(luxOverlayHandle);if(U.Alive(overlay))N.DestroyImmediate(overlay);IL2CPP.il2cpp_gchandle_free(luxOverlayHandle);IL2CPP.il2cpp_gchandle_free(luxGridHandle);IL2CPP.il2cpp_gchandle_free(luxSourceHandle);}IL2CPP.il2cpp_gchandle_free(buttonHandle);IL2CPP.il2cpp_gchandle_free(labelHandle);IL2CPP.il2cpp_gchandle_free(imageHandle);if(hoverHandle!=IntPtr.Zero)IL2CPP.il2cpp_gchandle_free(hoverHandle);}
}
