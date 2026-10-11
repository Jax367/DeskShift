using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskShift {
    internal static class LaunchIntegration {
        const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static string StartupCommand(string executable) {return "\""+Path.GetFullPath(executable)+"\" --startup";}
        internal static bool StartupEnabled(string executable) {
            using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&String.Equals(key.GetValue("DeskShift") as string,StartupCommand(executable),StringComparison.OrdinalIgnoreCase);
        }
        internal static void SetStartup(string executable,bool enabled) {
            if(enabled&&!File.Exists(executable))throw new FileNotFoundException("找不到 DeskShift 程序。",executable);
            using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
                if(enabled)key.SetValue("DeskShift",StartupCommand(executable),RegistryValueKind.String);
                else key.DeleteValue("DeskShift",false);
            }
        }
        internal static string CreateDesktopShortcut(string executable) {
            executable=Path.GetFullPath(executable);if(!File.Exists(executable))throw new FileNotFoundException("找不到 DeskShift 程序。",executable);
            string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DeskShift.lnk");
            object shell=null,shortcut=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
                shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{target});
                Action<string,object> set=(name,value)=>shortcut.GetType().InvokeMember(name,BindingFlags.SetProperty,null,shortcut,new[]{value});
                set("TargetPath",executable);set("WorkingDirectory",Path.GetDirectoryName(executable));set("IconLocation",executable+",0");set("Description","DeskShift · 桌面随心换");
                shortcut.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);return target;
            }finally{if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut);if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
        }
    }
}
