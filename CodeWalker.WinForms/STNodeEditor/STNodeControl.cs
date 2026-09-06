using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
/*
MIT License

Copyright (c) 2021 DebugST@crystal_lz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */
/*
 * time: 2021-01-06
 * Author: Crystal_lz
 * blog: st233.com
 * Github: DebugST.github.io
 */
namespace ST.Library.UI.NodeEditor
{
    public class STNodeControl
    {
        private STNode? _Owner;

        public STNode? Owner {
            get { return _Owner; }
            internal set { _Owner = value; }
        }

        private int _Left;

        public int Left {
            get { return _Left; }
            set {
                _Left = value;
                this.OnMove(EventArgs.Empty);
                this.Invalidate();
            }
        }

        private int _Top;

        public int Top {
            get { return _Top; }
            set {
                _Top = value;
                this.OnMove(EventArgs.Empty);
                this.Invalidate();
            }
        }

        private int _Width;

        public int Width {
            get { return _Width; }
            set {
                _Width = value;
                this.OnResize(EventArgs.Empty);
                this.Invalidate();
            }
        }

        private int _Height;

        public int Height {
            get { return _Height; }
            set {
                _Height = value;
                this.OnResize(EventArgs.Empty);
                this.Invalidate();
            }
        }

        public int Right { get { return this._Left + this._Width; } }

        public int Bottom { get { return this._Top + this._Height; } }

        public Point Location {
            get { return new Point(this._Left, this._Top); }
            set {
                this.Left = value.X;
                this.Top = value.Y;
            }
        }
        public Size Size {
            get { return new Size(this._Width, this._Height); }
            set {
                this.Width = value.Width;
                this.Height = value.Height;
            }
        }
        public Rectangle DisplayRectangle {
            get { return new Rectangle(this._Left, this._Top, this._Width, this._Height); }
            set {
                this.Left = value.X;
                this.Top = value.Y;
                this.Width = value.Width;
                this.Height = value.Height;
            }
        }
        public Rectangle ClientRectangle {
            get { return new Rectangle(0, 0, this._Width, this._Height); }
        }

        private Color _BackColor = Color.FromArgb(127, 0, 0, 0);

        public Color BackColor {
            get { return _BackColor; }
            set {
                _BackColor = value;
                this.Invalidate();
            }
        }

        private Color _ForeColor = Color.White;

        public Color ForeColor {
            get { return _ForeColor; }
            set {
                _ForeColor = value;
                this.Invalidate();
            }
        }

        private string _Text = "STNCTRL";

        public string Text {
            get { return _Text; }
            set {
                _Text = value;
                this.Invalidate();
            }
        }

        private Font _Font;

        public Font Font {
            get { return _Font; }
            set {
                if (value == _Font) return;
                if (value == null) throw new ArgumentNullException("Value cannot be null");
                _Font = value;
                this.Invalidate();
            }
        }

        private bool _Enabled = true;

        public bool Enabled {
            get { return _Enabled; }
            set {
                if (value == _Enabled) return;
                _Enabled = value;
                this.Invalidate();
            }
        }

        private bool _Visable = true;

        public bool Visable {
            get { return _Visable; }
            set {
                if (value == _Visable) return;
                _Visable = value;
                this.Invalidate();
            }
        }

        protected StringFormat m_sf;

        public STNodeControl() {
            m_sf = new StringFormat();
            m_sf.Alignment = StringAlignment.Center;
            m_sf.LineAlignment = StringAlignment.Center;
            this._Font = new Font("courier new", 8.25f);
            this.Width = 75;
            this.Height = 23;
        }

        protected internal virtual void OnPaint(DrawingTools dt) {
            Graphics g = dt.Graphics;
            SolidBrush brush = dt.SolidBrush;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            brush.Color = this._BackColor;
            g.FillRectangle(brush, 0, 0, this.Width, this.Height);
            if (!string.IsNullOrEmpty(this._Text)) {
                brush.Color = this._ForeColor;
                g.DrawString(this._Text, this._Font, brush, this.ClientRectangle, m_sf);
            }
            this.Paint?.Invoke(this, new STNodeControlPaintEventArgs(dt));
        }

        public void Invalidate() {
            this._Owner?.Invalidate(new Rectangle(this._Left, this._Top + this._Owner.TitleHeight, this.Width, this.Height));
        }

        public void Invalidate(Rectangle rect) {
            this._Owner?.Invalidate(this.RectangleToParent(rect));
        }

        public Rectangle RectangleToParent(Rectangle rect) {
            var owner = this._Owner ?? throw new InvalidOperationException("The control must belong to a node before converting coordinates.");
            return new Rectangle(this._Left, this._Top + owner.TitleHeight, this.Width, this.Height);
        }

        public event EventHandler? GotFocus;
        public event EventHandler? LostFocus;
        public event EventHandler? MouseEnter;
        public event EventHandler? MouseLeave;
        public event MouseEventHandler? MouseDown;
        public event MouseEventHandler? MouseMove;
        public event MouseEventHandler? MouseUp;
        public event MouseEventHandler? MouseClick;
        public event MouseEventHandler? MouseWheel;
        public event EventHandler? MouseHWheel;

        public event KeyEventHandler? KeyDown;
        public event KeyEventHandler? KeyUp;
        public event KeyPressEventHandler? KeyPress;

        public event EventHandler? Move;
        public event EventHandler? Resize;

        public event STNodeControlPaintEventHandler? Paint;

        protected internal virtual void OnGotFocus(EventArgs e) {
            this.GotFocus?.Invoke(this, e);
        }
        protected internal virtual void OnLostFocus(EventArgs e) {
            this.LostFocus?.Invoke(this, e);
        }
        protected internal virtual void OnMouseEnter(EventArgs e) {
            this.MouseEnter?.Invoke(this, e);
        }
        protected internal virtual void OnMouseLeave(EventArgs e) {
            this.MouseLeave?.Invoke(this, e);
        }
        protected internal virtual void OnMouseDown(MouseEventArgs e) {
            this.MouseDown?.Invoke(this, e);
        }
        protected internal virtual void OnMouseMove(MouseEventArgs e) {
            this.MouseMove?.Invoke(this, e);
        }
        protected internal virtual void OnMouseUp(MouseEventArgs e) {
            this.MouseUp?.Invoke(this, e);
        }
        protected internal virtual void OnMouseClick(MouseEventArgs e) {
            this.MouseClick?.Invoke(this, e);
        }
        protected internal virtual void OnMouseWheel(MouseEventArgs e) {
            this.MouseWheel?.Invoke(this, e);
        }
        protected internal virtual void OnMouseHWheel(MouseEventArgs e) {
            this.MouseHWheel?.Invoke(this, e);
        }

        protected internal virtual void OnKeyDown(KeyEventArgs e) {
            this.KeyDown?.Invoke(this, e);
        }
        protected internal virtual void OnKeyUp(KeyEventArgs e) {
            this.KeyUp?.Invoke(this, e);
        }
        protected internal virtual void OnKeyPress(KeyPressEventArgs e) {
            this.KeyPress?.Invoke(this, e);
        }

        protected internal virtual void OnMove(EventArgs e) {
            this.Move?.Invoke(this, e);
        }

        protected internal virtual void OnResize(EventArgs e) {
            this.Resize?.Invoke(this, e);
        }

        public IAsyncResult? BeginInvoke(Delegate method) { return this.BeginInvoke(method, null); }
        public IAsyncResult? BeginInvoke(Delegate method, params object?[]? args) {
            if (this._Owner == null) return null;
            return this._Owner.BeginInvoke(method, args);
        }
        public object? Invoke(Delegate method) { return this.Invoke(method, null); }
        public object? Invoke(Delegate method, params object?[]? args) {
            if (this._Owner == null) return null;
            return this._Owner.Invoke(method, args);
        }
    }

    public delegate void STNodeControlPaintEventHandler(object sender, STNodeControlPaintEventArgs e);

    public class STNodeControlPaintEventArgs : EventArgs
    {
        /// <summary>
        /// Drawing tools
        /// </summary>
        public DrawingTools DrawingTools { get; private set; }

        public STNodeControlPaintEventArgs(DrawingTools dt) {
            this.DrawingTools = dt;
        }
    }
}