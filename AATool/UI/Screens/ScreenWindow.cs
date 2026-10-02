using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AATool.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AATool.UI.Screens
{
    /// <summary>
    /// An SDL window that a <see cref="UIScreen"/> renders to. The primary window is the one MonoGame creates,
    /// secondary windows (overlay, update popup) are created here and share MonoGame's OpenGL context.
    /// </summary>
    public sealed class ScreenWindow
    {
        private sealed class Win32Window : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }
            public Win32Window(IntPtr handle) => this.Handle = handle;
        }

        private struct WindowEvent
        {
            public uint WindowId;
            public byte Type;
            public int Data1;
            public int Data2;
        }

        public static ScreenWindow Primary { get; private set; }

        private static readonly Dictionary<uint, ScreenWindow> Windows = new ();
        private static readonly ConcurrentQueue<WindowEvent> PendingEvents = new ();

        //keep the filter delegate alive for as long as sdl might call it
        private static SdlNative.EventFilter Filter;
        private static Game Game;
        private static IntPtr GLContext;

        public IntPtr Handle { get; }
        public uint Id { get; }
        public bool IsPrimary => this == Primary;
        public bool IsDisposed { get; private set; }

        /// <summary>Raised when the user tries to close the window. Set Cancel to keep it open.</summary>
        public event EventHandler<CancelEventArgs> Closing;
        /// <summary>Raised when the user resizes the window.</summary>
        public event EventHandler Resized;

        public bool KeepRestored { get; set; }

        /// <summary>Owner for winforms dialogs. Only available on windows, where forms and sdl share the same window system.</summary>
        public System.Windows.Forms.IWin32Window Owner { get; }

        private bool topMost;
        private int savedBackBufferWidth;
        private int savedBackBufferHeight;

        public static void Initialize(Game game)
        {
            Game = game;
            GLContext = SdlNative.GLGetCurrentContext();
            Primary = new ScreenWindow(game.Window.Handle);

            //monogame assumes there is only one window, so intercept events meant for other windows
            Filter = FilterEvent;
            SdlNative.SetEventFilter(Marshal.GetFunctionPointerForDelegate(Filter), IntPtr.Zero);
        }

        private ScreenWindow(IntPtr handle)
        {
            this.Handle = handle;
            this.Id = SdlNative.GetWindowID(handle);
            if (Platform.IsWindows)
                this.Owner = new Win32Window(SdlNative.GetWin32WindowHandle(handle));
            lock (Windows)
                Windows[this.Id] = this;
        }

        public ScreenWindow(string title, int width, int height)
            : this(CreateHandle(title, width, height))
        {
        }

        private static IntPtr CreateHandle(string title, int width, int height)
        {
            //new opengl windows use the same attributes as the primary window, so they can share its context
            IntPtr handle = SdlNative.CreateWindow(SdlNative.Utf8(title),
                SdlNative.WindowPosUndefined, SdlNative.WindowPosUndefined,
                width, height,
                SdlNative.WindowOpenGL | SdlNative.WindowHidden);

            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"Unable to create window \"{title}\".");
            return handle;
        }

        public string Title
        {
            set => SdlNative.SetWindowTitle(this.Handle, SdlNative.Utf8(value));
        }

        /// <summary>Position of the window's client area on the desktop.</summary>
        public Point Location
        {
            get
            {
                SdlNative.GetWindowPosition(this.Handle, out int x, out int y);
                return new Point(x, y);
            }
            set => SdlNative.SetWindowPosition(this.Handle, value.X, value.Y);
        }

        public Point ClientSize
        {
            get
            {
                SdlNative.GetWindowSize(this.Handle, out int width, out int height);
                return new Point(width, height);
            }
            set => SdlNative.SetWindowSize(this.Handle, value.X, value.Y);
        }

        /// <summary>Bounds of the window including its decorations, if the window manager reports them.</summary>
        public Rectangle Bounds
        {
            get
            {
                Point location = this.Location;
                Point size = this.ClientSize;
                if (SdlNative.GetWindowBordersSize(this.Handle, out int top, out int left, out int bottom, out int right) is not 0)
                    top = left = bottom = right = 0;
                return new Rectangle(location.X - left, location.Y - top, size.X + left + right, size.Y + top + bottom);
            }
        }

        public bool Visible
        {
            get => !this.IsDisposed && (SdlNative.GetWindowFlags(this.Handle) & SdlNative.WindowShown) is not 0;
            set
            {
                if (value)
                    this.Show();
                else
                    this.Hide();
            }
        }

        public bool Focused => !this.IsDisposed
            && (SdlNative.GetWindowFlags(this.Handle) & SdlNative.WindowInputFocus) is not 0;

        public bool Resizable
        {
            set => SdlNative.SetWindowResizable(this.Handle, value ? 1 : 0);
        }

        public bool TopMost
        {
            get => this.topMost;
            set
            {
                this.topMost = value;
                this.ApplyTopMost();
            }
        }

        public void SetMinimumSize(int width, int height) =>
            SdlNative.SetWindowMinimumSize(this.Handle, Math.Max(width, 1), Math.Max(height, 1));

        public void SetMaximumSize(int width, int height) =>
            SdlNative.SetWindowMaximumSize(this.Handle, Math.Max(width, 1), Math.Max(height, 1));

        public void Show()
        {
            if (!this.IsDisposed && !this.Visible)
                SdlNative.ShowWindow(this.Handle);
        }

        public void Hide()
        {
            if (!this.IsDisposed && this.Visible)
                SdlNative.HideWindow(this.Handle);
        }

        public void BringToFront()
        {
            if (!this.IsDisposed)
                SdlNative.RaiseWindow(this.Handle);
        }

        public void SetIcon(string path)
        {
            if (this.IsDisposed)
                return;

            using var icon = new System.Drawing.Icon(path, 64, 64);
            using System.Drawing.Bitmap bitmap = icon.ToBitmap();
            var area = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData pixels = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                //32bpp argb is stored as bgra in memory
                IntPtr surface = SdlNative.CreateRGBSurfaceFrom(pixels.Scan0,
                    bitmap.Width, bitmap.Height, 32, pixels.Stride,
                    0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000);

                if (surface != IntPtr.Zero)
                {
                    SdlNative.SetWindowIcon(this.Handle, surface);
                    SdlNative.FreeSurface(surface);
                }
            }
            finally
            {
                bitmap.UnlockBits(pixels);
            }
        }

        public void Close()
        {
            if (this.IsDisposed)
                return;

            if (this.IsPrimary)
            {
                Game.Exit();
                return;
            }

            lock (Windows)
                Windows.Remove(this.Id);
            SdlNative.DestroyWindow(this.Handle);
            this.IsDisposed = true;
        }

        /// <summary>Direct rendering to this window's default framebuffer.</summary>
        public void BeginRender(GraphicsDevice device)
        {
            Point size = this.ClientSize;
            SdlNative.GLMakeCurrent(this.Handle, GLContext);

            //monogame flips viewports relative to the backbuffer size, so it needs to match this window while drawing
            PresentationParameters parameters = device.PresentationParameters;
            this.savedBackBufferWidth  = parameters.BackBufferWidth;
            this.savedBackBufferHeight = parameters.BackBufferHeight;
            parameters.BackBufferWidth  = size.X;
            parameters.BackBufferHeight = size.Y;

            device.SetRenderTarget(null);
            device.Viewport = new Viewport(0, 0, size.X, size.Y);
        }

        /// <summary>Present this window and return rendering to the primary window.</summary>
        public void EndRender(GraphicsDevice device)
        {
            SdlNative.GLSwapWindow(this.Handle);

            PresentationParameters parameters = device.PresentationParameters;
            parameters.BackBufferWidth  = this.savedBackBufferWidth;
            parameters.BackBufferHeight = this.savedBackBufferHeight;

            SdlNative.GLMakeCurrent(Primary.Handle, GLContext);
            device.Viewport = new Viewport(0, 0, parameters.BackBufferWidth, parameters.BackBufferHeight);
        }

        private void ApplyTopMost()
        {
            if (this.IsDisposed)
                return;

            if (SdlNative.SetWindowAlwaysOnTop is not null)
                SdlNative.SetWindowAlwaysOnTop(this.Handle, this.topMost ? 1 : 0);
            else if (Platform.IsLinux)
                X11Native.SetAlwaysOnTop(SdlNative.GetX11WindowId(this.Handle), this.topMost);
        }

        private void RequestClose()
        {
            var args = new CancelEventArgs();
            this.Closing?.Invoke(this, args);
            if (!args.Cancel)
                this.Close();
        }

        private void HandleEvent(WindowEvent windowEvent)
        {
            switch (windowEvent.Type)
            {
                case SdlNative.WindowEventShown:
                    //window managers ignore state changes while a window is hidden
                    if (this.topMost)
                        this.ApplyTopMost();
                    break;
                case SdlNative.WindowEventClose:
                    this.RequestClose();
                    break;
                case SdlNative.WindowEventResized:
                    this.Resized?.Invoke(this, EventArgs.Empty);
                    break;
                case SdlNative.WindowEventMinimized:
                    if (this.KeepRestored)
                        SdlNative.RestoreWindow(this.Handle);
                    break;
            }
        }

        /// <summary>Handle window events that were intercepted since the last update. Call from the main thread.</summary>
        public static void ProcessEvents()
        {
            while (PendingEvents.TryDequeue(out WindowEvent windowEvent))
            {
                ScreenWindow window;
                lock (Windows)
                    Windows.TryGetValue(windowEvent.WindowId, out window);
                if (window is not null && !window.IsDisposed)
                    window.HandleEvent(windowEvent);
            }
        }

        public static bool AnyFocused()
        {
            lock (Windows)
            {
                foreach (ScreenWindow window in Windows.Values)
                {
                    if (window.Focused)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Mouse state relative to the primary window, regardless of which of the program's windows the cursor is over.
        /// MonoGame only tracks buttons while the cursor is over its own window.
        /// </summary>
        public static MouseState GetMouseState()
        {
            MouseState monogame = Mouse.GetState();
            uint buttons = SdlNative.GetGlobalMouseState(out int x, out int y);
            Point origin = Primary.Location;

            static ButtonState Button(uint state, uint mask) =>
                (state & mask) is not 0 ? ButtonState.Pressed : ButtonState.Released;

            return new MouseState(x - origin.X, y - origin.Y,
                monogame.ScrollWheelValue,
                Button(buttons, SdlNative.ButtonLeft),
                Button(buttons, SdlNative.ButtonMiddle),
                Button(buttons, SdlNative.ButtonRight),
                Button(buttons, SdlNative.ButtonX1),
                Button(buttons, SdlNative.ButtonX2),
                monogame.HorizontalScrollWheelValue);
        }

        /// <summary>Usable (excluding panels and taskbars) area of each display.</summary>
        public static List<Rectangle> GetDisplays()
        {
            var displays = new List<Rectangle>();
            int count = SdlNative.GetNumVideoDisplays();
            for (int i = 0; i < count; i++)
            {
                if (SdlNative.GetDisplayUsableBounds(i, out SdlNative.Rect rect) is 0)
                    displays.Add(new Rectangle(rect.X, rect.Y, rect.W, rect.H));
            }
            return displays;
        }

        private static int FilterEvent(IntPtr userData, IntPtr sdlEvent)
        {
            try
            {
                //sdl events store type at offset 0, and window id at offset 8 for window and mouse events
                uint type = (uint)Marshal.ReadInt32(sdlEvent);
                if (type is SdlNative.EventQuit)
                {
                    //let the primary window's closing logic decide whether to quit
                    PendingEvents.Enqueue(new WindowEvent { WindowId = Primary.Id, Type = SdlNative.WindowEventClose });
                    return 0;
                }

                if (type is SdlNative.EventWindow)
                {
                    var windowEvent = new WindowEvent {
                        WindowId = (uint)Marshal.ReadInt32(sdlEvent, 8),
                        Type = Marshal.ReadByte(sdlEvent, 12),
                        Data1 = Marshal.ReadInt32(sdlEvent, 16),
                        Data2 = Marshal.ReadInt32(sdlEvent, 20),
                    };

                    if (windowEvent.WindowId != Primary.Id || windowEvent.Type is SdlNative.WindowEventClose)
                    {
                        PendingEvents.Enqueue(windowEvent);
                        return 0;
                    }

                    //monogame doesn't need to know when its window is shown, but we still do
                    if (windowEvent.Type is SdlNative.WindowEventShown)
                        PendingEvents.Enqueue(windowEvent);
                    return 1;
                }

                if (type is SdlNative.EventMouseMotion)
                {
                    //motion over other windows would be mistaken for motion over the primary window
                    return (uint)Marshal.ReadInt32(sdlEvent, 8) == Primary.Id ? 1 : 0;
                }
            }
            catch
            {
                //never let an exception escape into native code
            }
            return 1;
        }
    }
}
