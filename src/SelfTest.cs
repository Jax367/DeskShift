using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace DeskShift {
    public static class SelfTest {
        static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        public static void Run() {
            string root=Path.Combine(Directory.GetCurrentDirectory(),"test-output");Directory.CreateDirectory(root);
            Check(!new Settings().AutoBrowserTheme,"Experimental automatic theme loading is off by default");
            using(var blurSample=new Bitmap(35,35,System.Drawing.Imaging.PixelFormat.Format24bppRgb)){using(var g=Graphics.FromImage(blurSample)){g.Clear(Color.Black);g.FillRectangle(Brushes.White,17,0,18,35);}BrowserWallpaper.Blur(blurSample,7);Check(blurSample.GetPixel(17,17).R>60&&blurSample.GetPixel(17,17).R<220,"Acrylic blur softens a sharp edge");Check(blurSample.GetPixel(0,17).R<blurSample.GetPixel(34,17).R,"Acrylic blur retains broad color composition");}
            var legacyChrome=Library.Json.Deserialize<Settings>("{\"AutoThemeBrowser\":\"chrome\"}");Check(legacyChrome.ThemeTargets().SequenceEqual(new[]{"chrome"}),"Old Chrome selection is preserved");
            Check(new Settings().ThemeTargets().SequenceEqual(new[]{"edge"}),"Legacy default target remains Edge");
            var both=new Settings{AutoThemeEdge=true,AutoThemeChrome=true};Check(Library.Json.Deserialize<Settings>(Library.Json.Serialize(both)).ThemeTargets().SequenceEqual(new[]{"edge","chrome"}),"Both browser choices persist in order");
            Check(new Settings{AutoThemeEdge=false,AutoThemeChrome=false,AutoThemeBrowser="chrome"}.ThemeTargets().Length==0,"Explicitly disabled targets do not revert to old selection");
            bool rejected=false;try{BrowserThemeLoader.ValidateManifest("{\"manifest_version\":3,\"name\":\"bad\",\"version\":\"1.0\",\"permissions\":[\"history\"],\"theme\":{\"colors\":{\"frame\":[1,2,3]}}}");}catch(InvalidOperationException){rejected=true;}Check(rejected,"Theme loader rejects runtime permissions");
            rejected=false;try{BrowserThemeLoader.ValidateManifest("{\"theme\":{\"colors\":{\"frame\":[999,2,3]}}}");}catch(InvalidOperationException){rejected=true;}Check(rejected,"Theme loader rejects invalid RGB");
            Check(new BrowserThemeLoader(CancellationToken.None,()=>false).Run("edge","unused").StartsWith("跳过"),"Missing cover prevents all browser input");
            string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","wallpapers");
            var lib=new Library(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")),assets);
            Check(lib.State.Wallpapers.Count==3,"Initial wallpaper count");
            Check(BrowserWallpaper.Normalize(null)=="acrylic","Existing installations retain acrylic background");
            var persistedStyle=Library.Json.Deserialize<Settings>(Library.Json.Serialize(new Settings{BrowserBackgroundStyle="blocks"}));Check(persistedStyle.BrowserBackgroundStyle=="blocks","Background style persists");
            foreach(string style in BrowserWallpaper.StyleIds) {
                foreach(int index in new[]{0,2}){var wallpaper=lib.State.Wallpapers[index];File.Copy(BrowserWallpaper.Prepare(wallpaper.File,style,lib.Effective(wallpaper)),Path.Combine(root,"preview-browser-"+style+"-"+(index==2?"dark":"light")+".jpg"),true);}
            }
            var sample=lib.State.Wallpapers[0];Check(BrowserWallpaper.StyleIds.Select(style=>BrowserWallpaper.Prepare(sample.File,style,lib.Effective(sample))).Distinct().Count()==3,"Background styles use separate cache entries");
            string blockRedPath=Path.Combine(root,"block-red.png"),blockBluePath=Path.Combine(root,"block-blue.png");using(var image=new Bitmap(120,80)){using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Red);image.Save(blockRedPath);using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Blue);image.Save(blockBluePath);}
            Check(File.ReadAllBytes(BrowserWallpaper.Prepare(blockRedPath,"blocks",sample.Palette)).SequenceEqual(File.ReadAllBytes(BrowserWallpaper.Prepare(blockBluePath,"blocks",sample.Palette))),"Abstract blocks depend on palette, not source subjects or pixels");
            var summary=new StringBuilder();
            foreach(var w in lib.State.Wallpapers) {
                var p=w.Palette;Check(p.Swatches.Length>=3,"Palette diversity");Check(Palette.Contrast(Palette.ColorOf(p.Background),Palette.ColorOf(p.Text))>=4.5,"Readable text");
                summary.AppendLine(w.Name+": "+Library.Json.Serialize(p));
                string export=lib.ExportChrome(w);var manifest=Library.Json.DeserializeObject(System.IO.File.ReadAllText(Path.Combine(export,"manifest.json")));Check(manifest!=null,"Chrome theme export");
                string theme=WindowsTheme.ThemeText(w.File,p);Check(theme.Contains(p.Accent.Substring(1)),"Windows accent output");
            }
            foreach(var browser in new[]{"chrome","edge"}) {
                string folder=lib.ExportBrowser(lib.State.Wallpapers[0],browser);var manifest=Library.Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(System.IO.File.ReadAllText(Path.Combine(folder,"manifest.json")));
                foreach(var forbidden in new[]{"chrome_url_overrides","chrome_settings_overrides","homepage_url","permissions","host_permissions","content_scripts","background"})Check(!manifest.ContainsKey(forbidden),"Color-only theme preserves pages and has no runtime permissions: "+forbidden);
                var theme=(System.Collections.Generic.Dictionary<string,object>)manifest["theme"];Check(!theme.ContainsKey("images"),"Browser theme does not include wallpapers");var colors=(System.Collections.Generic.Dictionary<string,object>)theme["colors"];Check(!colors.Keys.Any(key=>key.StartsWith("ntp_")),"Browser theme leaves new-tab colors unspecified");
                Check(Library.Json.Serialize(colors["frame"])==Library.Json.Serialize(colors["frame_inactive"]),"Inactive title bar follows wallpaper accent");
                File.WriteAllText(Path.Combine(folder,"Cached Theme.pak"),"old theme cache");
                string reusedFolder=lib.ExportBrowser(lib.State.Wallpapers[1],browser);Check(reusedFolder==folder && System.IO.File.ReadAllText(Path.Combine(folder,"manifest.json")).Contains("131,190,178"),"Theme file updates in the same reloadable folder");
                Check(!File.Exists(Path.Combine(folder,"Cached Theme.pak")),"Changed theme invalidates cached colors");
                Check(File.ReadAllText(Path.Combine(folder,"HOW-TO.html")).Contains(browser+"://extensions"),"Theme guide points to the correct browser");
            }
            summary.AppendLine("Chrome and Edge: colors only; no page overrides, wallpaper images, scripts, homepage/search settings or permissions; stable export folders passed.");
            Check(!lib.State.Wallpapers[0].Palette.Dark && !lib.State.Wallpapers[1].Palette.Dark && lib.State.Wallpapers[2].Palette.Dark,"Automatic light/dark on reference images");
            var mint=Palette.ColorOf(lib.State.Wallpapers[1].Palette.Accent);Check(mint.G>mint.R && mint.B>mint.R,"Mint background selected");
            string gray=Path.Combine(root,"gray.png");using(var bmp=new Bitmap(10,10)){using(var g=Graphics.FromImage(bmp))g.Clear(Color.White);bmp.Save(gray);}
            Check(!String.IsNullOrEmpty(Palette.Extract(gray).Accent),"Monochrome fallback");
            var imported=lib.Import(gray,"测试壁纸");Check(System.IO.File.Exists(imported.File),"Imported image copied");
            var reload=new Library(lib.Root,assets);Check(reload.State.Wallpapers.Count==4,"Persistence");
            Check(!reload.State.ShowFloatingOrb,"Floating orb is opt-in");lib.State.ShowFloatingOrb=true;lib.State.HasOrbPosition=true;lib.State.OrbX=-12;lib.State.OrbY=240;lib.Save();reload=new Library(lib.Root,assets);Check(reload.State.ShowFloatingOrb && reload.State.HasOrbPosition && reload.State.OrbX==-12 && reload.State.OrbY==240,"Floating orb settings and monitor position persist");
            var bounded=OrbWindow.Clamp(new Point(-100000,-100000),new Size(60,60));var orbArea=Screen.FromRectangle(new Rectangle(bounded,new Size(60,60))).WorkingArea;Check(orbArea.Contains(new Rectangle(bounded,new Size(60,60))),"An unavailable monitor position is moved onto an existing display");
            using(var preview=new Bitmap(94,54))using(var picker=new WallpaperPicker(lib,w=>preview,w=>{})){Check(picker.Controls.OfType<FlowLayoutPanel>().Single().Controls.Count==4,"Floating picker includes imported wallpapers");}
            var watch=Stopwatch.StartNew();var large=lib.State.Wallpapers[1];string cached=WindowsTheme.PrepareWallpaper(lib.Root,large);long cold=watch.ElapsedMilliseconds;
            using(var original=new Bitmap(large.File))using(var full=new Bitmap(cached)){Check(full.Size==original.Size,"Original dimensions preserved");foreach(var point in new[]{new Point(0,0),new Point(original.Width/2,original.Height/2),new Point(original.Width-1,original.Height-1)})Check(full.GetPixel(point.X,point.Y).ToArgb()==original.GetPixel(point.X,point.Y).ToArgb(),"Decoded pixels preserved");summary.AppendLine("Original wallpaper dimensions: "+full.Width+"x"+full.Height);}
            DateTime modified=System.IO.File.GetLastWriteTimeUtc(cached);watch.Restart();string reused=WindowsTheme.PrepareWallpaper(lib.Root,large);long warm=watch.ElapsedMilliseconds;
            Check(cached==reused && System.IO.File.GetLastWriteTimeUtc(cached)==modified,"Repeated switch reuses bitmap without rewriting");
            summary.AppendLine("Full-resolution cache: "+new FileInfo(cached).Length+" bytes; prepare "+cold+" ms; reuse "+warm+" ms.");
            var prepared=WindowsTheme.Prepare(lib,large,lib.Effective(large),true);Check(prepared.Bitmap==cached && System.IO.File.ReadAllText(prepared.ThemeFile).Contains(cached),"Wallpaper and theme are prepared before animation");Check(lib.State.ActiveId==null && !System.IO.File.Exists(Path.Combine(lib.Root,"undo.json")),"Preparation does not apply theme or overwrite undo");
            for(int style=0;style<5;style++)using(var enter=TransitionWindow.Shape(new Size(800,450),0,0,style))using(var covered=TransitionWindow.Shape(new Size(800,450),1,1,style))using(var exit=TransitionWindow.Shape(new Size(800,450),2,1,style))using(var middle=TransitionWindow.Shape(new Size(800,450),0,.5,style)) {
                Check(!enter.IsVisible(400,225),"Every cover starts empty");Check(covered.IsVisible(0,0)&&covered.IsVisible(799,449),"Every cover hides the entire display");Check(!exit.IsVisible(400,225),"Every reveal removes cover");int pixels=0;for(int y=0;y<24;y++)for(int x=0;x<40;x++)if(middle.IsVisible(x*20+10,y*18.75f+9.375f))pixels++;Check(pixels>0 && pixels<960,"Every mask exposes a partial transition: "+style);
            }
            int previous=DesktopTransition.PickStyle("随机");for(int i=0;i<30;i++){int current=DesktopTransition.PickStyle("随机");Check(current!=previous,"Random transitions do not repeat consecutively");previous=current;}
            using(var frame=new Bitmap(1500,820))using(var g=Graphics.FromImage(frame))using(var wallpaper=Image.FromFile(lib.State.Wallpapers[0].File))using(var font=new Font("Microsoft YaHei UI",15))using(var brush=new SolidBrush(Color.FromArgb(35,42,51))) {
                g.Clear(Color.FromArgb(246,247,243));for(int style=0;style<5;style++) {
                    int x=20+style%3*500,y=40+style/3*390;g.DrawString(DesktopTransition.Styles[style+1],font,brush,x,y-32);g.DrawImage(wallpaper,x,y,460,320);
                    var state=g.Save();g.TranslateTransform(x,y);using(var region=TransitionWindow.Shape(new Size(460,320),0,.55,style))g.SetClip(region,System.Drawing.Drawing2D.CombineMode.Replace);TransitionWindow.Render(g,new Rectangle(0,0,460,320),lib.State.Wallpapers[style%3].Palette,style,.2);g.Restore(state);
                }frame.Save(Path.Combine(root,"transition-preview.png"));
            }
            var testDisplays=new[]{new Rectangle(-320,0,320,180),new Rectangle(0,0,320,180)};
            using(var preview=new Bitmap(1200,680))using(var g=Graphics.FromImage(preview))using(var font=new Font("Microsoft YaHei UI",12)) {
                for(int i=0;i<6;i++){var palette=lib.State.Wallpapers[i%3].Palette;var state=g.Save();g.TranslateTransform(i%3*400,i/3*340);TransitionWindow.Render(g,new Rectangle(0,0,400,340),palette,0,.2);using(var motion=new WaitingMotion(palette,i))motion.Draw(g,0,30,.4);using(var brush=new SolidBrush(Palette.ColorOf(palette.Text)))using(var align=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(WaitingMotion.Names[i],font,brush,new RectangleF(0,282,400,30),align);g.Restore(state);}preview.Save(Path.Combine(root,"waiting-preview.png"));
            }
            int lastWait=WaitingMotion.PickStyle();for(int i=0;i<30;i++){int nextWait=WaitingMotion.PickStyle();Check(nextWait!=lastWait,"Waiting designs do not repeat consecutively");lastWait=nextWait;}
            var testPalette=lib.State.Wallpapers[0].Palette;
            using(var first=new TransitionWindow(testDisplays[0],testPalette,0,0))using(var second=new TransitionWindow(new Rectangle(0,0,640,360),testPalette,0,0)) {
                first.PrepareArtwork();second.PrepareArtwork();Check(Object.ReferenceEquals(first.MotionIdentity,second.MotionIdentity),"Different monitor sizes share waiting frames");
                int builds=AnimationResources.Builds;using(var repeat=new TransitionWindow(testDisplays[0],testPalette,0,0)){repeat.PrepareArtwork();Check(AnimationResources.Builds==builds,"Repeated artwork uses cache without regeneration");}
                var ready=first.PrimeFirstFrame(false);Check(!ready.IsCompleted,"First paint barrier waits for UI message completion");Pump(ready);
                first.ClearSession();using(var frame=new Bitmap(640,360)){second.UpdateFrame(1,1,0);second.DrawToBitmap(frame,new Rectangle(0,0,640,360));}
            }
            IntPtr[] pooledHandles;
            using(var effect=new DesktopTransition(testPalette,"测试",testDisplays,false)){var cover=effect.CoverAsync();Check(!effect.ClockStarted,"Animation clock waits for prepared first paint");Pump(cover);Check(effect.ClockStarted && effect.SharedWaitingFrames,"Animation starts after preparation and shares monitor sprites");pooledHandles=effect.WindowHandles;Check(effect.Covered,"Multiple display covers wait for background operation");Pump(effect.RevealAsync());Check(effect.Dismissed,"Reveal cleans up overlay");}
            using(var effect=new DesktopTransition(testPalette,"对开幕帘",testDisplays,false)){Check(effect.WindowHandles.SequenceEqual(pooledHandles),"Native windows reused across different effects");Pump(effect.CoverAsync(),"Reused cover");Check(effect.Covered,"Reused windows complete first paint without fallback");Pump(effect.RevealAsync(),"Reused reveal");}
            summary.AppendLine("First-paint barrier, delayed animation clock, HWND reuse, shared multi-monitor frames and repeated artwork cache passed.");
            using(var effect=new DesktopTransition(lib.State.Wallpapers[0].Palette,"测试",testDisplays,false,60)){Pump(effect.CoverAsync());Check(effect.Dismissed,"Timeout removes stalled cover");Pump(effect.RevealAsync());}
            using(var effect=new DesktopTransition(lib.State.Wallpapers[0].Palette,"交错条带",testDisplays,false)){var duration=Stopwatch.StartNew();Pump(effect.CoverAsync());Pump(MainForm.RunBackground(()=>Thread.Sleep(850)));Check(effect.Covered && !effect.WhenDismissed.IsCompleted,"Cover stays in place until slow switching completes");Pump(effect.RevealAsync());Check(effect.Dismissed,"Cover reveals after slow backend completes");summary.AppendLine("Waited for slow backend before reveal: "+duration.ElapsedMilliseconds+" ms.");}
            using(var effect=new DesktopTransition(lib.State.Wallpapers[0].Palette,"斜向扫幕",testDisplays,false)){Pump(effect.CoverAsync());bool failed=false;try{Pump(MainForm.RunBackground(()=>{throw new InvalidOperationException("simulated switch failure");}));}catch(InvalidOperationException){failed=true;}Pump(effect.RevealAsync());Check(failed && effect.Dismissed,"A failed switch can clean up the cover");}
            using(var effect=new DesktopTransition(lib.State.Wallpapers[0].Palette,"随机",testDisplays,false)){Pump(effect.RevealAsync());Check(effect.Dismissed,"Cleanup before cover starts does not hang");}
            summary.AppendLine("Full-screen transitions: 5 masks, cached artwork/regions, nonrepeating random selection, backend completion gate, failure/timeout cleanup passed (hidden windows).");
            using(var animationPreview=new Preview{Size=new Size(300,200)}) {
                animationPreview.CreateControl();var blue=new Bitmap(300,200);using(var g=Graphics.FromImage(blue))g.Clear(Color.Blue);animationPreview.SetContent(blue,lib.State.Wallpapers[0].Palette);
                var red=new Bitmap(300,200);using(var g=Graphics.FromImage(red))g.Clear(Color.Red);animationPreview.SetContent(red,lib.State.Wallpapers[0].Palette);
                if(WindowsTheme.AnimationsEnabled)using(var frame=new Bitmap(300,200)){animationPreview.DrawToBitmap(frame,new Rectangle(0,0,300,200));Check(frame.GetPixel(1,1).B>frame.GetPixel(1,1).R,"Fade starts with previous frame");}
                var timer=new System.Windows.Forms.Timer{Interval=15};int ticks=0;timer.Tick+=(s,e)=>ticks++;timer.Start();
                var work=MainForm.RunBackground(()=>{Check(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"Shell worker uses STA");Thread.Sleep(300);});
                while(!work.IsCompleted){Application.DoEvents();Thread.Sleep(5);}work.GetAwaiter().GetResult();Application.DoEvents();timer.Dispose();Check(ticks>=4,"UI remains responsive during background work");
                using(var frame=new Bitmap(300,200)){animationPreview.DrawToBitmap(frame,new Rectangle(0,0,300,200));Check(frame.GetPixel(1,1).R>240 && frame.GetPixel(1,1).B<10,"Fade completes with new frame");}
                summary.AppendLine("Background processing UI heartbeat: "+ticks+" ticks; transition completed.");
            }
            lib.State.Mode="深色";Check(lib.Effective(lib.State.Wallpapers[0]).Dark,"Mode override");
            using(var bridge=new Bridge(0)) {
                int port=bridge.ListeningPort;var wallpaper=lib.State.Wallpapers[0];bridge.Publish(wallpaper,lib.Effective(wallpaper));
                var preparation=Stopwatch.StartNew();while(preparation.ElapsedMilliseconds<15000 && !Request(port,"GET /theme?browser=edge HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("200 OK"))Thread.Sleep(30);
                using(var client=new WebClient()){var data=Library.Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(client.DownloadString("http://127.0.0.1:"+port+"/theme?browser=edge"));var bytes=client.DownloadData("http://127.0.0.1:"+port+"/wallpaper?revision="+data["revision"]);Check(bytes.SequenceEqual(System.IO.File.ReadAllBytes(BrowserWallpaper.Prepare(wallpaper.File))),"Browser receives its compressed derivative");}
                Check(bridge.LastSeenEdge!=DateTime.MinValue && bridge.LastSeenChrome==DateTime.MinValue,"Browser connection indicators remain independent");
                Check(Request(port,"GET /theme?browser=chrome HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nOrigin: chrome-extension://test\r\n\r\n").Contains("200 OK"),"Chrome extension receives the same palette");
                Check(Request(port,"GET /theme HTTP/1.1\r\nHost: attacker.invalid\r\n\r\n").Contains("403"),"Unexpected host rejected");Check(Request(port,"POST /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("403"),"Write request rejected");Check(Request(port,"GET /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nOrigin: https://example.com\r\n\r\n").Contains("403"),"Website origin rejected");Check(Request(port,"GET /wallpaper?revision=outdated HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("404"),"Outdated wallpaper revision rejected");bridge.Clear();Check(Request(port,"GET /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("404"),"Undo without active wallpaper clears browser bridge");
                summary.AppendLine("Browser bridge: compressed browser derivative, palette sync, per-browser status, revision gate and host/origin/method restrictions passed.");
                bridge.Publish(wallpaper,lib.Effective(wallpaper),"blocks");var styleWait=Stopwatch.StartNew();while(styleWait.ElapsedMilliseconds<15000&&!Request(port,"GET /theme?browser=edge HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("\"backgroundStyle\":\"blocks\""))Thread.Sleep(30);
                Check(Request(port,"GET /theme?browser=edge HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("\"backgroundStyle\":\"blocks\""),"Same wallpaper can publish a new browser style");bridge.Clear();
            }
            foreach(var wallpaper in lib.State.Wallpapers.Take(3)) {
                byte[] original=File.ReadAllBytes(wallpaper.File);string compressed=BrowserWallpaper.Prepare(wallpaper.File);
                using(var image=Image.FromFile(compressed))using(var source=Image.FromFile(wallpaper.File)){Check(Math.Max(image.Width,image.Height)<=2560,"Browser background longest edge bounded");Check(image.Width<=source.Width && image.Height<=source.Height,"Browser backgrounds never upscale");Check(Math.Abs((double)image.Width/image.Height-(double)source.Width/source.Height)<.003,"Browser background preserves aspect ratio");summary.AppendLine(wallpaper.Name+": browser JPEG "+image.Width+"x"+image.Height+", "+new FileInfo(compressed).Length+" bytes / original "+original.Length+" bytes");}
                Check(original.SequenceEqual(File.ReadAllBytes(wallpaper.File)),"Browser compression leaves desktop original unchanged");Check(BrowserWallpaper.Prepare(wallpaper.File)==compressed,"Browser derivative cache reused");
            }
            var snapshot=WindowsTheme.Capture(null);var roundtrip=Library.Json.Deserialize<Snapshot>(Library.Json.Serialize(snapshot));Check(roundtrip.Values.Count==snapshot.Values.Count && roundtrip.Wallpaper==snapshot.Wallpaper,"Windows backup serialization");
            string failure=Path.Combine(root,"failure.txt");if(System.IO.File.Exists(failure))System.IO.File.Delete(failure);
            System.IO.File.WriteAllText(Path.Combine(root,"results.txt"),"PASS: extraction, contrast, mode, import, persistence, theme exports; color-only browser themes preserve pages.\r\n"+summary,Encoding.UTF8);
        }
        static void Pump(System.Threading.Tasks.Task task,string label="Animation stage"){var time=Stopwatch.StartNew();while(!task.IsCompleted && time.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(5);}Check(task.IsCompleted,label+" completes without hanging");task.GetAwaiter().GetResult();}
        static string Request(int port,string request){using(var client=new TcpClient("127.0.0.1",port)){client.ReceiveTimeout=5000;var stream=client.GetStream();byte[] bytes=Encoding.ASCII.GetBytes(request);stream.Write(bytes,0,bytes.Length);using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
    }
}
