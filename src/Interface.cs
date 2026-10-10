using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskShift {
    internal static class Interface {
        internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        internal static ComboBox Picker(int width){var box=new ChoicePicker{DropDownStyle=ComboBoxStyle.DropDownList,Width=width,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=24,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(246,247,249)};box.DrawItem+=(s,e)=>{e.DrawBackground();int index=e.Index>=0?e.Index:box.SelectedIndex;if(index>=0)TextRenderer.DrawText(e.Graphics,box.Items[index].ToString(),box.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),Color.FromArgb(53,60,66),TextFormatFlags.VerticalCenter|TextFormatFlags.Left);};return box;}
    }
    internal class ChoicePicker : ComboBox {
        // Native ComboBox omits its selection in some WM_PRINT paths (DrawToBitmap).
        protected override void WndProc(ref Message m){base.WndProc(ref m);if((m.Msg==0x317 || m.Msg==0x318) && m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam)){using(var brush=new SolidBrush(BackColor))g.FillRectangle(brush,ClientRectangle);TextRenderer.DrawText(g,Text,Font,new Rectangle(8,0,Width-32,Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);using(var pen=new Pen(Color.FromArgb(97,108,104),1.5f)){int x=Width-15,y=Height/2;g.DrawLines(pen,new[]{new Point(x-3,y-1),new Point(x,y+2),new Point(x+3,y-1)});}}}}
    }
    internal class Surface : Panel {
        public Surface(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(16);}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(247,248,250):Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Interface.Round(new RectangleF(1,1,Width-3,Height-3),14))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(Color.FromArgb(229,232,235))){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}}
    }
    internal class SoftButton : Button {
        public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color color=Enabled?BackColor:Color.FromArgb(228,232,229);if(ClientRectangle.Contains(PointToClient(Cursor.Position)) && Enabled)color=Palette.Mix(color,Color.Black,.04);using(var path=Interface.Round(new RectangleF(0,0,Width-1,Height-1),9))using(var b=new SolidBrush(color))g.FillPath(b,path);TextRenderer.DrawText(g,Text,Font,ClientRectangle,Enabled?ForeColor:Color.Gray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-9,Height-9));}
    }
    internal class Toggle : CheckBox {
        public Toggle(){Appearance=Appearance.Normal;AutoSize=false;Size=new Size(46,28);Text="";Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);using(var path=Interface.Round(new RectangleF(2,3,40,22),11))using(var brush=new SolidBrush(Enabled && Checked?Color.FromArgb(88,122,104):Color.FromArgb(207,212,217)))g.FillPath(brush,path);using(var brush=new SolidBrush(Color.White))g.FillEllipse(brush,Checked?23:5,6,16,16);if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
    }
}
