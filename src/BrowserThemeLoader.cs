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
using System.Text.RegularExpressions;

namespace DeskShift {
    // Experimental, bounded UI automation. No debugging port, clipboard or input hooks.
    internal sealed class BrowserThemeLoader {
        readonly CancellationToken cancellation;readonly Func<bool> covered;readonly Stopwatch clock=Stopwatch.StartNew();
        IntPtr window,dialog,previous;uint inputStamp,browserPid,browserThread;bool started,ownTab,confirmingTheme,awaitingDialog;string scheme,profile,folder;AutomationElement ownedTab;
        readonly int limit;internal bool StopBatch {get;private set;}
        internal BrowserThemeLoader(CancellationToken token,Func<bool> isCovered,int timeout=6500){cancellation=token;covered=isCovered;limit=timeout;}
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
        internal sealed class ManifestLease : IDisposable {
            readonly FileStream stream;readonly string expected;
            internal ManifestLease(string directory) {
                directory=Path.GetFullPath(directory);
                for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)if((parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("主题目录不能使用链接或重定向。");
                string path=Path.Combine(directory,"manifest.json");if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("主题文件不能使用链接。");
                stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                try {if(stream.Length>16384)throw new InvalidOperationException("主题文件过大。");expected=Read();ValidateManifest(expected);string name=Path.GetFileName(directory);if(Regex.IsMatch(name,"^[A-Fa-f0-9]{64}$")){using(var sha=SHA256.Create()){string digest=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(expected))).Replace("-","");if(!String.Equals(name,digest,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("主题快照摘要不匹配。");}}Verify();}catch{stream.Dispose();throw;}
            }
            string Read(){stream.Position=0;using(var reader=new StreamReader(stream,Encoding.UTF8,true,1024,true))return reader.ReadToEnd();}
            internal void Verify(){if(Read()!=expected)throw new InvalidOperationException("主题文件已变化，停止加载。");ValidateManifest(expected);}
            public void Dispose(){stream.Dispose();}
        }
        internal string Run(string browser,string themeFolder,string testProfile=null,IntPtr testWindow=default(IntPtr)) {
            ManifestLease lease=null;
            try {
                if(cancellation.IsCancellationRequested||!covered())return "跳过：没有完整覆盖或操作已取消。";
                if(browser!="edge"&&browser!="chrome")throw new InvalidOperationException("请选择 Chrome 或 Edge。");
                folder=Path.GetFullPath(themeFolder);lease=new ManifestLease(folder);
                scheme=browser;profile=testProfile??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),browser=="edge"?"Microsoft\\Edge\\User Data\\Default":"Google\\Chrome\\User Data\\Default");
                previous=GetForegroundWindow();window=testWindow!=IntPtr.Zero?testWindow:FindBrowser(browser);
                if(window==IntPtr.Zero)return "跳过：浏览器未打开，或存在多个窗口。";
                browserThread=GetWindowThreadProcessId(window,out browserPid);
                inputStamp=LastInput();started=true;Check(false);
                if(GetForegroundWindow()!=window&&!SetForegroundWindow(window))throw new InvalidOperationException("无法取得浏览器焦点。");
                Check(true);OpenTemporaryTab();Navigate(scheme+"://version");
                Wait(()=>FindProfile(),"无法确认当前窗口使用默认浏览器配置。");
                Navigate(scheme+"://extensions");Wait(()=>LoadButton()!=null,"扩展页尚未就绪，或开发者模式未打开。");
                Check(true);lease.Verify();awaitingDialog=true;Invoke(LoadButton());
                Wait(()=>FindDialog()!=IntPtr.Zero,"未找到属于目标浏览器的文件夹选择框。");
                awaitingDialog=false;
                var edit=FindFolderEdit();if(edit==null)throw new InvalidOperationException("无法可靠识别文件夹输入框。");
                Check(true);object pattern;if(!edit.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw new InvalidOperationException("文件夹输入框不可直接写入。");((ValuePattern)pattern).SetValue(folder);Check(true);
                Check(true);var select=FindButton(AutomationElement.FromHandle(dialog),new[]{"选择文件夹","Select Folder","选择","Select"});if(select==null)throw new InvalidOperationException("无法识别文件夹确认按钮。");
                lease.Verify();if(((ValuePattern)pattern).Current.Value!=folder)throw new InvalidOperationException("主题目录输入未得到确认。");confirmingTheme=true;Invoke(select);
                Wait(()=>ThemeMatches(),"主题加载未得到确认。");dialog=IntPtr.Zero;
                lease.Verify();Check(true);if(Address()!=scheme+"://extensions")throw new InvalidOperationException("页面变化，停止自动操作。");
                var close=FindButton(ownedTab,new[]{"关闭标签页","关闭","Close tab","Close"});if(close==null)throw new InvalidOperationException("无法确认临时标签页的关闭按钮。");Invoke(close);ownTab=false;ownedTab=null;
                if(previous!=IntPtr.Zero&&IsWindow(previous))SetForegroundWindow(previous);
                return "主题已登记并返回原页面（标题栏实际显示需确认）";
            } catch(Exception ex) {
                StopBatch=started||ex is OperationCanceledException||ownTab||dialog!=IntPtr.Zero;
                // Leave uncertain UI for the user. Never send cleanup keys after failure.
                return "自动加载停止："+(ex is OperationCanceledException?"操作取消或超时":ex.Message);
            }
            finally {if(lease!=null)lease.Dispose();}
        }
        void Check(bool requireFocus) {
            cancellation.ThrowIfCancellationRequested();if(clock.ElapsedMilliseconds>limit||!covered())throw new OperationCanceledException();
            if(started&&LastInput()!=inputStamp)throw new InvalidOperationException("检测到用户操作，已停止。");
            if(!IsWindow(window))throw new InvalidOperationException("浏览器窗口已关闭。");
            uint currentPid;if(GetWindowThreadProcessId(window,out currentPid)!=browserThread||currentPid!=browserPid)throw new InvalidOperationException("浏览器窗口身份变化。");
            if(awaitingDialog&&dialog==IntPtr.Zero)FindDialog();
            if(dialog!=IntPtr.Zero){if(!IsWindow(dialog)&&confirmingTheme)dialog=IntPtr.Zero;else if(!IsWindow(dialog)||!IsOwnedDialog(dialog))throw new InvalidOperationException("文件夹选择框归属变化。");}
            if(dialog==IntPtr.Zero&&ownTab&&ownedTab!=null&&!Same(SelectedTab(),ownedTab))throw new InvalidOperationException("当前标签页变化，停止自动操作。");
            if(requireFocus){var current=GetForegroundWindow();if(current!=window&&(dialog==IntPtr.Zero||current!=dialog))throw new InvalidOperationException("浏览器焦点发生变化。");}
        }
        void Wait(Func<bool> condition,string message){while(true){Check(false);if(condition())return;if(clock.ElapsedMilliseconds>limit-500)throw new InvalidOperationException(message);Thread.Sleep(80);}}
        static bool Same(AutomationElement first,AutomationElement second){return first!=null&&second!=null&&first.GetRuntimeId().SequenceEqual(second.GetRuntimeId());}
        AutomationElement SelectedTab(){var items=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem));var selected=new List<AutomationElement>();foreach(AutomationElement item in items){object pattern;if(item.TryGetCurrentPattern(SelectionItemPattern.Pattern,out pattern)&&((SelectionItemPattern)pattern).Current.IsSelected)selected.Add(item);}return selected.Count==1?selected[0]:null;}
        void OpenTemporaryTab(){var root=AutomationElement.FromHandle(window);var old=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem)).Cast<AutomationElement>().Select(item=>String.Join(",",item.GetRuntimeId())).ToArray();var button=FindButton(root,new[]{"新建标签页","新标签页","New tab"});if(button==null||!IsBrowserChrome(button))throw new InvalidOperationException("无法确认新建标签页按钮。");Invoke(button);ownTab=true;Wait(()=>{var tab=SelectedTab();if(tab==null||old.Contains(String.Join(",",tab.GetRuntimeId())))return false;ownedTab=tab;return true;},"无法确认新建的临时标签页。");}
        bool IsBrowserChrome(AutomationElement element){for(int depth=0;element!=null&&depth<24;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)){if(element.Current.ControlType==ControlType.Document)return false;if(new IntPtr(element.Current.NativeWindowHandle)==window)return true;}return false;}
        AutomationElement AddressBar(){var edits=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));var matches=new List<AutomationElement>();foreach(AutomationElement item in edits)if(new[]{"地址和搜索栏","地址栏","搜索或输入网址","Address and search bar","Search or enter web address"}.Contains(item.Current.Name)&&item.Current.ProcessId==(int)browserPid&&IsBrowserChrome(item))matches.Add(item);return matches.Count==1?matches[0]:null;}
        void Navigate(string address){Check(true);var edit=AddressBar();if(edit==null)throw new InvalidOperationException("无法可靠确认浏览器地址栏。");edit.SetFocus();Check(true);if(!Same(AutomationElement.FocusedElement,edit))throw new InvalidOperationException("地址栏未获得焦点。");object pattern;if(!edit.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw new InvalidOperationException("地址栏不支持安全写入。");((ValuePattern)pattern).SetValue(address);Check(true);if(((ValuePattern)pattern).Current.Value!=address)throw new InvalidOperationException("地址栏输入发生变化。");EnterAddress(edit,address);Wait(()=>Address()==address&&DocumentHasURL(address),"未确认浏览器内部页面。");}
        internal static bool InternalDocumentTitle(string address,string title){if(address=="chrome://extensions"||address=="edge://extensions")return new[]{"扩展程序","扩展","Extensions"}.Contains(title);if(address=="chrome://version"||address=="edge://version")return new[]{"关于版本","版本","About Version","Version"}.Contains(title);return false;}
        bool DocumentHasURL(string address){if(Address()!=address)return false;var documents=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));foreach(AutomationElement document in documents){if(document.Current.IsOffscreen)continue;object pattern;if(document.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)){if(((ValuePattern)pattern).Current.Value.TrimEnd('/')==address)return true;continue;}if(InternalDocumentTitle(address,document.Current.Name))return true;}return false;}
        string Address(){var edit=AddressBar();object value;if(edit!=null&&edit.TryGetCurrentPattern(ValuePattern.Pattern,out value))return ((ValuePattern)value).Current.Value.TrimEnd('/');return "";}
        bool FindProfile(){var all=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,Condition.TrueCondition);foreach(AutomationElement element in all){if(String.Equals(element.Current.Name,profile,StringComparison.OrdinalIgnoreCase))return true;object p;if(element.TryGetCurrentPattern(ValuePattern.Pattern,out p)&&String.Equals(((ValuePattern)p).Current.Value,profile,StringComparison.OrdinalIgnoreCase))return true;}return false;}
        AutomationElement LoadButton(){if(Address()!=scheme+"://extensions"||!DocumentHasURL(scheme+"://extensions"))return null;return FindButton(AutomationElement.FromHandle(window),new[]{"加载解压缩的扩展","加载已解压的扩展程序","加载解压缩的扩展程序","Load unpacked"});}
        static AutomationElement FindButton(AutomationElement root,string[] names){var elements=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));var matches=new List<AutomationElement>();foreach(AutomationElement item in elements)if(names.Contains(item.Current.Name)&&item.Current.IsEnabled&&!item.Current.IsOffscreen)matches.Add(item);return matches.Count==1?matches[0]:null;}
        AutomationElement FindFolderEdit(){var edits=AutomationElement.FromHandle(dialog).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));var matches=new List<AutomationElement>();foreach(AutomationElement item in edits)if(new[]{"文件夹:","文件夹：","Folder:","文件夹","Folder"}.Contains(item.Current.Name)&&item.Current.IsEnabled&&!item.Current.IsOffscreen&&item.Current.ProcessId==(int)browserPid)matches.Add(item);return matches.Count==1?matches[0]:null;}
        bool IsOwnedDialog(IntPtr handle){uint pid;GetWindowThreadProcessId(handle,out pid);if(pid!=browserPid)return false;for(int depth=0;depth<8;depth++){handle=GetWindow(handle,4);if(handle==window)return true;if(handle==IntPtr.Zero)return false;}return false;}
        IntPtr FindDialog(){var matches=new List<IntPtr>();EnumWindows((handle,arg)=>{var cls=new StringBuilder(100);GetClassName(handle,cls,100);if(cls.ToString()=="#32770"&&IsWindowVisible(handle)&&IsOwnedDialog(handle))matches.Add(handle);return true;},IntPtr.Zero);if(matches.Count==1)dialog=matches[0];return matches.Count==1?dialog:IntPtr.Zero;}
        bool ThemeMatches(){try{var prefs=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(profile,"Preferences")));var extensions=prefs["extensions"] as Dictionary<string,object>;var theme=extensions["theme"] as Dictionary<string,object>;return String.Equals(Path.GetFullPath((string)theme["pack"]),folder,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        static IntPtr FindBrowser(string browser){var matches=new List<IntPtr>();string process=browser=="edge"?"msedge":"chrome",relative=browser=="edge"?"Microsoft\\Edge\\Application\\msedge.exe":"Google\\Chrome\\Application\\chrome.exe";var trusted=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}.Select(root=>Path.Combine(root,relative)).ToArray();EnumWindows((handle,arg)=>{if(!IsWindowVisible(handle))return true;var cls=new StringBuilder(100);GetClassName(handle,cls,100);if(cls.ToString()!="Chrome_WidgetWin_1")return true;uint pid;GetWindowThreadProcessId(handle,out pid);try{var p=Process.GetProcessById((int)pid);if(p.ProcessName==process&&trusted.Any(path=>String.Equals(path,p.MainModule.FileName,StringComparison.OrdinalIgnoreCase)))matches.Add(handle);}catch{}return true;},IntPtr.Zero);return matches.Count==1?matches[0]:IntPtr.Zero;}
        void Invoke(AutomationElement element){Check(true);object pattern;if(element==null||element.Current.ProcessId!=(int)browserPid||!element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))throw new InvalidOperationException("控件不支持安全调用或归属不匹配。");((InvokePattern)pattern).Invoke();}
        void EnterAddress(AutomationElement edit,string address){Check(true);for(int key=1;key<255;key++)if((GetAsyncKeyState(key)&0x8000)!=0)throw new InvalidOperationException("检测到按键或鼠标按钮仍被按住。");Thread.Sleep(40);Check(true);object value;if(!Same(AddressBar(),edit)||!Same(AutomationElement.FocusedElement,edit)||!edit.TryGetCurrentPattern(ValuePattern.Pattern,out value)||((ValuePattern)value).Current.Value!=address)throw new InvalidOperationException("发送回车前地址栏身份变化。");uint stamp=GetTickCount();if(stamp==0)throw new InvalidOperationException("无法标记自动输入。");var events=new[]{Stroke(0x0D,false,false),Stroke(0x0D,true,false)};for(int i=0;i<events.Length;i++)events[i].data.keyboard.time=stamp;Check(true);if(SendInput((uint)events.Length,events,Marshal.SizeOf(typeof(INPUT)))!=events.Length)throw new InvalidOperationException("回车发送失败。");inputStamp=stamp;Thread.Sleep(30);if(stamp!=LastInput())throw new InvalidOperationException("自动输入期间检测到其他输入，已停止。");Check(true);}
        internal static void Test(string browser="edge") {
            if(browser!="chrome")browser="edge";
            string root=Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-"+browser+"-"+DateTime.UtcNow.Ticks),profile=Path.Combine(root,"profile","Default"),theme=Path.Combine(root,"theme");Directory.CreateDirectory(profile);Directory.CreateDirectory(theme);
            File.WriteAllText(Path.Combine(profile,"Preferences"),"{\"extensions\":{\"ui\":{\"developer_mode\":true}}}");
            File.WriteAllText(Path.Combine(theme,"manifest.json"),"{\"manifest_version\":3,\"name\":\"DeskShift isolated color test\",\"version\":\"1.0\",\"theme\":{\"colors\":{\"frame\":[131,190,178],\"toolbar\":[235,246,242]}}}");
            string relative=browser=="edge"?"Microsoft\\Edge\\Application\\msedge.exe":"Google\\Chrome\\Application\\chrome.exe";
            string executable=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}.Select(path=>Path.Combine(path,relative)).First(File.Exists);
            var process=Process.Start(new ProcessStartInfo(executable,"--no-first-run --no-default-browser-check --user-data-dir=\""+Path.GetDirectoryName(profile)+"\" --new-window "+browser+"://extensions"){UseShellExecute=false});
            string result="";try{IntPtr handle=IntPtr.Zero;for(int i=0;i<80&&handle==IntPtr.Zero;i++){Thread.Sleep(100);process.Refresh();handle=process.MainWindowHandle;}if(handle==IntPtr.Zero)throw new Exception("独立测试浏览器没有出现窗口。");Thread.Sleep(800);
                SetForegroundWindow(handle);
                using(var mask=new DesktopTransition(new Palette{Accent="#83BEB2",Background="#F7FBFA",Text="#222734"},"对开幕帘")) {
                    var task=mask.CoverAsync();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();
                    var runner=System.Threading.Tasks.Task.Run(()=>new BrowserThemeLoader(CancellationToken.None,()=>mask.Covered).Run(browser,theme,profile,handle));
                    var time=Stopwatch.StartNew();while(!runner.IsCompleted&&time.ElapsedMilliseconds<7500){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}result=runner.IsCompleted?runner.Result:"测试超时";
                    task=mask.RevealAsync();while(!task.IsCompleted){System.Windows.Forms.Application.DoEvents();Thread.Sleep(5);}
                }
                File.WriteAllText(Path.Combine(root,"result.txt"),result);File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-result.txt"),root+"\r\n"+result);
            }catch(Exception ex){File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"test-output","auto-theme-result.txt"),ex.ToString());}
            finally{try{if(!process.HasExited){process.CloseMainWindow();if(!process.WaitForExit(1200))process.Kill();}}catch{}}
        }
        static INPUT Stroke(ushort key,bool up,bool unicode){return new INPUT{type=1,data=new INPUTUNION{keyboard=new KEYBDINPUT{vk=key,flags=(uint)(up?2:0)}}};}
        static uint LastInput(){var value=new LASTINPUTINFO{size=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO))};if(!GetLastInputInfo(ref value))throw new InvalidOperationException("无法检查用户输入。");return value.time;}
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
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll")]static extern uint GetTickCount();
    }
}
