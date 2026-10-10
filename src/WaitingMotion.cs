using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DeskShift {
    // Original geometric loops inspired by the motion references in README.md.
    internal sealed class WaitingMotion : IDisposable {
        public const int Width=400,Height=240,Count=72;
        public static readonly string[] Names={"轨道光环","无限回环","模块矩阵","节奏波列","呼吸花环","折叠棱镜"};
        static readonly Random random=new Random();static int previous=-1;
        internal static int PickStyle(){int next=random.Next(Names.Length);if(next==previous)next=(next+1+random.Next(Names.Length-1))%Names.Length;previous=next;return next;}
        readonly Bitmap[] frames=new Bitmap[Count];
        public WaitingMotion(Palette palette,int style) {
            try{for(int i=0;i<Count;i++){frames[i]=new Bitmap(Width,Height);using(var g=Graphics.FromImage(frames[i]))Render(g,palette,style,i/(double)Count);}}catch{Dispose();throw;}
        }
        public static int Frame(double seconds){return (int)(Math.Max(0,seconds)*30)%Count;}
        public void Draw(Graphics g,int x,int y,double seconds){g.DrawImageUnscaled(frames[Frame(seconds)],x,y);}
        static PointF Point(double a,double r){return new PointF((float)(Math.Cos(a)*r),(float)(Math.Sin(a)*r));}
        static void Dot(Graphics g,Color c,float x,float y,float r){using(var brush=new SolidBrush(c))g.FillEllipse(brush,x-r,y-r,2*r,2*r);}
        static Pen Stroke(Color c,float width){return new Pen(c,width){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};}
        static void Render(Graphics g,Palette p,int style,double phase) {
            g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(Width/2f,Height/2f);
            Color ink=Palette.ColorOf(p.Text),accent=Palette.ColorOf(p.Accent);
            Color light=Palette.Mix(accent,Color.White,.76),main=Palette.Mix(accent,p.Dark?Color.White:Color.Black,p.Dark?.5:.25);
            Color[] colors={ink,light,main};double angle=phase*2*Math.PI;
            if(style==0) {
                for(int i=0;i<3;i++) {
                    float r=31+i*19,start=(float)(phase*360*(i%2==0?1:-1)+i*100);
                    using(var pen=Stroke(Color.FromArgb(28,ink),1.5f))g.DrawEllipse(pen,-r,-r,r*2,r*2);
                    using(var pen=Stroke(colors[i],4-i*.6f))g.DrawArc(pen,-r,-r,r*2,r*2,start,76+i*19);
                    var point=Point(start*Math.PI/180,r);Dot(g,colors[i],point.X,point.Y,4.5f-i*.5f);
                }
                var state=g.Save();g.RotateTransform((float)(phase*180));using(var brush=new SolidBrush(light))g.FillRectangle(brush,-5,-5,10,10);g.Restore(state);
            } else if(style==1) {
                Func<double,PointF> infinity=t=>new PointF((float)(Math.Sin(t)*76),(float)(Math.Sin(t*2)*33));
                var path=new PointF[161];for(int i=0;i<path.Length;i++)path[i]=infinity(i*2*Math.PI/160);
                using(var pen=Stroke(Color.FromArgb(38,ink),6))g.DrawLines(pen,path);
                for(int arm=0;arm<2;arm++) {
                    var trail=new PointF[48];for(int i=0;i<trail.Length;i++)trail[i]=infinity(angle+arm*Math.PI-1.55+i*1.55/(trail.Length-1));
                    using(var pen=Stroke(arm==0?ink:light,7))g.DrawLines(pen,trail);var head=infinity(angle+arm*Math.PI);Dot(g,arm==0?ink:light,head.X,head.Y,6.5f);
                }
            } else if(style==2) {
                for(int y=0;y<3;y++)for(int x=0;x<3;x++) {
                    double wave=.5+.5*Math.Sin(angle-x*.8-y*.8);float s=(float)(10+15*wave);var state=g.Save();g.TranslateTransform((x-1)*37,(y-1)*37);g.RotateTransform((float)(phase*180+(x-y)*15));
                    using(var brush=new SolidBrush(colors[(x+y)%3]))g.FillRectangle(brush,-s/2,-s/2,s,s);g.Restore(state);
                }
            } else if(style==3) {
                for(int i=0;i<7;i++) {
                    float h=(float)(17+69*(.5+.5*Math.Sin(angle-i*.58)));float x=(i-3)*20;
                    using(var pen=Stroke(colors[i%3],8))g.DrawLine(pen,x,-h/2,x,h/2);Dot(g,Color.FromArgb(75,ink),x,64,1.5f);
                }
            } else if(style==4) {
                for(int i=0;i<12;i++) {
                    var state=g.Save();g.RotateTransform(i*30+(float)(Math.Sin(angle)*18));double offset=i*Math.PI/6;float radius=(float)(35+11*Math.Sin(angle-offset)),length=(float)(18+10*(.5+.5*Math.Sin(angle-offset)));
                    using(var pen=Stroke(colors[i%3],6))g.DrawLine(pen,0,-radius,0,-radius-length);g.Restore(state);
                }
                Dot(g,light,0,0,(float)(4+2*Math.Sin(angle)));
            } else {
                for(int i=0;i<3;i++) {
                    double a=i*2*Math.PI/3-Math.PI/2;double expand=24+10*(.5+.5*Math.Sin(angle-i*2*Math.PI/3));PointF pos=Point(a,expand);
                    float s=20+(float)Math.Sin(angle-i*2*Math.PI/3)*3,cx=pos.X,cy=pos.Y;
                    PointF top=new PointF(cx,cy-s),left=new PointF(cx-s*.86f,cy-s*.5f),right=new PointF(cx+s*.86f,cy-s*.5f),center=new PointF(cx,cy),bottom=new PointF(cx,cy+s),ll=new PointF(cx-s*.86f,cy+s*.5f),rr=new PointF(cx+s*.86f,cy+s*.5f);
                    using(var brush=new SolidBrush(light))g.FillPolygon(brush,new[]{top,right,center,left});using(var brush=new SolidBrush(main))g.FillPolygon(brush,new[]{left,center,bottom,ll});using(var brush=new SolidBrush(ink))g.FillPolygon(brush,new[]{center,right,rr,bottom});
                }
            }
        }
        public void Dispose(){foreach(var frame in frames)if(frame!=null)frame.Dispose();}
    }
}
