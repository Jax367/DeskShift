$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('DeskShiftIconBuilder' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class DeskShiftIconBuilder {
    static GraphicsPath Rounded(float x,float y,float width,float height,float radius) {
        var p=new GraphicsPath();float d=radius*2;
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+width-d,y,d,d,270,90);
        p.AddArc(x+width-d,y+height-d,d,d,0,90);p.AddArc(x,y+height-d,d,d,90,90);p.CloseFigure();return p;
    }
    static void Fill(Graphics g,Color c,float x,float y,float w,float h,float r) {
        using(var p=Rounded(x,y,w,h,r))using(var b=new SolidBrush(c))g.FillPath(b,p);
    }
    static void Arrow(Graphics g,Color c,PointF a,PointF b,PointF tip,PointF wing) {
        using(var pen=new Pen(c,12)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLine(pen,a,b);g.DrawLines(pen,new[]{tip,b,wing});}
    }
    public static void Create(string target,string preview) {
        int[] sizes={16,24,32,48,64,128,256};var images=new List<byte[]>();
        foreach(int size in sizes)using(var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(bmp)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);g.Clear(Color.Transparent);
                Fill(g,Color.FromArgb(53,81,68),8,8,240,240,52);
                Fill(g,Color.FromArgb(241,246,238),40,48,176,130,17);
                using(var pen=new Pen(Color.FromArgb(196,219,201),9))g.DrawLine(pen,55,73,201,73);
                Arrow(g,Color.FromArgb(83,143,119),new PointF(78,109),new PointF(175,109),new PointF(155,90),new PointF(155,128));
                Arrow(g,Color.FromArgb(127,117,154),new PointF(175,148),new PointF(78,148),new PointF(98,129),new PointF(98,167));
                Fill(g,Color.FromArgb(241,246,238),118,178,20,18,3);
                Fill(g,Color.FromArgb(241,246,238),92,196,72,10,5);
                Color[] colors={Color.FromArgb(131,190,178),Color.FromArgb(191,171,205),Color.FromArgb(240,224,183)};
                for(int i=0;i<3;i++)using(var brush=new SolidBrush(colors[i]))g.FillEllipse(brush,99+i*24,218,12,12);
            }
            if(size==256)bmp.Save(preview,ImageFormat.Png);
            using(var stream=new MemoryStream()){bmp.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());}
        }
        using(var file=File.Create(target))using(var writer=new BinaryWriter(file)) {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);
                writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;
            }
            foreach(byte[] bytes in images)writer.Write(bytes);
        }
    }
}
'@
}
[DeskShiftIconBuilder]::Create((Join-Path $PSScriptRoot '..\assets\DeskShift.ico'), (Join-Path $PSScriptRoot '..\assets\DeskShift-icon.png'))
