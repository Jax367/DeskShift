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
    static GraphicsPath Flow() {
        var p=new GraphicsPath();
        p.StartFigure();
        p.AddBezier(198,57,159,24,94,28,64,70);
        p.AddBezier(64,70,28,121,70,145,125,155);
        p.AddBezier(125,155,166,163,179,181,151,198);
        p.AddBezier(151,198,128,212,91,201,61,181);
        p.AddBezier(61,181,53,187,44,199,38,208);
        p.AddBezier(38,208,79,240,138,244,179,217);
        p.AddBezier(179,217,222,188,210,144,161,128);
        p.AddBezier(161,128,134,119,83,117,95,93);
        p.AddBezier(95,93,107,67,145,62,177,84);
        p.AddBezier(177,84,183,78,193,66,198,57);
        p.CloseFigure();return p;
    }
    public static void Create(string target,string preview) {
        int[] sizes={16,24,32,48,64,128,256};var images=new List<byte[]>();
        foreach(int size in sizes)using(var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
            using(var canvas=new Bitmap(size*4,size*4,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(canvas)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size*4/256f,size*4/256f);g.Clear(Color.Transparent);
                using(var tile=Rounded(8,8,240,240,57)) {
                    using(var brush=new LinearGradientBrush(new PointF(20,8),new PointF(235,248),Color.FromArgb(37,53,65),Color.FromArgb(18,23,39)))g.FillPath(brush,tile);
                    if(size>=32)using(var edge=new Pen(Color.FromArgb(45,192,213,226),1.5f))g.DrawPath(edge,tile);
                }
                using(var flow=Flow()) {
                    using(var brush=new LinearGradientBrush(new PointF(75,53),new PointF(170,221),Color.FromArgb(147,237,215),Color.FromArgb(169,152,245))) {
                        brush.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(161,246,216),Color.FromArgb(101,201,211),Color.FromArgb(135,163,233),Color.FromArgb(190,157,244)},Positions=new[]{0f,.36f,.65f,1f}};
                        g.FillPath(brush,flow);
                    }
                    if(size>=48) {
                        var state=g.Save();g.SetClip(flow);
                        using(var highlight=new Pen(Color.FromArgb(65,255,255,255),2.5f)){highlight.StartCap=LineCap.Round;highlight.EndCap=LineCap.Round;g.DrawBezier(highlight,185,56,148,33,98,42,76,78);g.DrawBezier(highlight,52,209,86,232,139,232,174,209);}
                        g.Restore(state);
                    }
                }
            }
            using(var g=Graphics.FromImage(bmp)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(canvas,new Rectangle(0,0,size,size),0,0,canvas.Width,canvas.Height,GraphicsUnit.Pixel);}
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
