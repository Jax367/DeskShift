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
            string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","wallpapers");
            var lib=new Library(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")),assets);
            Check(lib.State.Wallpapers.Count==3,"Initial wallpaper count");
            var summary=new StringBuilder();
            foreach(var w in lib.State.Wallpapers) {
                var p=w.Palette;Check(p.Swatches.Length>=3,"Palette diversity");Check(Palette.Contrast(Palette.ColorOf(p.Background),Palette.ColorOf(p.Text))>=4.5,"Readable text");
                summary.AppendLine(w.Name+": "+Library.Json.Serialize(p));
                string export=lib.ExportChrome(w);var manifest=Library.Json.DeserializeObject(System.IO.File.ReadAllText(Path.Combine(export,"manifest.json")));Check(manifest!=null,"Chrome theme export");
                string theme=WindowsTheme.ThemeText(w.File,p);Check(theme.Contains(p.Accent.Substring(1)),"Windows accent output");
            }
            Check(!lib.State.Wallpapers[0].Palette.Dark && !lib.State.Wallpapers[1].Palette.Dark && lib.State.Wallpapers[2].Palette.Dark,"Automatic light/dark on reference images");
            var mint=Palette.ColorOf(lib.State.Wallpapers[1].Palette.Accent);Check(mint.G>mint.R && mint.B>mint.R,"Mint background selected");
            string gray=Path.Combine(root,"gray.png");using(var bmp=new Bitmap(10,10)){using(var g=Graphics.FromImage(bmp))g.Clear(Color.White);bmp.Save(gray);}
            Check(!String.IsNullOrEmpty(Palette.Extract(gray).Accent),"Monochrome fallback");
            var imported=lib.Import(gray,"测试壁纸");Check(System.IO.File.Exists(imported.File),"Imported image copied");
            var reload=new Library(lib.Root,assets);Check(reload.State.Wallpapers.Count==4,"Persistence");
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
                int port=bridge.ListeningPort;
                bridge.Publish(lib.State.Wallpapers[0],lib.Effective(lib.State.Wallpapers[0]));
                using(var client=new WebClient()) {string json=client.DownloadString("http://127.0.0.1:"+port+"/theme");Check(json.Contains("palette"),"Bridge palette");Check(client.DownloadData("http://127.0.0.1:"+port+"/wallpaper").Length>0,"Bridge wallpaper");}
                Check(Request(port,"GET /theme HTTP/1.1\r\nHost: attacker.invalid\r\n\r\n").Contains("403"),"DNS rebinding blocked");
                Check(Request(port,"POST /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("403"),"Write endpoint blocked");
                Check(Request(port,"GET /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\nOrigin: https://example.com\r\n\r\n").Contains("403"),"Website origin blocked");
                bridge.Clear();Check(Request(port,"GET /theme HTTP/1.1\r\nHost: 127.0.0.1:"+port+"\r\n\r\n").Contains("404"),"Undo without active theme clears bridge");
            }
            var snapshot=WindowsTheme.Capture(null);var roundtrip=Library.Json.Deserialize<Snapshot>(Library.Json.Serialize(snapshot));Check(roundtrip.Values.Count==snapshot.Values.Count && roundtrip.Wallpaper==snapshot.Wallpaper,"Windows backup serialization");
            string failure=Path.Combine(root,"failure.txt");if(System.IO.File.Exists(failure))System.IO.File.Delete(failure);
            System.IO.File.WriteAllText(Path.Combine(root,"results.txt"),"PASS: extraction, contrast, mode, import, persistence, theme exports, bridge, host/origin/method restrictions.\r\n"+summary,Encoding.UTF8);
        }
        static string Request(int port,string request) {using(var client=new TcpClient("127.0.0.1",port)){client.ReceiveTimeout=5000;var stream=client.GetStream();byte[] bytes=Encoding.ASCII.GetBytes(request);stream.Write(bytes,0,bytes.Length);using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
        static void Pump(System.Threading.Tasks.Task task,string label="Animation stage"){var time=Stopwatch.StartNew();while(!task.IsCompleted && time.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(5);}Check(task.IsCompleted,label+" completes without hanging");task.GetAwaiter().GetResult();}
    }
}
