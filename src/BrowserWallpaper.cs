using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DeskShift {
    // Browser-only derivative. Never overwrite the desktop original or its BMP cache.
    public static class BrowserWallpaper {
        static readonly object gate=new object();
        public static string Prepare(string source) {
            var info=new FileInfo(source);
            string key;using(var hash=SHA256.Create())key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(info.FullName+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|2560-q82-v1"))).Replace("-","");
            string folder=Path.Combine(info.DirectoryName,"browser-cache"),target=Path.Combine(folder,key+".jpg");
            lock(gate) {
                if(File.Exists(target))return target;
                Directory.CreateDirectory(folder);
                using(var original=Image.FromFile(source)) {
                    double scale=Math.Min(1,2560.0/Math.Max(original.Width,original.Height));
                    using(var bitmap=new Bitmap(Math.Max(1,(int)Math.Round(original.Width*scale)),Math.Max(1,(int)Math.Round(original.Height*scale)),PixelFormat.Format24bppRgb)) {
                        using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.FromArgb(24,25,29));graphics.CompositingMode=CompositingMode.SourceOver;graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.DrawImage(original,new Rectangle(0,0,bitmap.Width,bitmap.Height));}
                        var codec=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
                        using(var settings=new EncoderParameters(1)){settings.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,82L);string temporary=target+".tmp";try{bitmap.Save(temporary,codec,settings);File.Move(temporary,target);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
                    }
                }
                return target;
            }
        }
    }
}
