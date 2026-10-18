using Il2CppInterop.Runtime;
using System.Globalization;
namespace LimbusCompanyUnnerfed;

internal sealed class CapEditor : IDisposable
{
 readonly List<IntPtr> handles=new();
 readonly Dictionary<IntPtr,Action> actions=new();
 readonly Dictionary<IntPtr,IntPtr> buttonImages=new();
 readonly List<IntPtr> materials=new();
 readonly List<IntPtr> blockedRaycasters=new();
 readonly IntPtr root,artwork,fontSample;
 readonly IntPtr[] inputs=new IntPtr[3],stateLabels=new IntPtr[3];
 readonly bool[] enabled=new bool[3];
 readonly bool[] synced=new bool[3];
 readonly string[] manual=new string[3];
 readonly IntPtr[] syncLabels=new IntPtr[3];
 readonly Action<CombatCaps> save;
 readonly Action close;
 readonly IntPtr error;
 IntPtr Keep(IntPtr value){handles.Add(IL2CPP.il2cpp_gchandle_new(value,false));return value;}
 public CapEditor(ModeControl source,CombatCaps values,Action<CombatCaps> save,Action close)
 {
  this.save=save;this.close=close;artwork=source.Artwork;fontSample=source.Label;
  try {
  var parent=U.Parent(U.Transform(source.Button));
  var canvas=U.Class("Canvas","UnityEngine","UnityEngine.UIModule.dll");
  for(var t=parent;t!=IntPtr.Zero;t=U.Parent(t))if(U.Component(U.GO(t),canvas)!=IntPtr.Zero)parent=t;
  root=Keep(U.NewGO("Unnerfed_CombatCaps",parent));U.ActiveSelf(root,false);var rt=U.Transform(root);
  var modalCanvas=U.Component(root,canvas,true);
  U.Set(modalCanvas,"overrideSorting",(byte)1);U.Set(modalCanvas,"sortingOrder",32000);
  var modalRaycaster=U.Component(root,U.Class("GraphicRaycaster","UnityEngine.UI","UnityEngine.UI.dll"),true);
  var group=U.Component(root,U.Class("CanvasGroup","UnityEngine","UnityEngine.UIModule.dll"),true);
  U.Set(group,"alpha",1f);U.Set(group,"interactable",(byte)1);U.Set(group,"blocksRaycasts",(byte)1);U.Set(group,"ignoreParentGroups",(byte)1);
  U.Set(rt,"anchorMin",new V2(0,0));U.Set(rt,"anchorMax",new V2(1,1));U.Set(rt,"sizeDelta",new V2(0,0));U.Set(rt,"anchoredPosition",new V2(0,0));
  var shade=U.Component(root,U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"),true);
  U.Set(shade,"color",new Color(0,0,0,.88f));U.Set(shade,"raycastTarget",(byte)1);
  var blocker=Keep(U.Component(root,U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),true));
  U.ObjectArg(blocker,"set_targetGraphic",shade);U.Set(blocker,"transition",0);
  U.Set(blocker,"interactable",(byte)1);actions[blocker]=()=>{};
  U.Set(root,"layer",U.Get<int>(U.GO(parent),"layer"));
  var nativePopup=FindNativePopup();
  if(nativePopup==IntPtr.Zero)throw new MissingMemberException("Native TextOkPopup window");
  var nativeBase=U.Find(nativePopup,"PopupBase",3);
  if(nativeBase==IntPtr.Zero)throw new MissingMemberException("Native popup base");
  var panelGo=Keep(N.Instantiate(U.GO(nativeBase)));U.ActiveSelf(panelGo,false);
  var panel=U.Transform(panelGo);N.Parent(panel,rt);
  foreach(var child in U.Children(panel).ToArray())
  {
   var name=U.Name(child);
   if(name is "Frame" or "ContainerImage" or "Logo")U.ActiveSelf(U.GO(child),true);
   else N.DestroyImmediate(U.GO(child));
  }
  U.Set(panel,"anchorMin",new V2(.5f,.5f));U.Set(panel,"anchorMax",new V2(.5f,.5f));
  U.Set(panel,"pivot",new V2(.5f,.5f));U.Set(panel,"sizeDelta",new V2(1200,800));
  U.Set(panel,"anchoredPosition",new V2(0,0));U.Set(panel,"localScale",new V3(1,1,1));
  U.Set(panel,"localPosition",new V3(0,0,0));
  var nativeOk=U.Find(nativeBase,"[Button]Ok",4);
  if(nativeOk!=IntPtr.Zero)
  {
   var nativeImage=U.Component(U.GO(nativeOk),U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"));
   if(nativeImage!=IntPtr.Zero&&U.Call(nativeImage,"get_overrideSprite")!=IntPtr.Zero)artwork=nativeImage;
  }
  var nativeTitle=U.Find(nativePopup,"Title",3);
  if(nativeTitle!=IntPtr.Zero)
  {
   var titleGo=Keep(N.Instantiate(U.GO(nativeTitle)));var title=U.Transform(titleGo);N.Parent(title,panel);
   foreach(var child in U.Children(title).ToArray())N.DestroyImmediate(U.GO(child));
   U.Set(title,"anchorMin",new V2(.5f,.5f));U.Set(title,"anchorMax",new V2(.5f,.5f));
   U.Set(title,"pivot",new V2(.5f,.5f));U.Set(title,"anchoredPosition",new V2(0,300));
   U.Set(title,"localScale",new V3(1,1,1));U.ActiveSelf(titleGo,true);
  }
  Text(panel,"Combat Caps",0,300,900,65,44);
  U.ActiveSelf(panelGo,true);
  BodyText(panel,"Caps apply on combat entry and never raise an owned level or tier.",0,188,920,60,26);
  enabled[0]=values.LevelEnabled;enabled[1]=values.UptieEnabled;enabled[2]=values.ThreadspinEnabled;
  synced[0]=values.Level==0;synced[1]=values.UptieSync;synced[2]=values.ThreadspinSync;
  var names=new[]{"Identity Level","Identity Uptie","E.G.O Threadspin"};var numbers=new[]{values.Level,values.Uptie,values.Threadspin};
  for(int i=0;i<3;i++)
  {
   int row=i;float y=96-i*100;
   BodyText(panel,names[i],-330,y,320,65,31);
   manual[i]=Math.Max(1,numbers[i]).ToString(CultureInfo.InvariantCulture);
   inputs[i]=Input(panel,manual[i],0,y);
   stateLabels[i]=Button(panel,enabled[i]?"On":"Off",350,y,180,65,()=>{enabled[row]=!enabled[row];U.Text(stateLabels[row],enabled[row]?"On":"Off");});
  }
  for(int i=0;i<3;i++)
  {
   int row=i;
   syncLabels[i]=Button(panel,"Sync",160,96-i*100,140,65,()=>{
    if(!synced[row])manual[row]=IL2CPP.Il2CppStringToManaged(U.Call(inputs[row],"get_text"))??"1";
    synced[row]=!synced[row];RefreshSync(row);
   });
   RefreshSync(i);
  }
  error=BodyText(panel,"",0,-220,940,45,23);
  CloseButton(panel,parent);
  Button(panel,"Apply",0,-278,370,65,Apply);
  U.Call(rt,"SetAsLastSibling");
  U.ActiveSelf(root,true);
  foreach(var field in inputs)U.Call(field,"ForceLabelUpdate");
  BlockOtherRaycasters(modalRaycaster);
  }catch{Dispose();throw;}
 }
 IntPtr Rect(string name,IntPtr parent,float x,float y,float w,float h)
 {
  var go=Keep(U.NewGO(name,parent));U.Set(go,"layer",U.Get<int>(U.GO(parent),"layer"));var r=U.Transform(go);
  U.Set(r,"anchorMin",new V2(.5f,.5f));U.Set(r,"anchorMax",new V2(.5f,.5f));U.Set(r,"pivot",new V2(.5f,.5f));
  U.Set(r,"sizeDelta",new V2(w,h));U.Set(r,"anchoredPosition",new V2(x,y));return r;
 }
 IntPtr Text(IntPtr parent,string value,float x,float y,float w,float h,float size)
 {
  var r=Rect("Label",parent,x,y,w,h);var text=Keep(U.Component(U.GO(r),U.Class("TextMeshProUGUI","TMPro","Unity.TextMeshPro.dll"),true));
  var font=U.Call(fontSample,"get_font");U.ObjectArg(text,"set_font",font);
  var material=Keep(N.Instantiate(U.Call(fontSample,"get_fontSharedMaterial")));materials.Add(material);
  U.ObjectArg(text,"set_fontSharedMaterial",material);
  U.Set(text,"fontSize",size);U.Set(text,"color",new Color(.94f,.8f,.58f,1));U.Set(text,"alignment",514);U.Set(text,"raycastTarget",(byte)0);U.Set(text,"richText",(byte)0);U.Text(text,value);return text;
 }
 IntPtr BodyText(IntPtr parent,string value,float x,float y,float w,float h,float size)
 {
  var text=Text(parent,value,x,y,w,h,size);
  U.Set(text,"color",new Color(.13f,.075f,.035f,1));
  return text;
 }
 IntPtr Image(IntPtr rect)
 {
  var image=Keep(U.Component(U.GO(rect),U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"),true));
  U.ObjectArg(image,"set_sprite",U.Call(artwork,"get_overrideSprite"));U.Set(image,"type",U.Get<int>(artwork,"type"));
  U.Set(image,"color",new Color(1,1,1,1));U.Set(image,"raycastTarget",(byte)1);return image;
 }
 IntPtr Button(IntPtr parent,string title,float x,float y,float w,float h,Action action)
 {
  var r=Rect(title,parent,x,y,w,h);var image=Image(r);var button=Keep(U.Component(U.GO(r),U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),true));
  U.ObjectArg(button,"set_targetGraphic",image);U.Set(button,"interactable",(byte)1);actions[button]=action;buttonImages[button]=image;
  return Text(r,title,0,0,w-20,h-10,30);
 }
 IntPtr Input(IntPtr parent,string value,float x,float y)
 {
  var r=Rect("Number",parent,x,y,180,65);var image=Image(r);
  var viewport=Rect("TextArea",r,0,0,150,55);
  U.Component(U.GO(viewport),U.Class("RectMask2D","UnityEngine.UI","UnityEngine.UI.dll"),true);
  var text=Text(viewport,value,0,0,150,55,33);
  var field=Keep(U.Component(U.GO(r),U.Class("TMP_InputField","TMPro","Unity.TextMeshPro.dll"),true));
  U.ObjectArg(field,"set_textComponent",text);U.ObjectArg(field,"set_textViewport",viewport);U.ObjectArg(field,"set_targetGraphic",image);
  U.Set(field,"pointSize",33f);U.Set(field,"customCaretColor",(byte)1);
  U.Set(field,"caretColor",new Color(1,.75f,.35f,1));U.Set(field,"caretWidth",2);
  U.Set(field,"onFocusSelectAll",(byte)1);U.Set(field,"lineType",0);
  U.Set(field,"contentType",2);U.Set(field,"characterLimit",10);U.Set(field,"interactable",(byte)1);U.Set(field,"readOnly",(byte)0);
  U.ObjectArg(field,"set_text",IL2CPP.ManagedStringToIl2Cpp(value));return field;
 }
 void RefreshSync(int row)
 {
  U.Set(inputs[row],"readOnly",(byte)(synced[row]?1:0));
  U.ObjectArg(inputs[row],"set_text",IL2CPP.ManagedStringToIl2Cpp(synced[row]?"Sync":manual[row]));
  U.Text(syncLabels[row],synced[row]?"Sync: On":"Sync");
  U.Set(syncLabels[row],"color",synced[row]?new Color(1,.65f,.2f,1):new Color(.94f,.8f,.58f,1));
 }
 public bool OwnsInput(IntPtr field)=>inputs.Contains(field);
 public ushort ValidateInput(IntPtr field,string text,int position,ushort character)
 {
  int row=Array.IndexOf(inputs,field);
  if(row>=0&&synced[row])return 0;
  if(field!=inputs[0])return character is >=49 and <=53&&text.Length==0?character:(ushort)0;
  return CombatCaps.ValidLevelInsertion(text,position,(char)character)?character:(ushort)0;
 }
 void Apply()
 {
  var numbers=new int[3];
  for(int i=0;i<3;i++){if(i==0&&synced[0]){numbers[0]=0;continue;}if(!int.TryParse(synced[i]?manual[i]:IL2CPP.Il2CppStringToManaged(U.Call(inputs[i],"get_text")),NumberStyles.None,CultureInfo.InvariantCulture,out numbers[i])){U.Text(error,"Enter whole numbers in all three fields.");return;}if(numbers[i]<1){U.Text(error,"Enter a positive level, or select Sync.");return;}}
  var limits=new CombatCaps(enabled[0],numbers[0],enabled[1],numbers[1],enabled[2],numbers[2],synced[1],synced[2]);
  if(!limits.Valid){U.Text(error,"Level must be 0 or higher. Uptie and Threadspin must be 1–5.");return;}
  save(limits);U.Text(error,"Caps applied.");
 }
 IntPtr FindNativePopup()
 {
  var canvas=U.Class("Canvas","UnityEngine","UnityEngine.UIModule.dll");
  var type=IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(canvas));
  unsafe
  {
   byte includeInactive=1;void** args=stackalloc void*[2];args[0]=(void*)type;args[1]=&includeInactive;
   var array=Keep(U.Invoke(U.Method(U.Class("Object"),"FindObjectsOfType",2),IntPtr.Zero,args));
   int count=checked((int)IL2CPP.il2cpp_array_length(array));
   for(int i=0;i<count;i++)
   {
    var canvasRoot=U.Transform(System.Runtime.InteropServices.Marshal.ReadIntPtr(array,32+i*IntPtr.Size));
    var popup=U.Find(canvasRoot,"TextOkPopup Variant",3);
    if(popup!=IntPtr.Zero&&U.Find(popup,"PopupBase",3)!=IntPtr.Zero)return popup;
   }
  }
  return IntPtr.Zero;
 }
 void BlockOtherRaycasters(IntPtr own)
 {
  var klass=U.Class("BaseRaycaster","UnityEngine.EventSystems","UnityEngine.UI.dll");
  var type=IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(klass));
  unsafe
  {
   void** args=stackalloc void*[1];args[0]=(void*)type;
   var array=Keep(U.Invoke(U.MethodTyped(U.Class("Object"),"FindObjectsOfType","Type"),IntPtr.Zero,args));
   int count=checked((int)IL2CPP.il2cpp_array_length(array));
   for(int i=0;i<count;i++)
   {
    var raycaster=System.Runtime.InteropServices.Marshal.ReadIntPtr(array,32+i*IntPtr.Size);
    if(raycaster==own||!U.Alive(raycaster)||U.Get<byte>(raycaster,"enabled")==0)continue;
    blockedRaycasters.Add(Keep(raycaster));U.Set(raycaster,"enabled",(byte)0);
   }
  }
 }
 void CloseButton(IntPtr panel,IntPtr searchRoot)
 {
  var native=FindNativeClose();
  IntPtr rect,image,button;
  if(native!=IntPtr.Zero)
  {
   var go=Keep(N.Instantiate(U.GO(native)));U.ActiveSelf(go,false);
   rect=U.Transform(go);N.Parent(rect,panel);
   U.Set(rect,"anchorMin",new V2(.5f,.5f));U.Set(rect,"anchorMax",new V2(.5f,.5f));
   U.Set(rect,"pivot",new V2(.5f,.5f));U.Set(rect,"anchoredPosition",new V2(535,345));
   U.Set(rect,"localScale",new V3(.65f,.65f,1));
   foreach(var child in U.Children(rect))
    U.ActiveSelf(U.GO(child),!U.Name(child).Contains("Hover")&&!U.Name(child).Contains("Focus"));
   image=U.Component(go,U.Class("Image","UnityEngine.UI","UnityEngine.UI.dll"));
   button=Keep(U.Component(go,U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),true));
   U.ActiveSelf(go,true);
  }
  else
  {
   rect=Rect("Close Combat Caps",panel,535,345,75,75);image=Image(rect);
   Text(rect,"X",0,0,65,65,36);
   button=Keep(U.Component(U.GO(rect),U.Class("Button","UnityEngine.UI","UnityEngine.UI.dll"),true));
   U.ObjectArg(button,"set_targetGraphic",image);
  }
  U.Set(button,"interactable",(byte)1);actions[button]=close;
  if(image!=IntPtr.Zero)buttonImages[button]=image;
 }
 IntPtr FindNativeClose()
 {
  var klass=U.Class("Canvas","UnityEngine","UnityEngine.UIModule.dll");
  var type=IL2CPP.il2cpp_type_get_object(IL2CPP.il2cpp_class_get_type(klass));
  unsafe
  {
   byte inactive=1;void** args=stackalloc void*[2];args[0]=(void*)type;args[1]=&inactive;
   var array=Keep(U.Invoke(U.Method(U.Class("Object"),"FindObjectsOfType",2),IntPtr.Zero,args));
   for(int i=0;i<(int)IL2CPP.il2cpp_array_length(array);i++)
   {
    var found=U.Find(U.Transform(System.Runtime.InteropServices.Marshal.ReadIntPtr(array,32+i*IntPtr.Size)),"[Button]CloseButton",16);
    if(found!=IntPtr.Zero)return found;
   }
  }
  return IntPtr.Zero;
 }
 public void Hover(IntPtr button,bool on){if(buttonImages.TryGetValue(button,out var image))U.Set(image,"color",on?new Color(1,.72f,.3f,1):new Color(1,1,1,1));}
 public bool Click(IntPtr button){if(!actions.TryGetValue(button,out var action))return false;action();return true;}
 public void Dispose(){foreach(var raycaster in blockedRaycasters)if(U.Alive(raycaster))U.Set(raycaster,"enabled",(byte)1);blockedRaycasters.Clear();if(U.Alive(root)){U.ActiveSelf(root,false);N.DestroyImmediate(root);}foreach(var material in materials)if(U.Alive(material))N.DestroyImmediate(material);materials.Clear();foreach(var handle in handles)IL2CPP.il2cpp_gchandle_free(handle);handles.Clear();actions.Clear();buttonImages.Clear();}
}
