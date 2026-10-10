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
            using(var brush=new SolidBrush(Color.FromArgb(225,surface)))g.FillRectangle(brush,0,Height-30,Width,30);
            for(int i=0;i<Math.Min(5,Palette.Swatches==null?0:Palette.Swatches.Length);i++)using(var brush=new SolidBrush(Palette.ColorOf(Palette.Swatches[i])))g.FillEllipse(brush,Width/2-49+i*22,Height-21,12,12);
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
        public WallpaperCard(Wallpaper w) { Item=w;Size=new Size(206,178);Margin=new Padding(0,0,12,12);DoubleBuffered=true;Cursor=Cursors.Hand;
            using(var img=Image.FromFile(w.File)) { thumb=new Bitmap(238,134);using(var g=Graphics.FromImage(thumb)) { g.InterpolationMode=InterpolationMode.HighQualityBicubic;float scale=Math.Max(238f/img.Width,134f/img.Height);float width=img.Width*scale,height=img.Height*scale;g.DrawImage(img,(238-width)/2,(134-height)/2,width,height); }float previewScale=Math.Min(960f/img.Width,640f/img.Height);PreviewImage=new Bitmap(img,new Size(Math.Max(1,(int)(img.Width*previewScale)),Math.Max(1,(int)(img.Height*previewScale)))); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Parent==null?Color.FromArgb(247,248,250):Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Interface.Round(new RectangleF(1,1,Width-3,Height-3),12))using(var brush=new SolidBrush(Color.White)){g.FillPath(brush,path);using(var pen=new Pen(Selected?Color.FromArgb(97,120,104):Color.FromArgb(225,230,227),Selected?2:1))g.DrawPath(pen,path);}
            var state=g.Save();using(var path=Interface.Round(new RectangleF(8,8,Width-16,106),7)){g.SetClip(path);g.DrawImage(thumb,8,8,Width-16,106);}g.Restore(state);
            using(var brush=new SolidBrush(Color.FromArgb(35,42,51)))using(var font=new Font("Microsoft YaHei UI",9,FontStyle.Bold))using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(Item.Name,font,brush,new RectangleF(12,123,Width-24,24),format);
            for(int i=0;i<Math.Min(5,Item.Palette.Swatches.Length);i++)using(var brush=new SolidBrush(Palette.ColorOf(Item.Palette.Swatches[i])))g.FillEllipse(brush,13+i*20,153,11,11);
            using(var brush=new SolidBrush(Color.FromArgb(115,121,131)))using(var font=new Font("Microsoft YaHei UI",8))g.DrawString(Item.Palette.Dark?"深色":"浅色",font,brush,Width-44,150);
            if(Active){using(var brush=new SolidBrush(Color.FromArgb(235,255,255,255)))g.FillRectangle(brush,Width-82,14,66,22);using(var brush=new SolidBrush(Color.FromArgb(41,76,59)))using(var font=new Font("Microsoft YaHei UI",8))g.DrawString("正在使用",font,brush,Width-76,17);}
        }
        protected override void Dispose(bool disposing) { if(disposing){thumb.Dispose();PreviewImage.Dispose();}base.Dispose(disposing); }
    }
    public class MainForm : Form {
        Library library;Bridge bridge;Wallpaper selected;FlowLayoutPanel cards;Preview preview;Label title,subtitle,paletteLabel,status,connection;Button apply;CheckBox system,fullscreen;ComboBox mode;NotifyIcon tray;bool quitting;bool busy;DesktopTransition transition;Task startupWarmup=Task.FromResult(true);
        Button[] navigation;CheckBox floating;FloatingLauncher launcher;readonly bool smokeMode;Label edgeConnection;
        static Color Ink=Color.FromArgb(36,43,39),Muted=Color.FromArgb(114,122,117),Green=Color.FromArgb(81,108,92);
        public MainForm(Library data,bool smoke) {
            library=data;smokeMode=smoke;Text="DeskShift";Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(247,248,250);ForeColor=Ink;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);StartPosition=FormStartPosition.CenterScreen;
            var work=Screen.FromPoint(Cursor.Position).WorkingArea;ClientSize=new Size(Math.Min(940,work.Width-80),Math.Min(660,work.Height-100));MinimumSize=new Size(Math.Min(800,work.Width-40),Math.Min(570,work.Height-50));
            BuildInterface();
            AllowDrop=true;DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=(s,e)=>ImportFiles((string[])e.Data.GetData(DataFormats.FileDrop));
            RefreshCards();selected=library.State.Wallpapers.FirstOrDefault(w=>w.Id==library.State.ActiveId)??library.State.Wallpapers.FirstOrDefault();ShowSelected();
            if(!smoke) {
                Shown+=(s,e)=>{UpdateFloatingOrb();if(library.State.FullscreenTransition && WindowsTheme.AnimationsEnabled && selected!=null)startupWarmup=WarmTransitions(library.Effective(selected));};
                FormClosed+=(s,e)=>{if(launcher!=null)launcher.Dispose();TransitionWindowPool.DisposeIdle();AnimationResources.ClearUnused();};
                try{bridge=new Bridge();UpdateBrowserThemes();}catch(Exception ex){Status("浏览器连接未启动："+ex.Message);}
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
                var browserTimer=new System.Windows.Forms.Timer{Interval=2500};browserTimer.Tick+=(s,e)=>UpdateBrowserConnection();browserTimer.Start();FormClosed+=(s,e)=>{browserTimer.Dispose();if(bridge!=null)bridge.Dispose();tray.Dispose();menu.Dispose();};
            }
        }
        static Label Label(string text,float size,FontStyle style,Point point,Size bounds) {return new Label{Text=text,Font=new Font("Microsoft YaHei UI",size,style),Location=point,Size=bounds,ForeColor=style==FontStyle.Bold?Ink:Muted,AutoEllipsis=true};}
        static Button Button(string text,bool primary) {return new SoftButton{Text=text,Height=36,Width=132,BackColor=primary?Green:Color.FromArgb(236,240,237),ForeColor=primary?Color.White:Ink,Margin=new Padding(0,3,9,3)};}
        void BuildInterface(){
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,174));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
            var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(233,237,235),Margin=new Padding(0)};root.Controls.Add(sidebar,0,0);
            var brand=Label("DeskShift",19,FontStyle.Bold,new Point(20,30),new Size(145,34));sidebar.Controls.Add(brand);sidebar.Controls.Add(Label("桌面随心换",9,FontStyle.Regular,new Point(22,68),new Size(135,24)));
            var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(24,20,24,12),Margin=new Padding(0)};main.RowStyles.Add(new RowStyle(SizeType.Absolute,68));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,80));root.Controls.Add(main,1,0);
            var heading=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};var pageTitle=Label("壁纸与配色",19,FontStyle.Bold,new Point(0,0),new Size(470,34));var pageDescription=Label("挑选一张喜欢的壁纸，让颜色自然延伸。",9,FontStyle.Regular,new Point(1,38),new Size(560,24));heading.Controls.AddRange(new Control[]{pageTitle,pageDescription});main.Controls.Add(heading,0,0);
            var pages=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};main.Controls.Add(pages,0,1);
            var wallpaperPage=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0)};wallpaperPage.RowStyles.Add(new RowStyle(SizeType.Absolute,196));wallpaperPage.RowStyles.Add(new RowStyle(SizeType.Absolute,48));wallpaperPage.RowStyles.Add(new RowStyle(SizeType.Percent,100));pages.Controls.Add(wallpaperPage);
            var hero=new Surface{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8)};wallpaperPage.Controls.Add(hero,0,0);
            preview=new Preview{Location=new Point(14,14),Size=new Size(254,158)};hero.Controls.Add(preview);title=Label("",12,FontStyle.Bold,new Point(288,25),new Size(370,28));subtitle=Label("",9,FontStyle.Regular,new Point(288,62),new Size(370,28));paletteLabel=Label("",9,FontStyle.Regular,new Point(288,95),new Size(370,24));hero.Controls.AddRange(new Control[]{title,subtitle,paletteLabel});var accent=Button("调整主色",false);accent.Location=new Point(288,133);accent.Click+=(s,e)=>ChangeAccent();hero.Controls.Add(accent);
            var bar=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};bar.Controls.Add(Label("我的壁纸",11,FontStyle.Bold,new Point(0,9),new Size(180,28)));var import=Button("＋ 添加壁纸",false);import.Location=new Point(0,3);import.Anchor=AnchorStyles.Top|AnchorStyles.Right;bar.Controls.Add(import);bar.Resize+=(s,e)=>import.Left=bar.Width-import.Width;import.Click+=(s,e)=>Import();wallpaperPage.Controls.Add(bar,0,1);
            cards=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=BackColor,Padding=new Padding(1),WrapContents=true,Margin=new Padding(0)};wallpaperPage.Controls.Add(cards,0,2);
            var settingsPage=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=new Padding(0)};pages.Controls.Add(settingsPage);
            mode=Interface.Picker(148);mode.Items.AddRange(new object[]{"自动","浅色","深色"});mode.SelectedItem=library.State.Mode;if(mode.SelectedIndex<0)mode.SelectedIndex=0;mode.SelectedIndexChanged+=(s,e)=>{library.State.Mode=mode.Text;library.Save();ShowSelected();};
            system=new Toggle{Checked=library.State.SystemTheme,AccessibleName="同步 Windows 配色"};system.CheckedChanged+=(s,e)=>{library.State.SystemTheme=system.Checked;library.Save();};
            fullscreen=new Toggle{Checked=library.State.FullscreenTransition,AccessibleName="全屏过渡"};fullscreen.CheckedChanged+=(s,e)=>{library.State.FullscreenTransition=fullscreen.Checked;library.Save();};
            var styleBox=Interface.Picker(148);styleBox.Items.AddRange(DesktopTransition.Styles);styleBox.SelectedItem=library.State.TransitionStyle;if(styleBox.SelectedIndex<0)styleBox.SelectedIndex=0;styleBox.SelectedIndexChanged+=(s,e)=>{library.State.TransitionStyle=styleBox.Text;library.Save();};
            var appearance=SettingsGroup("外观",new[]{SettingRow("明暗模式","自动跟随壁纸，也可以固定明暗。",mode),SettingRow("同步 Windows 配色","同时更新系统强调色与明暗模式。",system)});settingsPage.Controls.Add(appearance);
            var motion=SettingsGroup("切换体验",new[]{SettingRow("全屏过渡","覆盖桌面后切换，准备完成再揭幕。",fullscreen),SettingRow("过渡效果","随机展示，或选择你喜欢的一种。",styleBox)});settingsPage.Controls.Add(motion);
            floating=new Toggle{Checked=library.State.ShowFloatingOrb,AccessibleName="显示桌面悬浮球"};floating.CheckedChanged+=(s,e)=>{library.State.ShowFloatingOrb=floating.Checked;library.Save();UpdateFloatingOrb();};var shortcuts=SettingsGroup("桌面快捷入口",new[]{SettingRow("显示桌面悬浮球","点击挑选壁纸，拖动可调整位置。",floating)});settingsPage.Controls.Add(shortcuts);settingsPage.Controls.SetChildIndex(shortcuts,1);
            var undo=Button("撤销上次切换",false);undo.Click+=(s,e)=>Undo();var folder=Button("打开数据目录",false);folder.Click+=(s,e)=>Open(library.Root);var actions=new FlowLayoutPanel{Height=46,Width=640,Margin=new Padding(0,5,0,0)};actions.Controls.AddRange(new Control[]{undo,folder});settingsPage.Controls.Add(actions);
            var browserPage=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=new Padding(0)};pages.Controls.Add(browserPage);
            connection=Label("新标签页壁纸与配色 · 常用网址\n等待扩展连接",9,FontStyle.Regular,new Point(18,52),new Size(590,42));edgeConnection=Label("新标签页壁纸与配色 · 常用网址\n等待扩展连接",9,FontStyle.Regular,new Point(18,52),new Size(590,42));browserPage.Controls.Add(BrowserCard("Google Chrome",connection,"chrome"));browserPage.Controls.Add(BrowserCard("Microsoft Edge",edgeConnection,"edge"));
            browserPage.Controls.Add(Label("新标签页只保留搜索和常用网址，不显示时间、日期。",9,FontStyle.Regular,new Point(),new Size(600,32)));browserPage.Controls.Add(Label("安装扩展会替换新标签页；标签栏配色仍需手动加载主题。",9,FontStyle.Regular,new Point(),new Size(620,32)));
            Action resizeGroups=()=>{int width=Math.Max(480,pages.ClientSize.Width-20);foreach(Control item in settingsPage.Controls)item.Width=width;foreach(Control item in browserPage.Controls)item.Width=width;};pages.Resize+=(s,e)=>resizeGroups();
            hero.Resize+=(s,e)=>{title.Width=subtitle.Width=paletteLabel.Width=Math.Max(120,hero.Width-308);};
            string[] names={"壁纸与配色","切换设置","浏览器联动"};string[] descriptions={"挑选一张喜欢的壁纸，让颜色自然延伸。","把外观与切换方式，调整成你的习惯。","壁纸、搜索、常用网址，让新标签页保持简洁。"};Control[] views={wallpaperPage,settingsPage,browserPage};var nav=new Button[3];navigation=nav;
            for(int i=0;i<3;i++){int index=i;nav[i]=Button(names[i],false);nav[i].SetBounds(12,126+i*48,150,38);nav[i].BackColor=i==0?Color.FromArgb(213,224,217):sidebar.BackColor;nav[i].Click+=(s,e)=>{for(int j=0;j<3;j++){views[j].Visible=j==index;nav[j].BackColor=j==index?Color.FromArgb(213,224,217):sidebar.BackColor;}views[index].BringToFront();pageTitle.Text=names[index];pageDescription.Text=descriptions[index];resizeGroups();};sidebar.Controls.Add(nav[i]);}
            var note=Label("本地处理 · 保留原图",8,FontStyle.Regular,new Point(18,0),new Size(150,22));sidebar.Controls.Add(note);sidebar.Layout+=(s,e)=>note.Top=Math.Max(0,sidebar.ClientSize.Height-40);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0),Padding=new Padding(0,12,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,176));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,40));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.Controls.Add(footer,0,2);
            var next=Button("下一张",false);next.Click+=(s,e)=>Next();footer.Controls.Add(next,0,0);apply=Button("应用桌面",true);apply.Width=176;apply.Dock=DockStyle.Fill;apply.Margin=new Padding(0);apply.Click+=(s,e)=>Apply();footer.Controls.Add(apply,1,0);status=Label("选择壁纸后，点击应用桌面。",8,FontStyle.Regular,new Point(),new Size());status.Dock=DockStyle.Fill;status.Margin=new Padding(0,7,0,0);footer.Controls.Add(status,0,1);footer.SetColumnSpan(status,2);
        }
        static Surface SettingsGroup(string caption,Panel[] rows){var card=new Surface{Width=660,Height=42+rows.Sum(r=>r.Height),Margin=new Padding(0,0,0,14)};card.Controls.Add(Label(caption,11,FontStyle.Bold,new Point(18,14),new Size(330,26)));int y=42;foreach(var row in rows){row.Location=new Point(16,y);row.Width=card.Width-32;row.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;card.Controls.Add(row);y+=row.Height;}return card;}
        Surface BrowserCard(string name,Label state,string browser){var card=new Surface{Width=660,Height=144,Margin=new Padding(0,0,0,14)};card.Controls.Add(Label(name,12,FontStyle.Bold,new Point(18,17),new Size(360,27)));card.Controls.Add(state);var install=Button("安装 "+(browser=="edge"?"Edge":"Chrome")+" 扩展",true);install.Location=new Point(18,96);install.Click+=(s,e)=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser",browser,"INSTALL.html"));card.Controls.Add(install);var export=Button("标题栏配色指南",false);export.Location=new Point(162,96);export.Click+=(s,e)=>{var active=library.State.Wallpapers.FirstOrDefault(w=>w.Id==library.State.ActiveId);var wallpaper=active??selected;if(wallpaper==null)return;try{string folder=library.ExportBrowser(wallpaper,browser,active==null?null:library.State.ActivePalette);Open(Path.Combine(folder,"HOW-TO.html"));Status(name+" 标题栏主题已准备，请按指南手动加载。");}catch(Exception ex){Error(ex);}};card.Controls.Add(export);return card;}
        void UpdateBrowserConnection(){var now=DateTime.UtcNow;connection.Text="新标签页壁纸与配色 · 常用网址\n"+(bridge!=null && (now-bridge.LastSeenChrome).TotalSeconds<8?"新标签页已连接，自动同步":"等待扩展连接");edgeConnection.Text="新标签页壁纸与配色 · 常用网址\n"+(bridge!=null && (now-bridge.LastSeenEdge).TotalSeconds<8?"新标签页已连接，自动同步":"等待扩展连接");}
        void UpdateBrowserThemes(){var active=library.State.Wallpapers.FirstOrDefault(w=>w.Id==library.State.ActiveId);if(active==null){if(bridge!=null)bridge.Clear();return;}var palette=library.State.ActivePalette??library.Effective(active);foreach(string browser in new[]{"chrome","edge"})library.ExportBrowser(active,browser,palette);if(bridge!=null)bridge.Publish(active,palette);}
        static Panel SettingRow(string caption,string description,Control input){var row=new Panel{Height=72,BackColor=Color.White};row.Controls.Add(Label(caption,10,FontStyle.Regular,new Point(2,8),new Size(330,25)));row.Controls.Add(Label(description,8,FontStyle.Regular,new Point(2,35),new Size(400,22)));input.BackColor=input is Toggle?Color.White:Color.FromArgb(246,247,249);input.Anchor=AnchorStyles.Top|AnchorStyles.Right;row.Controls.Add(input);row.Resize+=(s,e)=>{input.Left=row.Width-input.Width-4;input.Top=(row.Height-input.Height)/2;};return row;}
        internal void CapturePreview(int page,string filename){navigation[page].PerformClick();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(filename);}}
        Image FloatingPreview(Wallpaper wallpaper){return cards.Controls.Cast<WallpaperCard>().First(c=>c.Item.Id==wallpaper.Id).PreviewImage;}
        void UpdateFloatingOrb(){if(smokeMode || IsDisposed)return;if(launcher==null){if(!library.State.ShowFloatingOrb)return;launcher=new FloatingLauncher(library,FloatingPreview,SwitchWallpaper,ShowWindow,()=>floating.Checked=false);}launcher.Refresh();}
        internal void CaptureFloatingPreview(string folder){using(var orb=new OrbWindow())using(var picker=new WallpaperPicker(library,FloatingPreview,w=>{})){using(var bitmap=new Bitmap(orb.Width,orb.Height)){orb.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(folder,"floating-orb-preview.png"));}picker.Location=new Point(-10000,-10000);picker.Show();Application.DoEvents();using(var bitmap=new Bitmap(picker.Width,picker.Height)){picker.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(folder,"floating-picker-preview.png"));}picker.Hide();}}
        void RefreshCards() { foreach(Control c in cards.Controls.Cast<Control>().ToArray()) c.Dispose();cards.Controls.Clear();foreach(var w in library.State.Wallpapers){var card=new WallpaperCard(w);card.Click+=(s,e)=>{selected=card.Item;ShowSelected();};cards.Controls.Add(card);} }
        void ShowSelected() {
            if(selected==null) {apply.Enabled=false;return;}var p=library.Effective(selected);title.Text=selected.Name;
            var cardForPreview=cards.Controls.Cast<WallpaperCard>().First(c=>c.Item.Id==selected.Id);preview.SetContent(new Bitmap(cardForPreview.PreviewImage),p);
            subtitle.Text="自动识别 · "+(p.Dark?"深色方案":"浅色方案")+" · 主色 "+p.Accent;paletteLabel.Text="可点击「调整主色」微调取色结果";
            foreach(WallpaperCard card in cards.Controls){card.Selected=card.Item.Id==selected.Id;card.Active=card.Item.Id==library.State.ActiveId;card.Invalidate();}
            if(launcher!=null)launcher.Refresh();
        }
        void ChangeAccent() {if(selected==null)return;using(var dialog=new ColorDialog{Color=Palette.ColorOf(selected.Palette.Accent),FullOpen=true})if(dialog.ShowDialog()==DialogResult.OK){selected.Palette.Accent=Palette.Hex(dialog.Color); // Recompute surfaces without losing image's auto mode.
            bool dark=selected.Palette.Dark;selected.Palette.Background=Palette.Hex(Palette.Mix(dialog.Color,dark?Color.FromArgb(15,18,25):Color.White,dark?.86:.94));selected.Palette.Surface=Palette.Hex(Palette.Mix(dialog.Color,dark?Color.FromArgb(30,34,44):Color.White,dark?.8:.98));library.Save();ShowSelected();}}
        void Import() {using(var d=new OpenFileDialog{Filter="壁纸图片|*.jpg;*.jpeg;*.png;*.bmp",Multiselect=true})if(d.ShowDialog()==DialogResult.OK)ImportFiles(d.FileNames);}
        async void ImportFiles(string[] files) {
            if(busy)return;SetBusy(true);Status("正在读取图片并提取配色…");Wallpaper last=null;var failures=new System.Collections.Generic.List<string>();
            try {await Task.Run(()=>{foreach(string file in files)try{last=library.Import(file,Path.GetFileNameWithoutExtension(file));}catch(Exception ex){failures.Add(Path.GetFileName(file)+"："+ex.Message);}});if(last!=null)selected=last;RefreshCards();ShowSelected();Status(failures.Count>0?String.Join("；",failures):"已导入 "+files.Length+" 张壁纸，配色识别完成。");}finally{SetBusy(false);}
        }
        void SetBusy(bool value) {busy=value;Controls[0].Enabled=!value;preview.Working=value;apply.Text=value?"正在应用…":"应用桌面";if(launcher!=null)launcher.Suspend(value);}
        protected override void OnLoad(EventArgs e){base.OnLoad(e);var area=Screen.FromPoint(Cursor.Position).WorkingArea;var limit=new Size(Math.Max(320,area.Width-80),Math.Max(300,area.Height-80));MinimumSize=new Size(Math.Min(MinimumSize.Width,limit.Width),Math.Min(MinimumSize.Height,limit.Height));Size=new Size(Math.Min(Width,limit.Width),Math.Min(Height,limit.Height));Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);}
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
            try{Status("正在后台准备原尺寸图片与主题…");PreparedTheme prepared=null;await RunBackground(()=>prepared=WindowsTheme.Prepare(library,wallpaper,palette,syncSystem));await WithTransition(palette,async()=>{await RunBackground(()=>{WindowsTheme.Apply(library,prepared,Progress);library.State.ActiveId=wallpaper.Id;library.State.ActivePalette=palette;library.Save();});UpdateBrowserThemes();ShowSelected();await Task.Delay(120);});Status("壁纸已切换"+(syncSystem?"，Windows 主题已提交":"")+"；已向 Chrome / Edge 新标签页发布壁纸与配色。");}catch(Exception ex){Error(ex);}finally{SetBusy(false);}
        }
        void Next() {if(busy || library.State.Wallpapers.Count==0)return;int i=library.State.Wallpapers.FindIndex(w=>w.Id==library.State.ActiveId);selected=library.State.Wallpapers[(i+1)%library.State.Wallpapers.Count];ShowSelected();Apply();}
        void SwitchWallpaper(Wallpaper wallpaper){if(busy || !library.State.Wallpapers.Contains(wallpaper))return;selected=wallpaper;ShowSelected();Apply();}
        async void Undo() {if(busy)return;SetBusy(true);Status("正在恢复上次切换前的设置…");try{var palette=library.State.ActivePalette??(selected==null?new Palette{Accent="#718576",Background="#F5F6F1",Text="#252B28"}:library.Effective(selected));await WithTransition(palette,async()=>{await RunBackground(()=>WindowsTheme.Undo(library));UpdateBrowserThemes();ShowSelected();});Status("已恢复上次切换前的壁纸与系统设置。");}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
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
                    using(var form=new MainForm(library,smoke)) {if(smoke){form.Show();Application.DoEvents();string output=Path.Combine(Directory.GetCurrentDirectory(),"test-output");form.CapturePreview(0,Path.Combine(output,"preview.png"));form.CapturePreview(1,Path.Combine(output,"settings-preview.png"));form.CapturePreview(2,Path.Combine(output,"browser-preview.png"));form.CaptureFloatingPreview(output);form.Size=form.MinimumSize;form.CapturePreview(1,Path.Combine(output,"settings-compact-preview.png"));form.CapturePreview(0,Path.Combine(output,"wallpaper-compact-preview.png"));form.Close();}else Application.Run(form);}
                }catch(Exception e){MessageBox.Show(e.Message,"DeskShift 启动失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
        static bool created;
    }
}
