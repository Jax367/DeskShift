using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DeskShift {
    public class Preview : Control {
        public Image Image;
        public Palette Palette;
        Image previousFrame;readonly System.Windows.Forms.Timer animation;readonly Stopwatch elapsed=new Stopwatch();bool working;
        public bool Working {get{return working;}set{working=value;if(value)animation.Start();else if(previousFrame==null)animation.Stop();Invalidate();}}
        public Preview() { DoubleBuffered=true;animation=new System.Windows.Forms.Timer{Interval=16};animation.Tick+=(s,e)=>{if(previousFrame!=null && elapsed.ElapsedMilliseconds>=220){previousFrame.Dispose();previousFrame=null;}if(previousFrame==null && !working)animation.Stop();Invalidate();}; }
        public void SetContent(Image next,Palette palette) {
            Image frame=null;
            if(Image!=null && Width>0 && Height>0 && WindowsTheme.AnimationsEnabled) {frame=new Bitmap(Width,Height);DrawToBitmap((Bitmap)frame,new Rectangle(0,0,Width,Height));}
            if(previousFrame!=null)previousFrame.Dispose();previousFrame=frame;
            if(Image!=null)Image.Dispose();Image=next;Palette=palette;
            if(frame!=null){elapsed.Restart();animation.Start();}Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.FromArgb(225,229,232));
            if(Image!=null) {
                float ratio=Math.Max((float)Width/Image.Width,(float)Height/Image.Height);float w=Image.Width*ratio,h=Image.Height*ratio;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(Image,(Width-w)/2,(Height-h)/2,w,h);
            }
            if(Palette==null) return;
            Color surface=Palette.ColorOf(Palette.Surface),text=Palette.ColorOf(Palette.Text),accent=Palette.ColorOf(Palette.Accent);
            var browser=new Rectangle(Width/10,Height/5,Width*8/10,Height/2);
            using(var brush=new SolidBrush(surface)) g.FillRectangle(brush,browser);
            using(var brush=new SolidBrush(accent)) g.FillRectangle(brush,browser.X,browser.Y,browser.Width,27);
            using(var brush=new SolidBrush(surface)) g.FillRectangle(brush,browser.X+12,browser.Y+6,116,25);
            using(var brush=new SolidBrush(text)) {
                using(var font=new Font("Microsoft YaHei UI",8)) g.DrawString("Chrome 配色预览",font,brush,browser.X+17,browser.Y+9);
                using(var font=new Font("Microsoft YaHei UI",10)) g.DrawString("新的一页，新的心情。",font,brush,browser.X+18,browser.Y+39);
            }
            using(var brush=new SolidBrush(Palette.ColorOf(Palette.Background))) g.FillRectangle(brush,browser.X+18,browser.Bottom-30,browser.Width-36,18);
            using(var brush=new SolidBrush(Color.FromArgb(235,surface))) g.FillRectangle(brush,0,Height-35,Width,35);
            for(int i=0;i<5;i++) using(var brush=new SolidBrush(i==0?accent:Color.FromArgb(95,text))) g.FillRectangle(brush,Width/2-70+i*30,Height-24,17,17);
            if(previousFrame!=null) {
                double t=Math.Min(1,elapsed.Elapsed.TotalMilliseconds/220);float opacity=(float)Math.Pow(1-t,3);
                using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=opacity;attributes.SetColorMatrix(matrix);g.DrawImage(previousFrame,new Rectangle(0,0,Width,Height),0,0,previousFrame.Width,previousFrame.Height,GraphicsUnit.Pixel,attributes);}
            }
            if(working) {
                float t=(Environment.TickCount & int.MaxValue)%1200/1200f;
                using(var brush=new SolidBrush(accent))g.FillRectangle(brush,(Width+70)*t-70,Height-4,70,4);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing){animation.Dispose();if(Image!=null)Image.Dispose();if(previousFrame!=null)previousFrame.Dispose();}base.Dispose(disposing); }
    }
    public class WallpaperCard : Control {
        public Wallpaper Item;public bool Selected,Active;Image thumb;public Bitmap PreviewImage;
        public WallpaperCard(Wallpaper w) { Item=w;Size=new Size(238,206);Margin=new Padding(0,0,16,16);DoubleBuffered=true;Cursor=Cursors.Hand;
            using(var img=Image.FromFile(w.File)) { thumb=new Bitmap(238,134);using(var g=Graphics.FromImage(thumb)) { g.InterpolationMode=InterpolationMode.HighQualityBicubic;float scale=Math.Max(238f/img.Width,134f/img.Height);float width=img.Width*scale,height=img.Height*scale;g.DrawImage(img,(238-width)/2,(134-height)/2,width,height); }float previewScale=Math.Min(960f/img.Width,640f/img.Height);PreviewImage=new Bitmap(img,new Size(Math.Max(1,(int)(img.Width*previewScale)),Math.Max(1,(int)(img.Height*previewScale)))); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Color.White);g.DrawImage(thumb,0,0,Width,134);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var brush=new SolidBrush(Color.FromArgb(35,42,51))) g.DrawString(Item.Name,new Font("Microsoft YaHei UI",10,FontStyle.Bold),brush,12,144);
            for(int i=0;i<Item.Palette.Swatches.Length;i++) using(var brush=new SolidBrush(Palette.ColorOf(Item.Palette.Swatches[i]))) g.FillEllipse(brush,13+i*23,177,14,14);
            using(var brush=new SolidBrush(Color.FromArgb(115,121,131))) g.DrawString(Item.Palette.Dark?"深色":"浅色",new Font("Microsoft YaHei UI",8),brush,191,177);
            if(Active) { using(var brush=new SolidBrush(Color.FromArgb(230,255,255,255))) g.FillRectangle(brush,151,9,77,23);using(var brush=new SolidBrush(Color.FromArgb(41,76,59))) g.DrawString("正在使用",new Font("Microsoft YaHei UI",8),brush,160,13); }
            using(var pen=new Pen(Selected?Color.FromArgb(97,120,104):Color.FromArgb(222,226,225),Selected?3:1)) g.DrawRectangle(pen,1,1,Width-3,Height-3);
        }
        protected override void Dispose(bool disposing) { if(disposing){thumb.Dispose();PreviewImage.Dispose();}base.Dispose(disposing); }
    }
    public class MainForm : Form {
        Library library;Bridge bridge;Wallpaper selected;FlowLayoutPanel cards;Preview preview;Label title,subtitle,paletteLabel,status,connection;Button apply;CheckBox system,fullscreen;ComboBox mode;NotifyIcon tray;bool quitting;bool busy;DesktopTransition transition;Task startupWarmup=Task.FromResult(true);
        static Color Ink=Color.FromArgb(36,43,39),Muted=Color.FromArgb(114,122,117),Green=Color.FromArgb(81,108,92);
        public MainForm(Library data,bool smoke) {
            library=data;Text="DeskShift · 桌面随心换";Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(246,247,243);ForeColor=Ink;ClientSize=new Size(1240,820);MinimumSize=new Size(1100,750);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(30)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,350));Controls.Add(root);
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Margin=new Padding(0,0,25,0)};left.RowStyles.Add(new RowStyle(SizeType.Absolute,88));left.RowStyles.Add(new RowStyle(SizeType.Absolute,62));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,48));left.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.Controls.Add(left,0,0);
            var heading=new Panel{Dock=DockStyle.Fill};heading.Controls.Add(Label("DeskShift",25,FontStyle.Bold,new Point(0,0),new Size(400,44)));heading.Controls.Add(Label("让整个桌面，跟随一张壁纸。",10,FontStyle.Regular,new Point(1,49),new Size(500,28)));left.Controls.Add(heading,0,0);
            var bar=new Panel{Dock=DockStyle.Fill};bar.Controls.Add(Label("我的壁纸",13,FontStyle.Bold,new Point(0,14),new Size(160,30)));var import=Button("＋ 添加壁纸",false);import.SetBounds(0,7,138,40);import.Anchor=AnchorStyles.Right|AnchorStyles.Top;bar.Controls.Add(import);bar.Resize+=(s,e)=>import.Left=bar.Width-import.Width;import.Click+=(s,e)=>Import();left.Controls.Add(bar,0,1);
            cards=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=BackColor,Padding=new Padding(2),WrapContents=true};left.Controls.Add(cards,0,2);
            var tools=new FlowLayoutPanel{Dock=DockStyle.Fill};var next=Button("下一张",false);var undo=Button("撤销上次切换",false);var folder=Button("打开数据目录",false);tools.Controls.AddRange(new Control[]{next,undo,folder});next.Click+=(s,e)=>Next();undo.Click+=(s,e)=>Undo();folder.Click+=(s,e)=>Open(library.Root);left.Controls.Add(tools,0,3);
            status=Label("选择壁纸，预览配色后点击一键应用。",9,FontStyle.Regular,new Point(),new Size());status.Dock=DockStyle.Fill;left.Controls.Add(status,0,4);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=13,BackColor=Color.White,Padding=new Padding(20)};int[] heights={32,40,180,35,47,38,42,45,36,60,48,48};foreach(int h in heights) right.RowStyles.Add(new RowStyle(SizeType.Absolute,h));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(right,1,0);
            right.Controls.Add(Label("桌面预览",12,FontStyle.Bold,new Point(),new Size(280,30)),0,0);title=Label("",11,FontStyle.Bold,new Point(),new Size(280,32));right.Controls.Add(title,0,1);
            preview=new Preview{Dock=DockStyle.Fill,Margin=new Padding(0)};right.Controls.Add(preview,0,2);
            subtitle=Label("",9,FontStyle.Regular,new Point(),new Size(300,32));right.Controls.Add(subtitle,0,3);
            var accent=Button("调整主色",false);accent.Dock=DockStyle.Fill;accent.Click+=(s,e)=>ChangeAccent();right.Controls.Add(accent,0,4);
            paletteLabel=Label("",9,FontStyle.Regular,new Point(),new Size(300,32));right.Controls.Add(paletteLabel,0,5);
            var modePanel=new FlowLayoutPanel{Dock=DockStyle.Fill};modePanel.Controls.Add(Label("明暗模式",10,FontStyle.Regular,new Point(),new Size(100,30)));mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=165};mode.Items.AddRange(new object[]{"自动","浅色","深色"});mode.SelectedItem=library.State.Mode;modePanel.Controls.Add(mode);right.Controls.Add(modePanel,0,6);mode.SelectedIndexChanged+=(s,e)=>{library.State.Mode=mode.Text;library.Save();ShowSelected();};
            var effects=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};system=new CheckBox{Text="同步 Windows 配色",Checked=library.State.SystemTheme,Width=176,Height=35,Margin=new Padding(0)};system.CheckedChanged+=(s,e)=>{library.State.SystemTheme=system.Checked;library.Save();};fullscreen=new CheckBox{Text="全屏过渡",Checked=library.State.FullscreenTransition,Width=110,Height=35,Margin=new Padding(0)};fullscreen.CheckedChanged+=(s,e)=>{library.State.FullscreenTransition=fullscreen.Checked;library.Save();};effects.Controls.AddRange(new Control[]{system,fullscreen});right.Controls.Add(effects,0,7);
            var effectPicker=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};effectPicker.Controls.Add(Label("过渡效果",9,FontStyle.Regular,new Point(),new Size(100,30)));var styleBox=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=165};styleBox.Items.AddRange(DesktopTransition.Styles);styleBox.SelectedItem=library.State.TransitionStyle;if(styleBox.SelectedIndex<0)styleBox.SelectedIndex=0;styleBox.SelectedIndexChanged+=(s,e)=>{library.State.TransitionStyle=styleBox.Text;library.Save();};effectPicker.Controls.Add(styleBox);right.Controls.Add(effectPicker,0,8);
            connection=Label("Chrome 新标签页：安装扩展后自动同步\n完整标签栏主题：导出后手动加载\n程序需在后台运行以保持新标签页联动。",9,FontStyle.Regular,new Point(),new Size(300,60));right.Controls.Add(connection,0,9);
            apply=Button("一键应用桌面",true);apply.Dock=DockStyle.Fill;apply.Click+=(s,e)=>Apply();right.Controls.Add(apply,0,10);
            var chrome=Button("导出 Chrome 完整主题",false);chrome.Dock=DockStyle.Fill;chrome.Click+=(s,e)=>{if(selected==null)return;try { string dir=library.ExportChrome(selected);Open(dir);Status("主题已导出。在 chrome://extensions 开启开发者模式，加载此文件夹。"); }catch(Exception ex){Error(ex);} };right.Controls.Add(chrome,0,11);
            var help=Button("Chrome 扩展安装说明",false);help.Dock=DockStyle.Top;help.Click+=(s,e)=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser","chrome","INSTALL.html"));right.Controls.Add(help,0,12);
            AllowDrop=true;DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=(s,e)=>ImportFiles((string[])e.Data.GetData(DataFormats.FileDrop));
            RefreshCards();selected=library.State.Wallpapers.FirstOrDefault(w=>w.Id==library.State.ActiveId)??library.State.Wallpapers.FirstOrDefault();ShowSelected();
            if(!smoke) {
                Shown+=(s,e)=>{if(library.State.FullscreenTransition && WindowsTheme.AnimationsEnabled && selected!=null)startupWarmup=WarmTransitions(library.Effective(selected));};
                FormClosed+=(s,e)=>{TransitionWindowPool.DisposeIdle();AnimationResources.ClearUnused();};
                try {bridge=new Bridge();Publish();}catch(Exception ex){Status("本机浏览器连接未启动："+ex.Message);}
                tray=new NotifyIcon{Icon=Icon,Text="DeskShift · 双击打开",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add("打开 DeskShift",null,(s,e)=>ShowWindow());
                var switchMenu=new ToolStripMenuItem("切换壁纸");menu.Items.Add(switchMenu);
                var nextMenu=menu.Items.Add("下一张壁纸",null,(s,e)=>Next());var undoMenu=menu.Items.Add("撤销上次切换",null,(s,e)=>Undo());
                menu.Opening+=(s,e)=>{
                    foreach(ToolStripItem item in switchMenu.DropDownItems.Cast<ToolStripItem>().ToArray())item.Dispose();switchMenu.DropDownItems.Clear();
                    foreach(var wallpaper in library.State.Wallpapers){var target=wallpaper;var item=new ToolStripMenuItem(target.Name.Replace("&","&&")){Checked=target.Id==library.State.ActiveId,Enabled=!busy,ToolTipText="切换壁纸并同步当前设置的配色"};item.Click+=(sender,args)=>SwitchWallpaper(target);switchMenu.DropDownItems.Add(item);}
                    switchMenu.Enabled=!busy && switchMenu.DropDownItems.Count>0;nextMenu.Enabled=!busy && library.State.Wallpapers.Count>0;undoMenu.Enabled=!busy;
                };
                menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>{quitting=true;Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>ShowWindow();
                FormClosing+=(s,e)=>{if(quitting && busy){e.Cancel=true;quitting=false;Status("正在完成切换，请稍后退出。");return;}if(!quitting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2000,"DeskShift","已在托盘运行，右键图标可以退出。",ToolTipIcon.Info);}};
                var timer=new System.Windows.Forms.Timer{Interval=2500};timer.Tick+=(s,e)=>{connection.Text="Chrome 新标签页："+(bridge!=null && (DateTime.UtcNow-bridge.LastSeen).TotalSeconds<8?"已连接，自动同步":"等待扩展连接")+"\n完整标签栏主题：导出后手动加载\n程序需在后台运行以保持联动。";};timer.Start();FormClosed+=(s,e)=>{timer.Dispose();if(bridge!=null)bridge.Dispose();tray.Dispose();menu.Dispose();};
            }
        }
        static Label Label(string text,float size,FontStyle style,Point point,Size bounds) {return new Label{Text=text,Font=new Font("Microsoft YaHei UI",size,style),Location=point,Size=bounds,ForeColor=style==FontStyle.Bold?Ink:Muted,AutoEllipsis=true};}
        static Button Button(string text,bool primary) {var b=new Button{Text=text,Height=38,Width=145,FlatStyle=FlatStyle.Flat,BackColor=primary?Green:Color.FromArgb(242,245,240),ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Margin=new Padding(0,3,9,3)};b.FlatAppearance.BorderSize=0;return b;}
        void RefreshCards() { foreach(Control c in cards.Controls.Cast<Control>().ToArray()) c.Dispose();cards.Controls.Clear();foreach(var w in library.State.Wallpapers){var card=new WallpaperCard(w);card.Click+=(s,e)=>{selected=card.Item;ShowSelected();};cards.Controls.Add(card);} }
        void ShowSelected() {
            if(selected==null) {apply.Enabled=false;return;}var p=library.Effective(selected);title.Text=selected.Name;
            var cardForPreview=cards.Controls.Cast<WallpaperCard>().First(c=>c.Item.Id==selected.Id);preview.SetContent(new Bitmap(cardForPreview.PreviewImage),p);
            subtitle.Text="自动识别 · "+(p.Dark?"深色方案":"浅色方案")+" · 主色 "+p.Accent;paletteLabel.Text="可点击「调整主色」微调取色结果";
            foreach(WallpaperCard card in cards.Controls){card.Selected=card.Item.Id==selected.Id;card.Active=card.Item.Id==library.State.ActiveId;card.Invalidate();}
        }
        void ChangeAccent() {if(selected==null)return;using(var dialog=new ColorDialog{Color=Palette.ColorOf(selected.Palette.Accent),FullOpen=true})if(dialog.ShowDialog()==DialogResult.OK){selected.Palette.Accent=Palette.Hex(dialog.Color); // Recompute surfaces without losing image's auto mode.
            bool dark=selected.Palette.Dark;selected.Palette.Background=Palette.Hex(Palette.Mix(dialog.Color,dark?Color.FromArgb(15,18,25):Color.White,dark?.86:.94));selected.Palette.Surface=Palette.Hex(Palette.Mix(dialog.Color,dark?Color.FromArgb(30,34,44):Color.White,dark?.8:.98));library.Save();ShowSelected();}}
        void Import() {using(var d=new OpenFileDialog{Filter="壁纸图片|*.jpg;*.jpeg;*.png;*.bmp",Multiselect=true})if(d.ShowDialog()==DialogResult.OK)ImportFiles(d.FileNames);}
        async void ImportFiles(string[] files) {
            if(busy)return;SetBusy(true);Status("正在读取图片并提取配色…");Wallpaper last=null;var failures=new System.Collections.Generic.List<string>();
            try {await Task.Run(()=>{foreach(string file in files)try{last=library.Import(file,Path.GetFileNameWithoutExtension(file));}catch(Exception ex){failures.Add(Path.GetFileName(file)+"："+ex.Message);}});if(last!=null)selected=last;RefreshCards();ShowSelected();Status(failures.Count>0?String.Join("；",failures):"已导入 "+files.Length+" 张壁纸，配色识别完成。");}finally{SetBusy(false);}
        }
        void SetBusy(bool value) {busy=value;Controls[0].Enabled=!value;preview.Working=value;apply.Text=value?"正在应用，请稍候…":"一键应用桌面";}
        internal static Task RunBackground(Action action) {
            var completion=new TaskCompletionSource<bool>();var thread=new Thread(()=>{try{action();completion.SetResult(true);}catch(Exception ex){completion.SetException(ex);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
        }
        void Progress(string text) {if(!IsDisposed && IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)Status(text);}));}
        async Task WarmTransitions(Palette palette){try{await TransitionWindowPool.WarmUpAsync(palette);}catch{/* A failed idle warmup must not prevent wallpaper switching. */}}
        async Task WithTransition(Palette palette,Func<Task> action) {
            if(!library.State.FullscreenTransition){await action();return;}
            await startupWarmup;
            using(var effect=new DesktopTransition(palette,library.State.TransitionStyle)) {
                transition=effect;
                try {
                    Exception failure=null;
                    try{await effect.CoverAsync();await action();}catch(Exception ex){failure=ex;}
                    await effect.RevealAsync();if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                }finally{transition=null;}
            }
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {if(keyData==Keys.Escape && transition!=null){transition.Dismiss();return true;}return base.ProcessCmdKey(ref msg,keyData);}
        async void Apply() {
            if(selected==null || busy)return;var wallpaper=selected;var palette=library.Effective(wallpaper);bool syncSystem=system.Checked;
            SetBusy(true);Status("正在应用桌面…");
            try{Status("正在后台准备原尺寸图片与主题…");PreparedTheme prepared=null;await RunBackground(()=>prepared=WindowsTheme.Prepare(library,wallpaper,palette,syncSystem));await WithTransition(palette,async()=>{await RunBackground(()=>{WindowsTheme.Apply(library,prepared,Progress);library.State.ActiveId=wallpaper.Id;library.State.ActivePalette=palette;library.Save();});Publish();ShowSelected();await Task.Delay(120);});Status("壁纸已切换"+(syncSystem?"，Windows 主题已提交":"")+"；已向 Chrome 新标签页发布配色。");}catch(Exception ex){Error(ex);}finally{SetBusy(false);}
        }
        void Publish() {if(bridge==null)return;var w=library.State.Wallpapers.FirstOrDefault(x=>x.Id==library.State.ActiveId);if(w!=null)bridge.Publish(w,library.State.ActivePalette??library.Effective(w));else bridge.Clear();}
        void Next() {if(busy || library.State.Wallpapers.Count==0)return;int i=library.State.Wallpapers.FindIndex(w=>w.Id==library.State.ActiveId);selected=library.State.Wallpapers[(i+1)%library.State.Wallpapers.Count];ShowSelected();Apply();}
        void SwitchWallpaper(Wallpaper wallpaper){if(busy || !library.State.Wallpapers.Contains(wallpaper))return;selected=wallpaper;ShowSelected();Apply();}
        async void Undo() {if(busy)return;SetBusy(true);Status("正在恢复上次切换前的设置…");try{var palette=library.State.ActivePalette??(selected==null?new Palette{Accent="#718576",Background="#F5F6F1",Text="#252B28"}:library.Effective(selected));await WithTransition(palette,async()=>{await RunBackground(()=>WindowsTheme.Undo(library));Publish();ShowSelected();});Status("已恢复上次切换前的壁纸与系统设置。");}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
        void Status(string s) {status.Text=s;}
        void Error(Exception ex) {Status(ex.Message);MessageBox.Show(this,ex.Message,"DeskShift",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        static void Open(string path) {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
        void ShowWindow() {Show();WindowState=FormWindowState.Normal;Activate();}
    }
    static class Program {
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Contains("--self-test")){
                // Keep a real UI message loop alive while hidden-window tests await first paint.
                using(var dispatcher=new Control()){var handle=dispatcher.Handle;dispatcher.BeginInvoke((Action)(()=>{try{SelfTest.Run();Environment.Exit(0);}catch(Exception e){System.IO.File.WriteAllText("test-output/failure.txt",e.ToString());Environment.Exit(1);}}));Application.Run();}return;
            }
            bool smoke=args.Contains("--smoke");
            using(var mutex=new Mutex(true,"Local\\DeskShift-Desktop-1",out created)) {
                if(!created && !smoke){MessageBox.Show("DeskShift 已在运行，请双击系统托盘图标打开。","DeskShift");return;}
                try {string root=smoke?Path.Combine(Directory.GetCurrentDirectory(),"test-output","smoke-data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeskShift");var library=new Library(root,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","wallpapers"));
                    using(var form=new MainForm(library,smoke)) {if(smoke){form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(Directory.GetCurrentDirectory(),"test-output","preview.png"));}form.Close();}else Application.Run(form);}
                }catch(Exception e){MessageBox.Show(e.Message,"DeskShift 启动失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
        static bool created;
    }
}
