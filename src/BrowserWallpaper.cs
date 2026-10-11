using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace DeskShift {
    // Browser-only derivative. Never overwrite the desktop original or its BMP cache.
    public static class BrowserWallpaper {
        static readonly object gate=new object();
        public static readonly string[] Styles={"压缩原图","亚克力模糊","抽象色块"};
        public static readonly string[] StyleIds={"original","acrylic","blocks"};
        public static string Normalize(string style){return StyleIds.Contains(style)?style:"acrylic";}
        public static string Prepare(string source,string style="acrylic",Palette palette=null) {
            style=Normalize(style);if(style=="blocks"&&palette==null)palette=Palette.Extract(source);
            var info=new FileInfo(source);
            string key;using(var hash=SHA256.Create())key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(info.FullName+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|2560-q82-styles-v3|"+style+"|"+(style=="blocks"?Library.Json.Serialize(palette):"")))).Replace("-","");
            string folder=Path.Combine(info.DirectoryName,"browser-cache"),target=Path.Combine(folder,key+".jpg");
            lock(gate) {
                if(File.Exists(target))return target;
                Directory.CreateDirectory(folder);
                using(var original=Image.FromFile(source)) {
                    double scale=Math.Min(1,2560.0/Math.Max(original.Width,original.Height));
                    using(var bitmap=new Bitmap(Math.Max(1,(int)Math.Round(original.Width*scale)),Math.Max(1,(int)Math.Round(original.Height*scale)),PixelFormat.Format24bppRgb)) {
                        if(style=="blocks")RenderBlocks(bitmap,palette);
                        else if(style=="original") {
                            using(var graphics=Graphics.FromImage(bitmap))using(var edges=new ImageAttributes()){graphics.Clear(Color.FromArgb(24,25,29));graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;edges.SetWrapMode(WrapMode.TileFlipXY);graphics.DrawImage(original,new Rectangle(0,0,bitmap.Width,bitmap.Height),0,0,original.Width,original.Height,GraphicsUnit.Pixel,edges);}
                        } else {
                        // Blur a small browser-only surface once, rather than filtering a full-screen image every frame.
                        double blurScale=Math.Min(1,768.0/Math.Max(original.Width,original.Height));
                        using(var soft=new Bitmap(Math.Max(1,(int)Math.Round(original.Width*blurScale)),Math.Max(1,(int)Math.Round(original.Height*blurScale)),PixelFormat.Format24bppRgb)) {
                            using(var graphics=Graphics.FromImage(soft)){graphics.Clear(Color.FromArgb(24,25,29));graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.DrawImage(original,new Rectangle(0,0,soft.Width,soft.Height));}
                            Blur(soft,7);
                            using(var graphics=Graphics.FromImage(bitmap))using(var edges=new ImageAttributes()){graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;edges.SetWrapMode(WrapMode.TileFlipXY);graphics.DrawImage(soft,new Rectangle(0,0,bitmap.Width,bitmap.Height),0,0,soft.Width,soft.Height,GraphicsUnit.Pixel,edges);}
                        }
                        }
                        var codec=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
                        using(var settings=new EncoderParameters(1)){settings.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,82L);string temporary=target+".tmp";try{bitmap.Save(temporary,codec,settings);File.Move(temporary,target);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
                    }
                }
                return target;
            }
        }
        internal static void RenderBlocks(Bitmap bitmap,Palette palette) {
            Color background=Palette.ColorOf(palette.Background),accent=Palette.ColorOf(palette.Accent);
            var swatches=(palette.Swatches??new string[0]).Where(s=>!String.IsNullOrEmpty(s)).Select(Palette.ColorOf).ToArray();
            Color dominant=swatches.Length>0?swatches[0]:background,secondary=swatches.Length>2?swatches[2]:accent,detail=swatches.Length>3?swatches[3]:secondary;
            using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Palette.Mix(dominant,background,.12));
                float w=bitmap.Width,h=bitmap.Height;
                // Fixed, generous color fields: no pixel positions, outlines or subjects from the source image.
                using(var shape=new GraphicsPath())using(var brush=new SolidBrush(Palette.Mix(accent,dominant,.22))) {
                    shape.AddBezier(-w*.12f,h*.62f,w*.1f,-h*.05f,w*.51f,h*.12f,w*.64f,h*.68f);
                    shape.AddBezier(w*.64f,h*.68f,w*.7f,h*.98f,w*.84f,h*1.12f,w*1.04f,h*1.18f);
                    shape.AddLine(w*1.04f,h*1.18f,-w*.12f,h*1.18f);shape.CloseFigure();g.FillPath(brush,shape);
                }
                using(var brush=new SolidBrush(Palette.Mix(detail,dominant,.30)))g.FillEllipse(brush,w*.57f,-h*.43f,w*.7f,h*.92f);
                using(var brush=new SolidBrush(Palette.Mix(secondary,dominant,.15)))g.FillEllipse(brush,-w*.29f,h*.70f,w*.78f,h*.92f);
            }
        }
        internal static void Blur(Bitmap bitmap,int radius) {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
            try {
                int width=bitmap.Width,height=bitmap.Height,stride=data.Stride;byte[] pixels=new byte[stride*height],scratch=new byte[pixels.Length];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);int count=radius*2+1;
                for(int pass=0;pass<3;pass++) {
                    for(int y=0;y<height;y++)for(int channel=0;channel<3;channel++) {
                        int sum=0;for(int offset=-radius;offset<=radius;offset++)sum+=pixels[y*stride+Math.Max(0,Math.Min(width-1,offset))*3+channel];
                        for(int x=0;x<width;x++){scratch[y*stride+x*3+channel]=(byte)(sum/count);sum-=pixels[y*stride+Math.Max(0,x-radius)*3+channel];sum+=pixels[y*stride+Math.Min(width-1,x+radius+1)*3+channel];}
                    }
                    for(int x=0;x<width;x++)for(int channel=0;channel<3;channel++) {
                        int sum=0;for(int offset=-radius;offset<=radius;offset++)sum+=scratch[Math.Max(0,Math.Min(height-1,offset))*stride+x*3+channel];
                        for(int y=0;y<height;y++){pixels[y*stride+x*3+channel]=(byte)(sum/count);sum-=scratch[Math.Max(0,y-radius)*stride+x*3+channel];sum+=scratch[Math.Min(height-1,y+radius+1)*stride+x*3+channel];}
                    }
                }
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
            }finally{bitmap.UnlockBits(data);}
        }
    }
}
