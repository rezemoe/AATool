using System;
using System.IO;
using System.Linq;
using System.Xml;
using AATool.Configuration;
using AATool.Graphics;
using AATool.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AATool.UI.Screens
{
    public abstract class UIScreen : UIControl
    {
        public ScreenWindow Host               { get; private set; }
        public Main Main                       { get; private set; }
        public GraphicsDevice GraphicsDevice   { get; private set; }

        public int FormWidth  => this.Host.ClientSize.X;
        public int FormHeight => this.Host.ClientSize.Y;
        public bool HasFocus  => this.Host.Focused;

        public abstract Color FrameBackColor();
        public abstract Color FrameBorderColor();

        public readonly Canvas Canvas = new ();

        protected bool Positioned;

        public UIScreen(Main main, ScreenWindow host)
        {
            this.Main           = main;
            this.Host           = host;
            this.GraphicsDevice = main.GraphicsDevice;
            this.DrawMode       = DrawMode.All;
            this.SetIconFile(Paths.System.MainIcon);
        }

        public void Show() => this.Host.Show();
        public void Hide() => this.Host.Hide();

        public void SetIcon(string name) => 
            this.SetIconFile(Path.Combine(Paths.System.AssetsFolder, "icons", $"{name}.ico"));

        protected void SetIconFile(string path)
        {
            try
            {
                this.Host.SetIcon(path);
            }
            catch
            { 
                //couldn't change icon, probably file missing. move on
            }
        }

        public abstract string GetCurrentView();
        public abstract void ReloadView();
        protected abstract void ConstrainWindow();

        public virtual void Click(UIControl sender) { }

        public virtual void Dispose()
        {
            this.Host.Close();
        }

        public virtual void Prepare()
        {
            if (this.Host.IsPrimary)
            {
                this.GraphicsDevice.SetRenderTarget(null);
            }
            else
            {
                this.Host.BeginRender(this.GraphicsDevice);
                this.GraphicsDevice.Clear(this.FrameBackColor());
            }
        }

        public void Render() => this.DrawRecursive(this.Canvas);

        public virtual void Present()
        {
            //the primary window is presented by monogame
            if (!this.Host.IsPrimary)
                this.Host.EndRender(this.GraphicsDevice);
        }

        public override void MoveTo(Point point) =>
            this.Host.Location = point;

        public override void MoveBy(Point point) =>
            this.Host.Location = this.Host.Location + point;

        public override void ScaleTo(Point point) =>
            this.Host.ClientSize = point;

        public override void ResizeThis(Rectangle parent)
        {
            this.Bounds  = new Rectangle(this.Bounds.Location, parent.Size);
            this.Inner = new Rectangle(Point.Zero, parent.Size);
        }

        public override void DrawRecursive(Canvas canvas)
        {
            if (!SpriteSheet.Loading)
            {
                this.Canvas.BeginDraw(this);
                base.DrawRecursive(this.Canvas);
                if (Config.Main.LayoutDebugMode)
                    this.DrawDebugRecursive(this.Canvas);
                this.Canvas.EndDraw(this);
            }
        }

        public override void DrawDebugRecursive(Canvas canvas)
        {
            for (int i = 0; i < this.Children.Count; i++)
                this.Children[i].DrawDebugRecursive(canvas);
        }

        protected void PositionWindow(WindowSnap snap, int monitor, Point lastPosition)
        {
            try
            {
                var displays = ScreenWindow.GetDisplays();
                int displayIndex = MathHelper.Clamp(monitor - 1, 0, displays.Count - 1);
                Rectangle desktop = displays[displayIndex];

                //snap the outer edges of the window (including decorations), then convert back to client position
                Rectangle bounds = this.Host.Bounds;
                Point frameOffset = this.Host.Location - bounds.Location;
                Point point = snap switch {
                    WindowSnap.Remember => lastPosition,
                    WindowSnap.Centered => new Point(desktop.X + ((desktop.Width - bounds.Width) / 2), desktop.Y + ((desktop.Height - bounds.Height) / 2)) + frameOffset,
                    WindowSnap.TopLeft => new Point(desktop.Left, desktop.Top) + frameOffset,
                    WindowSnap.TopRight => new Point(desktop.Right - bounds.Width, desktop.Top) + frameOffset,
                    WindowSnap.BottomLeft => new Point(desktop.Left, desktop.Bottom - bounds.Height) + frameOffset,
                    WindowSnap.BottomRight => new Point(desktop.Right - bounds.Width, desktop.Bottom - bounds.Height) + frameOffset,
                    _ => this.Host.Location
                };

                this.Host.Location = point;

                //make sure window is visible on screen
                bounds = new Rectangle(point - frameOffset, bounds.Size);
                if (!displays.Any(display => display.Intersects(bounds)))
                    this.Host.Location = new Point(desktop.X + ((desktop.Width - bounds.Width) / 2), desktop.Y + ((desktop.Height - bounds.Height) / 2)) + frameOffset;
            }
            catch (Exception)
            {

            }
        }
    }
}