using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DeskShift {
    public static class AnimationProfile {
        [STAThread] public static void Main() {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var report=new StringBuilder();var screen=Screen.AllScreens.OrderByDescending(s=>(long)s.Bounds.Width*s.Bounds.Height).First();
            var palette=Palette.Extract(Path.Combine(Directory.GetCurrentDirectory(),"assets","wallpapers","01-lavender.jpg"));
            report.AppendLine("Hidden-window/offscreen diagnostic. No desktop capture, wallpaper/theme change or visible cover.");
            report.AppendLine("Display count: "+Screen.AllScreens.Length+"; largest logical display: "+screen.Bounds.Width+"x"+screen.Bounds.Height);
            report.AppendLine("Waiting frame pixels per palette/design (shared by monitors): "+(WaitingMotion.Width*(long)WaitingMotion.Height*4*WaitingMotion.Count)+" bytes.");
            report.AppendLine("This does not measure actual DWM presentation, GPU stalls or fullscreen Show(). DrawToBitmap adds offscreen-copy cost.");
            for(int style=0;style<5;style++) {
                var time=Stopwatch.StartNew();using(var window=new TransitionWindow(screen.Bounds,palette,style,style%6)) {
                    double construct=time.Elapsed.TotalMilliseconds;time.Restart();window.PrepareArtwork();double prepare=time.Elapsed.TotalMilliseconds;
                    time.Restart();var handle=window.Handle;double native=time.Elapsed.TotalMilliseconds;
                    var updates=new List<double>();
                    for(int i=0;i<=48;i++){time.Restart();window.UpdateFrame(0,i/48.0,i*.016);updates.Add(time.Elapsed.TotalMilliseconds);}
                    using(var target=new Bitmap(window.Width,window.Height)) {
                        window.UpdateFrame(0,.1,0);time.Restart();window.DrawToBitmap(target,new Rectangle(0,0,target.Width,target.Height));double first=time.Elapsed.TotalMilliseconds;
                        var paints=new List<double>();for(int i=0;i<5;i++){window.UpdateFrame(0,.2+i*.1,i*.04);time.Restart();window.DrawToBitmap(target,new Rectangle(0,0,target.Width,target.Height));paints.Add(time.Elapsed.TotalMilliseconds);}
                        updates.Sort();report.AppendLine(String.Format("{0}: construct {1:F1} ms; asset generation {2:F1} ms; hidden HWND {3:F1} ms; region-update p95 {4:F1} ms/max {5:F1} ms; offscreen first paint {6:F1} ms/warm median {7:F1} ms",DesktopTransition.Styles[style+1],construct,prepare,native,updates[(int)(updates.Count*.95)],updates.Last(),first,paints.OrderBy(x=>x).ElementAt(paints.Count/2)));
                    }
                }
            }
            AnimationResources.ClearUnused();IntPtr prior=IntPtr.Zero;
            for(int pass=0;pass<2;pass++) {
                int builds=AnimationResources.Builds,hits=AnimationResources.Hits;var time=Stopwatch.StartNew();
                var windows=TransitionWindowPool.Acquire(new[]{screen.Bounds},palette,0,0);double acquire=time.Elapsed.TotalMilliseconds;
                try{
                    time.Restart();foreach(var window in windows)window.PrepareArtwork();double assets=time.Elapsed.TotalMilliseconds;
                    time.Restart();foreach(var window in windows)window.WarmNativeSurface();double warmup=time.Elapsed.TotalMilliseconds;
                    time.Restart();foreach(var window in windows){var ready=window.PrimeFirstFrame(false);while(!ready.IsCompleted && time.ElapsedMilliseconds<2000){Application.DoEvents();System.Threading.Thread.Sleep(1);}ready.GetAwaiter().GetResult();}double barrier=time.Elapsed.TotalMilliseconds;
                    var handle=windows[0].Handle;report.AppendLine(String.Format("Pool {0}: acquire {1:F1} ms; assets {2:F1} ms; hidden surface warmup {3:F1} ms; first-paint barrier {4:F1} ms; new resource builds {5}; cache hits {6}; same HWND {7}",pass==0?"cold":"reused",acquire,assets,warmup,barrier,AnimationResources.Builds-builds,AnimationResources.Hits-hits,pass==1 && handle==prior));prior=handle;
                }finally{TransitionWindowPool.Release(windows);}
            }
            TransitionWindowPool.DisposeIdle();AnimationResources.ClearUnused();
            string path=Path.Combine(Directory.GetCurrentDirectory(),"test-output","animation-profile.txt");File.WriteAllText(path,report.ToString(),Encoding.UTF8);Console.Write(report.ToString());
        }
    }
}
