using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace DeskShift {
    // Experimental, bounded UI automation. No debugging port, clipboard or input hooks.
    internal sealed class BrowserThemeLoader {
        readonly CancellationToken cancellation;readonly Func<bool> covered;readonly Stopwatch clock=Stopwatch.StartNew();
        IntPtr window,dialog,previous;uint inputStamp;bool started,ownTab;string scheme,profile,folder;
        internal BrowserThemeLoader(CancellationToken token,Func<bool> isCovered){cancellation=token;covered=isCovered;}
        internal static string Snapshot(string exported,string root) {
            string manifest=File.ReadAllText(Path.Combine(exported,"manifest.json"));ValidateManifest(manifest);
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-","");
            string target=Path.Combine(root,"browser-themes","auto",hash);Directory.CreateDirectory(target);Library.AtomicWrite(Path.Combine(target,"manifest.json"),manifest);return target;
        }
        internal static void ValidateManifest(string text) {
            var data=Library.Json.Deserialize<Dictionary<string,object>>(text);
            var allowed=new[]{"manifest_version","name","version","description","theme"};
            if(data==null||data.Keys.Any(k=>!allowed.Contains(k))||!data.ContainsKey("theme"))throw new InvalidOperationException("自动加载只接受纯配色主题。");
            var theme=data["theme"] as Dictionary<string,object>;
            if(theme==null||theme.Count!=1||!theme.ContainsKey("colors"))throw new InvalidOperationException("主题中含有配色以外的配置。");
            var colors=theme["colors"] as Dictionary<string,object>;
            var names=new[]{"frame","frame_inactive","toolbar","tab_text","tab_background_text","tab_background_text_inactive","background_tab","background_tab_inactive","bookmark_text","omnibox_background","omnibox_text"};
            if(colors==null||colors.Count==0||colors.Keys.Any(k=>!names.Contains(k)))throw new InvalidOperationException("主题颜色字段无效。");
            foreach(var value in colors.Values){var values=value as System.Collections.IEnumerable;if(values==null||value is string)throw new InvalidOperationException("主题颜色无效。");var numbers=values.Cast<object>().ToArray();if(numbers.Length!=3||numbers.Any(v=>!(v is int)||(int)v<0||(int)v>255))throw new InvalidOperationException("主题 RGB 无效。");}
        }
        internal string Run(string browser,string themeFolder,string testProfile=null,IntPtr testWindow=default(IntPtr)) {
            try {
                if(cancellation.IsCancellationRequested||!covered())return "跳过：没有完整覆盖或操作已取消。";
                if(browser!="edge"&&browser!="chrome")throw new InvalidOperationException("请选择 Chrome 或 Edge。");
                folder=Path.GetFullPath(themeFolder);ValidateManifest(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                scheme=browser;profile=testProfile??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),browser=="edge"?"Microsoft\\Edge\\User Data\\Default":"Google\\Chrome\\User Data\\Default");
                previous=GetForegroundWindow();window=testWindow!=IntPtr.Zero?testWindow:FindBrowser(browser);
                if(window==IntPtr.Zero)return "跳过：浏览器未打开，或存在多个窗口。";
                inputStamp=LastInput();started=true;Check(false);
                if(GetForegroundWindow()!=window&&!SetForegroundWindow(window))throw new InvalidOperationException("无法取得浏览器焦点。");
                Check(true);Chord(0x54);ownTab=true;Navigate(scheme+"://version");
                Wait(()=>FindProfile(),"无法确认当前窗口使用默认浏览器配置。");
                Navigate(scheme+"://extensions");Wait(()=>LoadButton()!=null,"扩展页尚未就绪，或开发者模式未打开。");
                Check(true);ValidateManifest(File.ReadAllText(Path.Combine(folder,"manifest.json")));Invoke(LoadButton());
                Wait(()=>FindDialog()!=IntPtr.Zero,"未找到属于目标浏览器的文件夹选择框。");
                var edit=FindFolderEdit();if(edit==null)throw new InvalidOperationException("无法可靠识别文件夹输入框。");
                Check(true);object pattern;if(!edit.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw new InvalidOperationException("文件夹输入框不可直接写入。");((ValuePattern)pattern).SetValue(folder);
                Check(true);var select=FindButton(AutomationElement.FromHandle(dialog),new[]{"选择文件夹","Select Folder","选择","Select"});if(select==null)throw new InvalidOperationException("无法识别文件夹确认按钮。");
                ValidateManifest(File.ReadAllText(Path.Combine(folder,"manifest.json")));Invoke(select);
                Wait(()=>ThemeMatches(),"主题加载未得到确认。");dialog=IntPtr.Zero;
                Check(true);if(Address()!=scheme+"://extensions")throw new InvalidOperationException("页面变化，停止自动操作。");Chord(0x57);ownTab=false;
                if(previous!=IntPtr.Zero&&IsWindow(previous))SetForegroundWindow(previous);
                return "标题栏主题已自动加载";
            } catch(Exception ex) {
                // Cleanup only while guards still pass. Never inject keys after cancellation/user input.
                try{Check(true);if(dialog!=IntPtr.Zero&&GetForegroundWindow()==dialog){Key(0x1B);dialog=IntPtr.Zero;}if(ownTab&&GetForegroundWindow()==window&&(Address()==scheme+"://extensions"||Address()==scheme+"://version")){Chord(0x57);ownTab=false;}if(previous!=IntPtr.Zero&&IsWindow(previous))SetForegroundWindow(previous);}catch{}
                return "自动加载停止："+(ex is OperationCanceledException?"操作取消或超时":ex.Message);
            }
        }
        void Check(bool requireFocus) {
            cancellation.ThrowIfCancellationRequested();if(clock.ElapsedMilliseconds>6500||!covered())throw new OperationCanceledException();
            if(started&&LastInput()!=inputStamp)throw new InvalidOperationException("检测到用户操作，已停止。");
            if(!IsWindow(window))throw new InvalidOperationException("浏览器窗口已关闭。");
            if(requireFocus){var current=GetForegroundWindow();if(current!=window&&(dialog==IntPtr.Zero||current!=dialog))throw new InvalidOperationException("浏览器焦点发生变化。");}
        }
        void Wait(Func<bool> condition,string message){while(true){Check(false);if(condition())return;if(clock.ElapsedMilliseconds>6000)throw new InvalidOperationException(message);Thread.Sleep(80);}}
        void Navigate(string address){Check(true);Chord(0x4C);var focused=AutomationElement.FocusedElement;if(focused==null||focused.Current.ControlType!=ControlType.Edit)throw new InvalidOperationException("地址栏未获得焦点。");Check(true);object pattern;if(!focused.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw new InvalidOperationException("地址栏不支持安全写入。");((ValuePattern)pattern).SetValue(address);Check(true);Key(0x0D);Wait(()=>Address()==address,"未确认浏览器内部页面。");}
        string Address(){var edits=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));foreach(AutomationElement element in edits){object value;if(element.TryGetCurrentPattern(ValuePattern.Pattern,out value)){string address=((ValuePattern)value).Current.Value.TrimEnd('/');if(address==scheme+"://version"||address==scheme+"://extensions")return address;}}return "";}
        bool FindProfile(){var all=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,Condition.TrueCondition);foreach(AutomationElement element in all){if(String.Equals(element.Current.Name,profile,StringComparison.OrdinalIgnoreCase))return true;object p;if(element.TryGetCurrentPattern(ValuePattern.Pattern,out p)&&String.Equals(((ValuePattern)p).Current.Value,profile,StringComparison.OrdinalIgnoreCase))return true;}return false;}
        AutomationElement LoadButton(){if(Address()!=scheme+"://extensions")return null;return FindButton(AutomationElement.FromHandle(window),new[]{"加载解压缩的扩展","加载已解压的扩展程序","加载解压缩的扩展程序","Load unpacked"});}
        static AutomationElement FindButton(AutomationElement root,string[] names){var elements=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));foreach(AutomationElement item in elements)if(names.Contains(item.Current.Name)&&item.Current.IsEnabled)return item;return null;}
        AutomationElement FindFolderEdit(){var edits=AutomationElement.FromHandle(dialog).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));foreach(AutomationElement item in edits)if(new[]{"文件夹:","文件夹：","Folder:","文件夹","Folder"}.Contains(item.Current.Name))return item;return null;}
        IntPtr FindDialog(){uint browserPid;GetWindowThreadProcessId(window,out browserPid);IntPtr found=IntPtr.Zero;EnumWindows((handle,arg)=>{uint pid;GetWindowThreadProcessId(handle,out pid);var cls=new StringBuilder(100);GetClassName(handle,cls,100);if(pid==browserPid&&cls.ToString()=="#32770"&&IsWindowVisible(handle))found=handle;return true;},IntPtr.Zero);if(found!=IntPtr.Zero)dialog=found;return found;}
        bool ThemeMatches(){try{var prefs=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(profile,"Preferences")));var extensions=prefs["extensions"] as Dictionary<string,object>;var theme=extensions["theme"] as Dictionary<string,object>;return String.Equals(Path.GetFullPath((string)theme["pack"]),folder,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        static IntPtr FindBrowser(string browser){var matches=new List<IntPtr>();string process=browser=="edge"?"msedge":"chrome",relative=browser=="edge"?"Microsoft\\Edge\\Application\\msedge.exe":"Google\\Chrome\\Application\\chrome.exe";var trusted=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}.Select(root=>Path.Combine(root,relative)).ToArray();EnumWindows((handle,arg)=>{if(!IsWindowVisible(handle))return true;var cls=new StringBuilder(100);GetClassName(handle,cls,100);if(cls.ToString()!="Chrome_WidgetWin_1")return true;uint pid;GetWindowThreadProcessId(handle,out pid);try{var p=Process.GetProcessById((int)pid);if(p.ProcessName==process&&trusted.Any(path=>String.Equals(path,p.MainModule.FileName,StringComparison.OrdinalIgnoreCase)))matches.Add(handle);}catch{}return true;},IntPtr.Zero);return matches.Count==1?matches[0]:IntPtr.Zero;}
        void Invoke(AutomationElement element){Check(true);object pattern;if(element==null||!element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))throw new InvalidOperationException("控件不支持安全调用。");((InvokePattern)pattern).Invoke();}
        void Chord(ushort key){Send(new[]{Stroke(0x11,false,false),Stroke(key,false,false),Stroke(key,true,false),Stroke(0x11,true,false)});}
        void Key(ushort key){Send(new[]{Stroke(key,false,false),Stroke(key,true,false)});}
        void Send(INPUT[] events){Check(true);if(SendInput((uint)events.Length,events,Marshal.SizeOf(typeof(INPUT)))!=events.Length)throw new InvalidOperationException("按键发送失败。");Thread.Sleep(30);inputStamp=LastInput();}
        internal static void Test() {
            string root=Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-"+DateTime.UtcNow.Ticks),profile=Path.Combine(root,"profile","Default"),theme=Path.Combine(root,"theme");Directory.CreateDirectory(profile);Directory.CreateDirectory(theme);
            File.WriteAllText(Path.Combine(profile,"Preferences"),"{\"extensions\":{\"ui\":{\"developer_mode\":true}}}");
            File.WriteAllText(Path.Combine(theme,"manifest.json"),"{\"manifest_version\":3,\"name\":\"DeskShift isolated color test\",\"version\":\"1.0\",\"theme\":{\"colors\":{\"frame\":[131,190,178],\"toolbar\":[235,246,242]}}}");
            string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe");
            var process=Process.Start(new ProcessStartInfo(executable,"--no-first-run --no-default-browser-check --user-data-dir=\""+Path.GetDirectoryName(profile)+"\" --new-window edge://extensions"){UseShellExecute=false});
            string result="";try{IntPtr handle=IntPtr.Zero;for(int i=0;i<80&&handle==IntPtr.Zero;i++){Thread.Sleep(100);process.Refresh();handle=process.MainWindowHandle;}if(handle==IntPtr.Zero)throw new Exception("独立测试浏览器没有出现窗口。");Thread.Sleep(800);
                SetForegroundWindow(handle);
                using(var mask=new DesktopTransition(new Palette{Accent="#83BEB2",Background="#F7FBFA",Text="#222734"},"对开幕帘")) {
                    var task=mask.CoverAsync();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();
                    var runner=System.Threading.Tasks.Task.Run(()=>new BrowserThemeLoader(CancellationToken.None,()=>mask.Covered).Run("edge",theme,profile,handle));
                    var time=Stopwatch.StartNew();while(!runner.IsCompleted&&time.ElapsedMilliseconds<7500){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}result=runner.IsCompleted?runner.Result:"测试超时";
                    task=mask.RevealAsync();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}
                }
                File.WriteAllText(Path.Combine(root,"result.txt"),result);File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-result.txt"),root+"\r\n"+result);
            }catch(Exception ex){File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-result.txt"),ex.ToString());}
            finally{try{if(!process.HasExited){process.CloseMainWindow();if(!process.WaitForExit(1200))process.Kill();}}catch{}}
        }
        static INPUT Stroke(ushort key,bool up,bool unicode){return new INPUT{type=1,data=new INPUTUNION{keyboard=new KEYBDINPUT{vk=key,flags=(uint)(up?2:0)}}};}
        static uint LastInput(){var value=new LASTINPUTINFO{size=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO))};GetLastInputInfo(ref value);return value.time;}
        [StructLayout(LayoutKind.Sequential)]struct LASTINPUTINFO{public uint size,time;}
        [StructLayout(LayoutKind.Sequential)]struct INPUT{public uint type;public INPUTUNION data;}
        [StructLayout(LayoutKind.Explicit)]struct INPUTUNION{[FieldOffset(0)]public KEYBDINPUT keyboard;[FieldOffset(0)]public MOUSEINPUT mouse;}
        [StructLayout(LayoutKind.Sequential)]struct KEYBDINPUT{public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
        [StructLayout(LayoutKind.Sequential)]struct MOUSEINPUT{public int x,y;public uint data,flags,time;public UIntPtr extra;}
        delegate bool EnumWindow(IntPtr window,IntPtr param);
        [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindow callback,IntPtr param);
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int maximum);
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")]static extern uint SendInput(uint count,INPUT[] inputs,int size);
        [DllImport("user32.dll")]static extern bool GetLastInputInfo(ref LASTINPUTINFO value);
    }
}
