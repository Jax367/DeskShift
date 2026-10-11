using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace DeskShift {
    internal static class Interface {
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        internal static void WindowPalette(Form form,Palette palette){if(!form.IsHandleCreated)return;try{int dark=palette.Dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref dark,4);Color background=Palette.ColorOf(palette.Background),text=Palette.ColorOf(palette.Text);int caption=background.R|(background.G<<8)|(background.B<<16),ink=text.R|(text.G<<8)|(text.B<<16);DwmSetWindowAttribute(form.Handle,35,ref caption,4);DwmSetWindowAttribute(form.Handle,36,ref ink,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
        internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        internal static ComboBox Picker(int width){var box=new ChoicePicker{DropDownStyle=ComboBoxStyle.DropDownList,Width=width,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=24,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(246,247,249)};box.DrawItem+=(s,e)=>{using(var brush=new SolidBrush(box.BackColor))e.Graphics.FillRectangle(brush,e.Bounds);int index=e.Index>=0?e.Index:box.SelectedIndex;if(index>=0)TextRenderer.DrawText(e.Graphics,box.Items[index].ToString(),box.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),box.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);};return box;}
    }
    internal class ChoicePicker : ComboBox {
        // Native ComboBox omits its selection in some WM_PRINT paths (DrawToBitmap).
        protected override void WndProc(ref Message m){base.WndProc(ref m);if((m.Msg==0x317 || m.Msg==0x318) && m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam)){using(var brush=new SolidBrush(BackColor))g.FillRectangle(brush,ClientRectangle);TextRenderer.DrawText(g,Text,Font,new Rectangle(8,0,Width-32,Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);using(var pen=new Pen(Color.FromArgb(97,108,104),1.5f)){int x=Width-15,y=Height/2;g.DrawLines(pen,new[]{new Point(x-3,y-1),new Point(x,y+2),new Point(x+3,y-1)});}}}}
    }
    internal class Surface : Panel {
        internal Color BorderColor=Color.FromArgb(229,232,235);
        public Surface(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(16);}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Interface.Round(new RectangleF(1,1,Width-3,Height-3),14))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(BorderColor)){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}}
    }
    internal class SoftButton : Button {
        internal bool Primary;
        internal Color DisabledColor=Color.FromArgb(228,232,229),DisabledText=Color.Gray;
        public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=PointerPreservation.ForControl("Hand",Cursors.Hand);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color color=Enabled?BackColor:DisabledColor;if(ClientRectangle.Contains(PointToClient(Cursor.Position)) && Enabled)color=Palette.Mix(color,ForeColor,.06);using(var path=Interface.Round(new RectangleF(0,0,Width-1,Height-1),9))using(var b=new SolidBrush(color))g.FillPath(b,path);TextRenderer.DrawText(g,Text,Font,ClientRectangle,Enabled?ForeColor:DisabledText,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-9,Height-9));}
    }
    internal class Toggle : CheckBox {
        internal Color OnColor=Color.FromArgb(88,122,104),OffColor=Color.FromArgb(207,212,217),KnobColor=Color.White;
        public Toggle(){Appearance=Appearance.Normal;AutoSize=false;Size=new Size(46,28);Text="";Cursor=PointerPreservation.ForControl("Hand",Cursors.Hand);AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);using(var path=Interface.Round(new RectangleF(2,3,40,22),11))using(var brush=new SolidBrush(Enabled && Checked?OnColor:OffColor))g.FillPath(brush,path);using(var brush=new SolidBrush(KnobColor))g.FillEllipse(brush,Checked?23:5,6,16,16);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
    }
}
