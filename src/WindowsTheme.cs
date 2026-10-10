using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using System.Threading;

namespace DeskShift {
    public class RegistryValue { public string Path {get;set;} public string Name {get;set;} public bool Exists {get;set;} public string Kind {get;set;} public string Data {get;set;} }
    public class Snapshot { public string Wallpaper {get;set;} public string ActiveId {get;set;} public Palette ActivePalette {get;set;} public List<RegistryValue> Values {get;set;} }
    public class PreparedTheme {public string Bitmap;public string ThemeFile;public Palette Palette;public bool SystemTheme;}
    public static class WindowsTheme {
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SystemParametersInfo(int action,int param,string value,int flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SystemParametersInfo(int action,int param,StringBuilder value,int flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool SystemParametersInfo(int action,int param,out bool value,int flags);
        public static bool AnimationsEnabled {get{bool enabled;return !SystemParametersInfo(0x1042,0,out enabled,0) || enabled;}}
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wParam,string lParam,uint flags,uint timeout,out IntPtr result);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        static string Personalize=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        static string Dwm=@"Software\Microsoft\Windows\DWM";
        static string Accent=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
        static string Desktop=@"Control Panel\Desktop";
        static readonly string[][] Keys={new[]{Personalize,"AppsUseLightTheme"},new[]{Personalize,"SystemUsesLightTheme"},new[]{Personalize,"ColorPrevalence"},new[]{Dwm,"ColorizationColor"},new[]{Dwm,"AccentColor"},new[]{Dwm,"ColorPrevalence"},new[]{Accent,"AccentColorMenu"},new[]{Accent,"StartColorMenu"},new[]{Accent,"AccentPalette"},new[]{Desktop,"WallpaperStyle"},new[]{Desktop,"TileWallpaper"}};
        public static Snapshot Capture(string activeId) {
            var wallpaper=new StringBuilder(32768);if(!SystemParametersInfo(0x73,wallpaper.Capacity,wallpaper,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var s=new Snapshot{Wallpaper=wallpaper.ToString(),ActiveId=activeId,Values=new List<RegistryValue>()};
            foreach(var pair in Keys) using(var key=Registry.CurrentUser.OpenSubKey(pair[0])) {
                object value=key==null?null:key.GetValue(pair[1]);var v=new RegistryValue{Path=pair[0],Name=pair[1],Exists=value!=null};
                if(v.Exists) { v.Kind=key.GetValueKind(pair[1]).ToString();v.Data=value is byte[]?Convert.ToBase64String((byte[])value):Library.Json.Serialize(value); }s.Values.Add(v);
            }
            return s;
        }
        static void Set(string path,string name,object value) { using(var key=Registry.CurrentUser.CreateSubKey(path)) key.SetValue(name,value,value is byte[]?RegistryValueKind.Binary:value is string?RegistryValueKind.String:RegistryValueKind.DWord); }
        static void Wallpaper(string file) { if(!SystemParametersInfo(20,0,file,3)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        static void Broadcast() { IntPtr result;SendMessageTimeout(new IntPtr(0xffff),0x1a,IntPtr.Zero,"ImmersiveColorSet",2,1000,out result);SendMessageTimeout(new IntPtr(0xffff),0x320,IntPtr.Zero,null,2,1000,out result); }
        public static string ThemeText(string file,Palette p) {
            return "[Theme]\r\nDisplayName=DeskShift\r\n[Control Panel\\Desktop]\r\nWallpaper="+file+"\r\nWallpaperStyle=10\r\nTileWallpaper=0\r\n[VisualStyles]\r\nPath=%ResourceDir%\\Themes\\Aero\\Aero.msstyles\r\nColorStyle=NormalColor\r\nSize=NormalSize\r\nAutoColorization=0\r\nColorizationColor=0XFF"+p.Accent.Substring(1)+"\r\nAppMode="+(p.Dark?"Dark":"Light")+"\r\nSystemMode="+(p.Dark?"Dark":"Light")+"\r\n[MasterThemeSelector]\r\nMTSM=DABJDKT\r\n";
        }
        public static string PrepareWallpaper(string root,Wallpaper w) {
            // Preserve decoded pixels and original dimensions. Never use the old resized cache.
            string path=Path.Combine(root,"desktop-original-v3-"+w.Id+".bmp");
            if(System.IO.File.Exists(path)) return path;
            string temp=path+".tmp";
            try {
                using(var img=Image.FromFile(w.File)) img.Save(temp,System.Drawing.Imaging.ImageFormat.Bmp);
                System.IO.File.Move(temp,path);return path;
            }finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}
        }
        public static PreparedTheme Prepare(Library library,Wallpaper w,Palette p,bool system) {
            string bmp=PrepareWallpaper(library.Root,w),theme=null;
            if(system){theme=Path.Combine(library.Root,"prepared-"+w.Id+"-"+p.Accent.Substring(1)+"-"+(p.Dark?"dark":"light")+".theme");if(!System.IO.File.Exists(theme))System.IO.File.WriteAllText(theme,ThemeText(bmp,p),Encoding.Unicode);}
            // Warm the filesystem cache before the full-screen animation begins.
            using(var stream=new FileStream(bmp,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.SequentialScan)){byte[] buffer=new byte[1024*1024];while(stream.Read(buffer,0,buffer.Length)>0){}}
            return new PreparedTheme{Bitmap=bmp,ThemeFile=theme,Palette=p,SystemTheme=system};
        }
        public static void WaitForRefresh(string expectedWallpaper) {
            // No public API can certify that every other application's theme has rendered.
            // Wait for the wallpaper setting to remain stable, then synchronize our DWM work.
            var deadline=Stopwatch.StartNew();long stableSince=-1;
            while(deadline.ElapsedMilliseconds<2500){var current=new StringBuilder(32768);bool matches=SystemParametersInfo(0x73,current.Capacity,current,0) && String.Equals(current.ToString(),expectedWallpaper,StringComparison.OrdinalIgnoreCase);if(matches){if(stableSince<0)stableSince=deadline.ElapsedMilliseconds;if(deadline.ElapsedMilliseconds-stableSince>=450)break;}else stableSince=-1;Thread.Sleep(60);}
            try{DwmFlush();Thread.Sleep(80);DwmFlush();}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        public static void Apply(Library library,PreparedTheme prepared,Action<string> progress) {
            string bmp=prepared.Bitmap;Palette p=prepared.Palette;bool system=prepared.SystemTheme;
            progress("正在保存切换前的设置…");
            var snapshot=Capture(library.State.ActiveId);
            snapshot.ActivePalette=library.State.ActivePalette;
            Library.AtomicWrite(Path.Combine(library.Root,"undo.json"),Library.Json.Serialize(snapshot));
            try {
                progress("正在切换桌面壁纸…");
                Set(Desktop,"WallpaperStyle","10");Set(Desktop,"TileWallpaper","0");Wallpaper(bmp);
                if(system) {
                    progress("正在应用 Windows 配色…");
                    Color a=Palette.ColorOf(p.Accent);int argb=unchecked((int)(0xff000000u|((uint)a.R<<16)|((uint)a.G<<8)|a.B));int abgr=unchecked((int)(0xff000000u|((uint)a.B<<16)|((uint)a.G<<8)|a.R));
                    Set(Personalize,"AppsUseLightTheme",p.Dark?0:1);Set(Personalize,"SystemUsesLightTheme",p.Dark?0:1);Set(Personalize,"ColorPrevalence",p.Dark?1:0);
                    Set(Dwm,"ColorizationColor",argb);Set(Dwm,"AccentColor",abgr);Set(Dwm,"ColorPrevalence",1);Set(Accent,"AccentColorMenu",abgr);Set(Accent,"StartColorMenu",abgr);
                    byte[] ramp=new byte[32];for(int i=0;i<8;i++) { Color c=Palette.Mix(a,i<3?Color.White:Color.Black,i<3?(3-i)*.16:(i-3)*.14);ramp[i*4]=c.R;ramp[i*4+1]=c.G;ramp[i*4+2]=c.B;ramp[i*4+3]=0xff; }Set(Accent,"AccentPalette",ramp);
                    // Let Windows' theme engine refresh Explorer/DWM instead of undocumented DWM APIs.
                    Process.Start(new ProcessStartInfo(prepared.ThemeFile){UseShellExecute=true});
                }
                progress("正在通知系统刷新…");Broadcast();progress("正在等待桌面刷新稳定…");WaitForRefresh(bmp);
            } catch(Exception e) {
                try { RestoreSnapshot(snapshot); } catch(Exception rollback) { throw new Exception(e.Message+"；回退失败："+rollback.Message); }throw;
            }
        }
        public static void RestoreSnapshot(Snapshot s) {
            foreach(var v in s.Values) using(var key=Registry.CurrentUser.CreateSubKey(v.Path)) {
                if(!v.Exists) { key.DeleteValue(v.Name,false);continue; }
                var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),v.Kind);object value;
                if(kind==RegistryValueKind.Binary) value=Convert.FromBase64String(v.Data);
                else if(kind==RegistryValueKind.DWord) value=Library.Json.Deserialize<int>(v.Data);
                else if(kind==RegistryValueKind.QWord) value=Library.Json.Deserialize<long>(v.Data);
                else if(kind==RegistryValueKind.MultiString) value=Library.Json.Deserialize<string[]>(v.Data);
                else value=Library.Json.Deserialize<string>(v.Data);
                key.SetValue(v.Name,value,kind);
            }
            Wallpaper(s.Wallpaper);Broadcast();WaitForRefresh(s.Wallpaper);
        }
        public static void Undo(Library library) {
            string file=Path.Combine(library.Root,"undo.json");if(!System.IO.File.Exists(file)) throw new Exception("还没有可以撤销的切换。");
            var s=Library.Json.Deserialize<Snapshot>(System.IO.File.ReadAllText(file));RestoreSnapshot(s);library.State.ActiveId=s.ActiveId;library.State.ActivePalette=s.ActivePalette;library.Save();System.IO.File.Delete(file);
        }
    }
}
