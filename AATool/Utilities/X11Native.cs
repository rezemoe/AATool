using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AATool.Utilities
{
    /// <summary>
    /// Small set of Xlib helpers for features SDL and Mono don't expose on Linux,
    /// such as finding the focused window of another application.
    /// </summary>
    internal static class X11Native
    {
        private const int ClientMessage = 33;
        private const long SubstructureNotifyMask   = 1L << 19;
        private const long SubstructureRedirectMask = 1L << 20;
        private const long AnyPropertyType = 0;
        private const long XaCardinal = 6;
        private const long XaString = 31;
        private const long XaWindow = 33;

        [StructLayout(LayoutKind.Sequential)]
        private struct XClientMessageEvent
        {
            public int Type;
            public IntPtr Serial;
            public int SendEvent;
            public IntPtr Display;
            public IntPtr Window;
            public IntPtr MessageType;
            public int Format;
            public IntPtr Data0, Data1, Data2, Data3, Data4;
            //pad to the size of the XEvent union (24 longs)
            public IntPtr Pad0, Pad1, Pad2, Pad3, Pad4, Pad5, Pad6, Pad7, Pad8, Pad9, Pad10, Pad11;
        }

        private const string LibX11 = "libX11.so.6";

        [DllImport(LibX11)]
        private static extern IntPtr XOpenDisplay(IntPtr name);
        [DllImport(LibX11)]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);
        [DllImport(LibX11)]
        private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
        [DllImport(LibX11)]
        private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, IntPtr eventMask, ref XClientMessageEvent sendEvent);
        [DllImport(LibX11)]
        private static extern int XFlush(IntPtr display);
        [DllImport(LibX11)]
        private static extern int XFree(IntPtr data);
        [DllImport(LibX11)]
        private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            IntPtr offset, IntPtr length, bool delete, IntPtr requestedType,
            out IntPtr actualType, out int actualFormat, out IntPtr itemCount, out IntPtr bytesAfter, out IntPtr data);

        private static IntPtr Display;
        private static bool Unavailable;

        private static bool TryGetDisplay(out IntPtr display)
        {
            if (Display == IntPtr.Zero && !Unavailable)
            {
                try
                {
                    Display = XOpenDisplay(IntPtr.Zero);
                }
                catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
                {
                    //libX11 isn't installed
                }
                Unavailable = Display == IntPtr.Zero;
            }
            display = Display;
            return display != IntPtr.Zero;
        }

        public static void SetAlwaysOnTop(IntPtr window, bool enabled)
        {
            if (window == IntPtr.Zero || !TryGetDisplay(out IntPtr display))
                return;

            //ask the window manager to add or remove the "above" state (EWMH _NET_WM_STATE)
            var message = new XClientMessageEvent {
                Type = ClientMessage,
                SendEvent = 1,
                Display = display,
                Window = window,
                MessageType = XInternAtom(display, "_NET_WM_STATE", false),
                Format = 32,
                Data0 = (IntPtr)(enabled ? 1 : 0),
                Data1 = XInternAtom(display, "_NET_WM_STATE_ABOVE", false),
                Data2 = IntPtr.Zero,
                Data3 = (IntPtr)1,
            };
            IntPtr mask = (IntPtr)(SubstructureRedirectMask | SubstructureNotifyMask);
            XSendEvent(display, XDefaultRootWindow(display), false, mask, ref message);
            XFlush(display);
        }

        public static bool TryGetForegroundWindow(out int processId, out string title)
        {
            processId = 0;
            title = null;
            if (!TryGetDisplay(out IntPtr display))
                return false;

            IntPtr root = XDefaultRootWindow(display);
            IntPtr active = ReadLong(display, root, "_NET_ACTIVE_WINDOW", XaWindow);
            if (active == IntPtr.Zero)
                return false;

            processId = (int)ReadLong(display, active, "_NET_WM_PID", XaCardinal);
            title = ReadString(display, active, "_NET_WM_NAME") ?? ReadString(display, active, "WM_NAME");
            return processId > 0;
        }

        private static IntPtr ReadLong(IntPtr display, IntPtr window, string property, long type)
        {
            int status = XGetWindowProperty(display, window, XInternAtom(display, property, false),
                IntPtr.Zero, (IntPtr)1, false, (IntPtr)type,
                out _, out int format, out IntPtr count, out _, out IntPtr data);

            if (data == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                //xlib returns 32-bit format properties as an array of native longs
                return status is 0 && format is 32 && count != IntPtr.Zero
                    ? Marshal.ReadIntPtr(data)
                    : IntPtr.Zero;
            }
            finally
            {
                XFree(data);
            }
        }

        private static string ReadString(IntPtr display, IntPtr window, string property)
        {
            int status = XGetWindowProperty(display, window, XInternAtom(display, property, false),
                IntPtr.Zero, (IntPtr)1024, false, (IntPtr)AnyPropertyType,
                out IntPtr type, out int format, out IntPtr count, out _, out IntPtr data);

            if (data == IntPtr.Zero)
                return null;
            try
            {
                if (status is not 0 || format is not 8 || count == IntPtr.Zero)
                    return null;
                byte[] bytes = new byte[(int)count];
                Marshal.Copy(data, bytes, 0, bytes.Length);
                return (long)type == XaString
                    ? Encoding.GetEncoding("ISO-8859-1").GetString(bytes)
                    : Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                XFree(data);
            }
        }
    }
}
