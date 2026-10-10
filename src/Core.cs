using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DeskShift {
    public class Palette {
        public string Accent { get; set; }
        public string Background { get; set; }
        public string Surface { get; set; }
        public string Text { get; set; }
        public string[] Swatches { get; set; }
        public bool Dark { get; set; }
        public static Color ColorOf(string s) { return ColorTranslator.FromHtml(s); }
        public static string Hex(Color c) { return String.Format("#{0:X2}{1:X2}{2:X2}", c.R,c.G,c.B); }
        public static Color Mix(Color a, Color b, double t) { return Color.FromArgb((int)(a.R*(1-t)+b.R*t),(int)(a.G*(1-t)+b.G*t),(int)(a.B*(1-t)+b.B*t)); }
        public static double Luminance(Color c) {
            Func<byte,double> linear = v => v/255.0 <= .04045 ? v/255.0/12.92 : Math.Pow((v/255.0+.055)/1.055,2.4);
            return .2126*linear(c.R)+.7152*linear(c.G)+.0722*linear(c.B);
        }
        public static double Contrast(Color a, Color b) { double x=Luminance(a), y=Luminance(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
        public static Palette Extract(string path) {
            var bins = new Dictionary<int,List<int>>(); double brightness=0;
            using(var source=Image.FromFile(path)) using(var bmp=new Bitmap(160,100)) {
                using(var g=Graphics.FromImage(bmp)) { g.InterpolationMode=InterpolationMode.HighQualityBilinear; g.DrawImage(source,0,0,160,100); }
                for(int y=0;y<100;y++) for(int x=0;x<160;x++) {
                    Color c=bmp.GetPixel(x,y); brightness+=Luminance(c);
                    int key=(c.R/16<<8)|(c.G/16<<4)|c.B/16;
                    List<int> bin;
                    if(!bins.TryGetValue(key,out bin)) bins[key]=bin=new List<int>{0,0,0,0};
                    bin[0]++; bin[1]+=c.R;bin[2]+=c.G;bin[3]+=c.B;
                }
            }
            var ranked=bins.Values.OrderByDescending(b=>b[0]).Select(b=>new {Count=b[0],Color=Color.FromArgb(b[1]/b[0],b[2]/b[0],b[3]/b[0])}).ToList();
            var swatches=new List<Color>();
            foreach(var item in ranked) {
                if(swatches.All(c=>Distance(c,item.Color)>35)) swatches.Add(item.Color);
                if(swatches.Count==5) break;
            }
            Color accent=ranked.Where(b=>Luminance(b.Color)>.035 && Luminance(b.Color)<.78).OrderByDescending(b=>Math.Sqrt(b.Count)*(0.18+b.Color.GetSaturation())*Math.Min(1,b.Count/70.0)).Select(b=>b.Color).FirstOrDefault();
            if(accent.IsEmpty) accent=Color.FromArgb(110,125,160);
            bool dark=brightness/16000<.26;
            Color background=Mix(accent,dark?Color.FromArgb(15,18,25):Color.White,dark?.86:.94);
            Color surface=Mix(accent,dark?Color.FromArgb(30,34,44):Color.White,dark?.8:.98);
            return new Palette { Accent=Hex(accent),Background=Hex(background),Surface=Hex(surface),Text=dark?"#F4F5FA":"#222734",Dark=dark,Swatches=swatches.Select(Hex).ToArray() };
        }
        static double Distance(Color a, Color b) { return Math.Sqrt(Math.Pow(a.R-b.R,2)+Math.Pow(a.G-b.G,2)+Math.Pow(a.B-b.B,2)); }
    }
    public class Wallpaper {
        public string Id {get;set;}
        public string Name {get;set;}
        public string File {get;set;}
        public Palette Palette {get;set;}
    }
    public class Settings {
        public List<Wallpaper> Wallpapers {get;set;}
        public string ActiveId {get;set;}
        public Palette ActivePalette {get;set;}
        public bool SystemTheme {get;set;}
        public bool FullscreenTransition {get;set;}
        public string TransitionStyle {get;set;}
        public string Mode {get;set;}
        public bool ShowFloatingOrb {get;set;}
        public bool HasOrbPosition {get;set;}
        public int OrbX {get;set;}
        public int OrbY {get;set;}
        public Settings() { Wallpapers=new List<Wallpaper>();SystemTheme=true;FullscreenTransition=true;TransitionStyle="随机";Mode="自动"; }
    }
    public class Library {
        public readonly string Root;
        public Settings State;
        public static JavaScriptSerializer Json = new JavaScriptSerializer();
        public Library(string root, string assets) {
            Root=root;Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.Combine(Root,"wallpapers"));
            string config=Path.Combine(Root,"library.json");
            if(System.IO.File.Exists(config)) {
                try { State=Json.Deserialize<Settings>(System.IO.File.ReadAllText(config)); if(State==null || State.Wallpapers==null) throw new Exception("无效壁纸库"); }
                catch { System.IO.File.Copy(config,config+".broken-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true); }
            }
            if(State==null) {
                State=new Settings();
                string[] names={"薄荷与紫 · 午后","青绿 · 楼梯","暮蓝 · 光影"};
                var files=Directory.GetFiles(assets).OrderBy(p=>p).ToArray();
                for(int i=0;i<files.Length;i++) Import(files[i],i<names.Length?names[i]:Path.GetFileNameWithoutExtension(files[i]));
            }
            Save();
        }
        public Wallpaper Import(string path,string name) {
            if(new FileInfo(path).Length>60*1024*1024) throw new Exception("图片超过 60 MB，请先缩小后导入。");
            using(var img=Image.FromFile(path)) { if((long)img.Width*img.Height>160000000) throw new Exception("图片尺寸过大。"); }
            var palette=Palette.Extract(path);
            string id=Guid.NewGuid().ToString("N"), dest=Path.Combine(Root,"wallpapers",id+Path.GetExtension(path).ToLowerInvariant());
            System.IO.File.Copy(path,dest);
            var w=new Wallpaper{Id=id,Name=name,File=dest,Palette=palette};State.Wallpapers.Add(w);Save();return w;
        }
        public void Save() { AtomicWrite(Path.Combine(Root,"library.json"),Json.Serialize(State)); }
        public static void AtomicWrite(string path,string data) {
            string temp=path+".tmp";System.IO.File.WriteAllText(temp,data,System.Text.Encoding.UTF8);
            if(System.IO.File.Exists(path)) System.IO.File.Replace(temp,path,null); else System.IO.File.Move(temp,path);
        }
        public Palette Effective(Wallpaper w) {
            var p=Json.Deserialize<Palette>(Json.Serialize(w.Palette));
            if(State.Mode!="自动") {
                p.Dark=State.Mode=="深色";Color a=Palette.ColorOf(p.Accent);
                p.Background=Palette.Hex(Palette.Mix(a,p.Dark?Color.FromArgb(15,18,25):Color.White,p.Dark?.86:.94));
                p.Surface=Palette.Hex(Palette.Mix(a,p.Dark?Color.FromArgb(30,34,44):Color.White,p.Dark?.8:.98));p.Text=p.Dark?"#F4F5FA":"#222734";
            }
            return p;
        }
        public string ExportChrome(Wallpaper w) {
            return ExportBrowser(w,"chrome");
        }
        public string ExportBrowser(Wallpaper w,string browser,Palette palette=null) {
            if(browser!="chrome" && browser!="edge")throw new ArgumentException("Unsupported browser");
            var p=palette??Effective(w);string dir=Path.Combine(Root,"browser-themes",browser,"current");Directory.CreateDirectory(dir);
            Func<string,int[]> rgb=s=>{Color c=Palette.ColorOf(s);return new[]{(int)c.R,(int)c.G,(int)c.B};};
            string frameText=Palette.Contrast(Palette.ColorOf(p.Accent),Color.White)>4.5?"#FFFFFF":"#171A22";
            string toolbar=Palette.Hex(Palette.Mix(Palette.ColorOf(p.Accent),Palette.ColorOf(p.Surface),.82));
            var colors=new Dictionary<string,object>{{"frame",rgb(p.Accent)},{"frame_inactive",rgb(p.Accent)},{"toolbar",rgb(toolbar)},{"tab_text",rgb(p.Text)},{"tab_background_text",rgb(frameText)},{"tab_background_text_inactive",rgb(frameText)},{"background_tab",rgb(p.Accent)},{"background_tab_inactive",rgb(p.Accent)},{"bookmark_text",rgb(p.Text)},{"omnibox_background",rgb(p.Background)},{"omnibox_text",rgb(p.Text)}};
            string manifest=Json.Serialize(new{manifest_version=3,name="DeskShift · "+(browser=="edge"?"Edge":"Chrome")+" 配色",version="1.0.0",description="仅浏览器界面配色；不替换首页、新标签页，不包含壁纸。",theme=new{colors=colors}});
            string target=Path.Combine(dir,"manifest.json");
            if(!File.Exists(target)||File.ReadAllText(target)!=manifest){AtomicWrite(target,manifest);string cache=Path.Combine(dir,"Cached Theme.pak");try{if(File.Exists(cache))File.Delete(cache);}catch(IOException){}catch(UnauthorizedAccessException){}}
            string address=browser=="edge"?"edge://extensions":"chrome://extensions";
            AtomicWrite(Path.Combine(dir,"HOW-TO.html"),"<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>DeskShift 标题栏配色</title><style>body{font:16px 'Segoe UI',sans-serif;background:#f3f6f4;color:#26382e;max-width:700px;margin:70px auto;padding:24px;line-height:1.8}article{background:white;border-radius:24px;padding:32px}code{display:block;background:#eef3ef;border-radius:10px;padding:12px;overflow-wrap:anywhere;user-select:all}.sample{background:"+p.Accent+";color:"+frameText+";padding:18px;border-radius:14px}</style><article><h1>应用当前标题栏配色</h1><p class='sample'>"+System.Net.WebUtility.HtmlEncode(w.Name)+" · "+p.Accent+"</p><p>新标签页扩展只控制页面；标题栏需要单独加载这个主题。导出文件后，浏览器不会自动重新读取。</p><ol><li>在浏览器地址栏打开：<code>"+address+"</code></li><li>打开「开发者模式」，点击「加载解压缩的扩展」（Chrome 为「加载已解压的扩展程序」）。</li><li>选择以下主题文件夹，<strong>不是新标签页扩展文件夹</strong>：<code>"+System.Net.WebUtility.HtmlEncode(dir)+"</code></li></ol><p>如已加载 DeskShift 配色主题，点击该主题的重新加载按钮；如果主题不显示在列表里，就再次加载上面的文件夹。切换壁纸后需重复此步骤。</p><p>此主题只改变标题栏、标签和工具栏配色，新标签页仍由 DeskShift 新标签页扩展显示。</p></article></html>");return dir;
        }
    }
}
