using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskShift {
    // Opaque, shaped windows conceal the desktop while Windows applies a theme.
    // No screenshots, focus stealing, input hooks, or modification of wallpaper pixels.
    public sealed class TransitionWindow : Form {
        internal const double FirstAmount=4/48.0;
        Palette palette;int style,waitingStyle;readonly Size resolution;TransitionArtwork artwork;bool warmed;TaskCompletionSource<bool> firstPaint;int lastStage=-1,lastFrame=-1,lastSprite=-1;double waitingSince;
        public int Stage;public double Amount;public double Seconds;
        public TransitionWindow(Rectangle bounds,Palette p,int effectStyle,int waitStyle) {
            palette=p;style=effectStyle;waitingStyle=waitStyle;resolution=bounds.Size;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Bounds=bounds;
            AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;BackColor=Palette.ColorOf(p.Background);Cursor=Cursors.Default;
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p;}}
        internal void Configure(Palette p,int effectStyle,int waitStyle){ClearSession();palette=p;style=effectStyle;waitingStyle=waitStyle;BackColor=Palette.ColorOf(p.Background);Stage=-1;lastStage=lastFrame=lastSprite=-1;waitingSince=0;}
        internal void PrepareArtwork() {
            if(artwork!=null)return;artwork=new TransitionArtwork(resolution,palette,style,waitingStyle);
        }
        internal object MotionIdentity {get{return artwork==null?null:artwork.Motion.Value;}}
        internal void WarmNativeSurface(){if(warmed && IsHandleCreated)return;var handle=Handle;UpdateFrame(0,FirstAmount,0);using(var target=new Bitmap(resolution.Width,resolution.Height))DrawToBitmap(target,new Rectangle(Point.Empty,resolution));UpdateFrame(0,0,0);warmed=true;}
        internal Task PrimeFirstFrame(bool visible){WarmNativeSurface();firstPaint=new TaskCompletionSource<bool>();UpdateFrame(0,FirstAmount,0);if(visible){Show();Invalidate();Update();}else{using(var target=new Bitmap(resolution.Width,resolution.Height))DrawToBitmap(target,new Rectangle(Point.Empty,resolution));}return firstPaint.Task;}
        internal void ClearSession(){if(firstPaint!=null)firstPaint.TrySetResult(true);firstPaint=null;if(artwork!=null){artwork.Dispose();artwork=null;}}
        public void UpdateFrame(int stage,double amount,double seconds) {
            bool changedStage=Stage!=stage;if(changedStage && stage==1)waitingSince=seconds;Stage=stage;Amount=amount;Seconds=seconds;
            int frame=stage==1?48:Math.Max(0,Math.Min(48,(int)Math.Round(amount*48)));
            if(stage!=lastStage || frame!=lastFrame) {
                Region next=artwork==null?Shape(ClientSize,stage,amount,style):(stage==2?artwork.Geometry.Value.Reveal[frame]:artwork.Geometry.Value.Cover[frame]).Clone();
                var old=Region;Region=next;if(old!=null)old.Dispose();lastStage=stage;lastFrame=frame;
            }
            if(stage==1 && !changedStage){int sprite=WaitingMotion.Frame(seconds-waitingSince);if(sprite!=lastSprite){lastSprite=sprite;Invalidate(new Rectangle((Width-WaitingMotion.Width)/2,(Height-WaitingMotion.Height)/2,WaitingMotion.Width,WaitingMotion.Height));}}
            else{lastSprite=-1;Invalidate();}
        }
        internal static Region Shape(Size size,int stage,double t,int style=0) {
            if(stage==1)return new Region(new Rectangle(Point.Empty,size));
            var empty=new Region();empty.MakeEmpty();if((stage==0 && t<=0)||(stage==2 && t>=1))return empty;empty.Dispose();
            if((stage==0 && t>=1)||(stage==2 && t<=0))return new Region(new Rectangle(Point.Empty,size));
            float fill=(float)Ease(stage==0?t:1-t);
            if(style==1) {
                // Two curtains meet at the center, then part to reveal the desktop.
                var region=new Region(new RectangleF(0,0,size.Width*fill*.5f,size.Height));region.Union(new RectangleF(size.Width*(1-fill*.5f),0,size.Width*fill*.5f,size.Height));return region;
            }
            if(style==2) {
                // Alternating horizontal bands sweep in opposite directions.
                var region=new Region();region.MakeEmpty();const int bands=8;
                for(int i=0;i<bands;i++){float y=size.Height*i/(float)bands,h=size.Height/(float)bands+1;float offset=i*.014f;float f=(float)Ease(Math.Max(0,Math.Min(1,((stage==0?t:1-t)-offset)/(1-.1))));region.Union(new RectangleF(i%2==0?0:size.Width*(1-f),y,size.Width*f,h));}return region;
            }
            if(style==3) {
                // A circular iris expands to cover the display, then opens from the center.
                float radius=(float)Math.Sqrt((double)size.Width*size.Width+(double)size.Height*size.Height)*.5f;
                using(var path=new GraphicsPath()){float r=radius*(float)Ease(t);if(r<=0)return new Region(new Rectangle(Point.Empty,size));path.AddEllipse(size.Width*.5f-r,size.Height*.5f-r,r*2,r*2);var region=stage==0?new Region(path):new Region(new Rectangle(Point.Empty,size));if(stage==2)region.Exclude(path);return region;}
            }
            if(style==4) {
                // Staggered tiles grow and shrink without resampling the wallpaper.
                var region=new Region();region.MakeEmpty();const int cols=10,rows=6;
                for(int row=0;row<rows;row++)for(int column=0;column<cols;column++){float delay=((column*3+row*5)%9)/40f;double phase=Math.Max(0,Math.Min(1,((stage==0?t:1-t)-delay)/.8));float f=(float)Ease(phase);float w=size.Width/(float)cols,h=size.Height/(float)rows;region.Union(new RectangleF(column*w+w*(1-f)*.5f,row*h+h*(1-f)*.5f,(w+1)*f,(h+1)*f));}return region;
            }
            float skew=size.Width*.13f;float x=(float)((size.Width+2*skew)*Ease(t)-skew);
            using(var path=new GraphicsPath()) {
                if(stage==0)path.AddPolygon(new[]{new PointF(0,0),new PointF(x+skew,0),new PointF(x-skew,size.Height),new PointF(0,size.Height)});
                else path.AddPolygon(new[]{new PointF(x-skew,0),new PointF(size.Width,0),new PointF(size.Width,size.Height),new PointF(x+skew,size.Height)});
                return new Region(path);
            }
        }
        internal static double Ease(double t) {t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        protected override void OnPaint(PaintEventArgs e) {
            if(artwork==null)Render(e.Graphics,ClientRectangle,palette,style,Seconds);else e.Graphics.DrawImageUnscaled(artwork.Background.Value,0,0);
            if(Stage==1 && artwork!=null)artwork.Motion.Value.Draw(e.Graphics,(Width-WaitingMotion.Width)/2,(Height-WaitingMotion.Height)/2,Seconds-waitingSince);
            var painted=firstPaint;if(painted!=null && !painted.Task.IsCompleted)BeginInvoke((Action)(()=>painted.TrySetResult(true)));
        }
        protected override void Dispose(bool disposing){if(disposing)ClearSession();base.Dispose(disposing);}
        internal static void Render(Graphics g,Rectangle rect,Palette p,int style,double seconds) {
            Color accent=Palette.ColorOf(p.Accent),bg=Palette.ColorOf(p.Background);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var gradient=new LinearGradientBrush(rect,Palette.Mix(accent,bg,.16),Palette.Mix(bg,accent,.3),style==2?90f:35f))g.FillRectangle(gradient,rect);
            // Moving highlights decorate the mask itself; there is no intermediate page.
            float shift=(float)(seconds*rect.Width*.45);
            for(int i=-2;i<4;i++)using(var pen=new Pen(Color.FromArgb(24,Color.White),Math.Max(12,rect.Width*.04f)))g.DrawLine(pen,i*rect.Width*.5f+shift,0,i*rect.Width*.5f+shift-rect.Width*.3f,rect.Height);
        }
    }
    public sealed class DesktopTransition : IDisposable {
        readonly List<TransitionWindow> windows=new List<TransitionWindow>();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=16};
        public static readonly string[] Styles={"随机","斜向扫幕","对开幕帘","交错条带","圆形扩散","方格翻涌"};
        static readonly Random random=new Random();static int previousStyle=-1;
        internal static int PickStyle(string name){int chosen=Array.IndexOf(Styles,name)-1;if(chosen>=0)return chosen;int next=random.Next(5);if(next==previousStyle)next=(next+1+random.Next(4))%5;previousStyle=next;return next;}
        readonly Stopwatch clock=new Stopwatch(),lifetime=new Stopwatch();readonly bool showWindows;readonly int maximumDuration;TaskCompletionSource<bool> coverCompleted,revealCompleted;int stage;bool dismissed;
        internal bool Covered {get{return stage==1 && !dismissed;}}
        internal bool Dismissed {get{return dismissed;}}
        internal Task WhenDismissed {get{return revealCompleted.Task;}}
        internal bool ClockStarted {get{return clock.IsRunning;}}
        internal IntPtr[] WindowHandles {get{return windows.ConvertAll(w=>w.Handle).ToArray();}}
        internal bool SharedWaitingFrames {get{return windows.Count<2 || windows.TrueForAll(w=>Object.ReferenceEquals(w.MotionIdentity,windows[0].MotionIdentity));}}
        public DesktopTransition(Palette palette,string effect) : this(palette,effect,Array.ConvertAll(Screen.AllScreens,s=>s.Bounds),true,12000,WindowsTheme.AnimationsEnabled) {}
        internal DesktopTransition(Palette palette,string effect,Rectangle[] displays,bool visible,int deadline=12000,bool enabled=true) {
            showWindows=visible;maximumDuration=deadline;
            if(!enabled){dismissed=true;return;}
            int style=PickStyle(effect),waitStyle=WaitingMotion.PickStyle();windows.AddRange(TransitionWindowPool.Acquire(displays,palette,style,waitStyle));timer.Tick+=(s,e)=>Tick();
        }
        public Task PrepareAsync() {return Task.Run(()=>{foreach(var window in windows)window.PrepareArtwork();});}
        public async Task CoverAsync() {
            if(dismissed)return;await PrepareAsync();if(dismissed)return;stage=0;coverCompleted=new TaskCompletionSource<bool>();revealCompleted=new TaskCompletionSource<bool>();
            try{
                var painted=Task.WhenAll(windows.ConvertAll(w=>w.PrimeFirstFrame(showWindows)));
                if(await Task.WhenAny(painted,Task.Delay(2000))!=painted){Dismiss();return;}
                await painted;await Task.Yield();if(dismissed)return;
                clock.Restart();lifetime.Restart();timer.Start();await coverCompleted.Task;
            }catch{Dismiss();throw;}
        }
        public Task RevealAsync() {
            if(dismissed)return Task.FromResult(true);if(revealCompleted==null){Dismiss();return Task.FromResult(true);}if(stage!=2){stage=2;clock.Restart();}return revealCompleted.Task;
        }
        public void Dismiss() {if(dismissed)return;dismissed=true;timer.Stop();foreach(var window in windows)window.Hide();if(coverCompleted!=null)coverCompleted.TrySetResult(true);if(revealCompleted!=null)revealCompleted.TrySetResult(true);}
        void Tick() {
            // The cover cannot trap the desktop even if the Windows theme engine stalls.
            if(lifetime.ElapsedMilliseconds>maximumDuration){Dismiss();return;}
            double amount=stage==1?1:stage==0?Math.Min(1,TransitionWindow.FirstAmount+(1-TransitionWindow.FirstAmount)*clock.Elapsed.TotalMilliseconds/500):Math.Min(1,clock.Elapsed.TotalMilliseconds/650);
            foreach(var window in windows)window.UpdateFrame(stage,amount,lifetime.Elapsed.TotalSeconds);
            if(stage!=1 && amount>=1){if(stage==0){stage=1;clock.Restart();foreach(var window in windows)window.UpdateFrame(1,1,lifetime.Elapsed.TotalSeconds);coverCompleted.TrySetResult(true);}else Dismiss();}
        }
        public void Dispose() {Dismiss();timer.Dispose();TransitionWindowPool.Release(windows);windows.Clear();}
    }
}
