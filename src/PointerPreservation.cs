using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskShift {
    // Observe the current user's scheme, not an old undo scheme. No mouse-speed writes.
    internal sealed class PointerPreservation : IDisposable {
        const string PathName=@"Control Panel\Cursors";
        readonly RegistryValue[] before;readonly uint inputTime;
        internal PointerPreservation(){before=Read();inputTime=LastInput();}
        static uint LastInput(){var data=new LASTINPUTINFO{size=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO))};return GetLastInputInfo(ref data)?data.time:UInt32.MaxValue;}
        internal static RegistryValue[] Read(){var result=new List<RegistryValue>();using(var key=Registry.CurrentUser.OpenSubKey(PathName))if(key!=null)foreach(string name in key.GetValueNames()){object value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);result.Add(new RegistryValue{Path=PathName,Name=name,Exists=true,Kind=key.GetValueKind(name).ToString(),Data=value is byte[]?Convert.ToBase64String((byte[])value):Library.Json.Serialize(value)});}return result.ToArray();}
        internal static RegistryValue[] RestorationPlan(RegistryValue[] original,RegistryValue[] current){var changes=new List<RegistryValue>();foreach(string name in original.Concat(current).Select(v=>v.Name).Distinct(StringComparer.OrdinalIgnoreCase)){var a=original.FirstOrDefault(v=>String.Equals(v.Name,name,StringComparison.OrdinalIgnoreCase));var b=current.FirstOrDefault(v=>String.Equals(v.Name,name,StringComparison.OrdinalIgnoreCase));if(a==null)changes.Add(new RegistryValue{Path=PathName,Name=name,Exists=false});else if(b==null||a.Kind!=b.Kind||a.Data!=b.Data)changes.Add(a);}return changes.ToArray();}
        public void Dispose(){if(inputTime==UInt32.MaxValue||LastInput()!=inputTime)return;var changes=RestorationPlan(before,Read());if(changes.Length==0)return;using(var key=Registry.CurrentUser.CreateSubKey(PathName))foreach(var change in changes){if(!change.Exists){key.DeleteValue(change.Name,false);continue;}var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),change.Kind);object value=kind==RegistryValueKind.Binary?(object)Convert.FromBase64String(change.Data):kind==RegistryValueKind.DWord?(object)Library.Json.Deserialize<int>(change.Data):kind==RegistryValueKind.QWord?(object)Library.Json.Deserialize<long>(change.Data):kind==RegistryValueKind.MultiString?(object)Library.Json.Deserialize<string[]>(change.Data):(object)Library.Json.Deserialize<string>(change.Data);key.SetValue(change.Name,value,kind);}if(!ReloadCursors(0x57,0,IntPtr.Zero,0))throw new InvalidOperationException("自定义指针设置已保留，但 Windows 指针刷新失败。");}
        static readonly Dictionary<string,Cursor> controlCursors=new Dictionary<string,Cursor>(StringComparer.OrdinalIgnoreCase);
        internal static Cursor ForControl(string name,Cursor fallback){try{string file;using(var key=Registry.CurrentUser.OpenSubKey(PathName))file=key==null?null:key.GetValue(name) as string;if(String.IsNullOrEmpty(file))return fallback;file=Environment.ExpandEnvironmentVariables(file);if(!File.Exists(file))return fallback;string cache=file+"|"+File.GetLastWriteTimeUtc(file).Ticks;Cursor cursor;if(controlCursors.TryGetValue(cache,out cursor))return cursor;IntPtr handle=LoadCursorFromFile(file);if(handle==IntPtr.Zero)return fallback;cursor=new Cursor(handle);controlCursors[cache]=cursor;return cursor;}catch{return fallback;}}
        [StructLayout(LayoutKind.Sequential)]struct LASTINPUTINFO{public uint size,time;}
        [DllImport("user32.dll")]static extern bool GetLastInputInfo(ref LASTINPUTINFO value);
        [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]static extern bool ReloadCursors(uint action,uint param,IntPtr value,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadCursorFromFile(string file);
    }
}
