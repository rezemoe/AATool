using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;

namespace AATool.Utilities
{
    /// <summary>
    /// Minimal SDL2 bindings resolved from the same SDL library instance that MonoGame DesktopGL already loaded.
    /// Loading SDL a second time would create a separate copy with its own (uninitialized) global state.
    /// </summary>
    internal static class SdlNative
    {
        public const uint WindowOpenGL     = 0x00000002;
        public const uint WindowShown      = 0x00000004;
        public const uint WindowHidden     = 0x00000008;
        public const uint WindowResizable  = 0x00000020;
        public const uint WindowMinimized  = 0x00000040;
        public const uint WindowInputFocus = 0x00000200;
        public const uint WindowMouseFocus = 0x00000400;
        public const uint WindowAlwaysOnTop = 0x00008000;

        public const int WindowPosUndefined = 0x1FFF0000;

        public const uint EventQuit        = 0x100;
        public const uint EventWindow      = 0x200;
        public const uint EventMouseMotion = 0x400;

        public const byte WindowEventShown       = 1;
        public const byte WindowEventMoved       = 4;
        public const byte WindowEventResized     = 5;
        public const byte WindowEventSizeChanged = 6;
        public const byte WindowEventMinimized   = 7;
        public const byte WindowEventFocusGained = 12;
        public const byte WindowEventFocusLost   = 13;
        public const byte WindowEventClose       = 14;

        public const uint ButtonLeft   = 1 << 0;
        public const uint ButtonMiddle = 1 << 1;
        public const uint ButtonRight  = 1 << 2;
        public const uint ButtonX1     = 1 << 3;
        public const uint ButtonX2     = 1 << 4;

        public const int SysWMWindows = 1;
        public const int SysWMX11 = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int X, Y, W, H;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int EventFilter(IntPtr userData, IntPtr sdlEvent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_create_window(byte[] title, int x, int y, int w, int h, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window(IntPtr window);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint d_window_uint(IntPtr window);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_window_int(IntPtr window);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window_set_bool(IntPtr window, int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window_set_title(IntPtr window, byte[] title);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window_set_xy(IntPtr window, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window_get_xy(IntPtr window, out int x, out int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_window_get_borders(IntPtr window, out int top, out int left, out int bottom, out int right);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_window_set_icon(IntPtr window, IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_window_get_wminfo(IntPtr window, IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_create_surface_from(IntPtr pixels, int width, int height, int depth, int pitch,
            uint rmask, uint gmask, uint bmask, uint amask);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_free_surface(IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_get_int();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_get_display_bounds(int displayIndex, out Rect rect);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint d_get_mouse_state(out int x, out int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr d_get_ptr();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int d_gl_make_current(IntPtr window, IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_set_event_filter(IntPtr filter, IntPtr userData);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void d_get_version(byte[] version);

        private static readonly IntPtr Library = GetLibrary();

        public static readonly d_create_window CreateWindow = Load<d_create_window>("SDL_CreateWindow");
        public static readonly d_window DestroyWindow = Load<d_window>("SDL_DestroyWindow");
        public static readonly d_window ShowWindow = Load<d_window>("SDL_ShowWindow");
        public static readonly d_window HideWindow = Load<d_window>("SDL_HideWindow");
        public static readonly d_window RaiseWindow = Load<d_window>("SDL_RaiseWindow");
        public static readonly d_window RestoreWindow = Load<d_window>("SDL_RestoreWindow");
        public static readonly d_window_uint GetWindowID = Load<d_window_uint>("SDL_GetWindowID");
        public static readonly d_window_uint GetWindowFlags = Load<d_window_uint>("SDL_GetWindowFlags");
        public static readonly d_window_int GetWindowDisplayIndex = Load<d_window_int>("SDL_GetWindowDisplayIndex");
        public static readonly d_window_set_title SetWindowTitle = Load<d_window_set_title>("SDL_SetWindowTitle");
        public static readonly d_window_set_xy SetWindowPosition = Load<d_window_set_xy>("SDL_SetWindowPosition");
        public static readonly d_window_get_xy GetWindowPosition = Load<d_window_get_xy>("SDL_GetWindowPosition");
        public static readonly d_window_set_xy SetWindowSize = Load<d_window_set_xy>("SDL_SetWindowSize");
        public static readonly d_window_get_xy GetWindowSize = Load<d_window_get_xy>("SDL_GetWindowSize");
        public static readonly d_window_set_xy SetWindowMinimumSize = Load<d_window_set_xy>("SDL_SetWindowMinimumSize");
        public static readonly d_window_set_xy SetWindowMaximumSize = Load<d_window_set_xy>("SDL_SetWindowMaximumSize");
        public static readonly d_window_get_borders GetWindowBordersSize = Load<d_window_get_borders>("SDL_GetWindowBordersSize");
        public static readonly d_window_set_bool SetWindowResizable = Load<d_window_set_bool>("SDL_SetWindowResizable");
        public static readonly d_window_set_icon SetWindowIcon = Load<d_window_set_icon>("SDL_SetWindowIcon");
        public static readonly d_window_get_wminfo GetWindowWMInfo = Load<d_window_get_wminfo>("SDL_GetWindowWMInfo");
        public static readonly d_create_surface_from CreateRGBSurfaceFrom = Load<d_create_surface_from>("SDL_CreateRGBSurfaceFrom");
        public static readonly d_free_surface FreeSurface = Load<d_free_surface>("SDL_FreeSurface");
        public static readonly d_get_int GetNumVideoDisplays = Load<d_get_int>("SDL_GetNumVideoDisplays");
        public static readonly d_get_display_bounds GetDisplayUsableBounds = Load<d_get_display_bounds>("SDL_GetDisplayUsableBounds");
        public static readonly d_get_mouse_state GetGlobalMouseState = Load<d_get_mouse_state>("SDL_GetGlobalMouseState");
        public static readonly d_get_ptr GLGetCurrentContext = Load<d_get_ptr>("SDL_GL_GetCurrentContext");
        public static readonly d_gl_make_current GLMakeCurrent = Load<d_gl_make_current>("SDL_GL_MakeCurrent");
        public static readonly d_window GLSwapWindow = Load<d_window>("SDL_GL_SwapWindow");
        public static readonly d_set_event_filter SetEventFilter = Load<d_set_event_filter>("SDL_SetEventFilter");
        public static readonly d_get_version GetVersion = Load<d_get_version>("SDL_GetVersion");

        //only available in SDL 2.0.16 and newer
        public static readonly d_window_set_bool SetWindowAlwaysOnTop = Load<d_window_set_bool>("SDL_SetWindowAlwaysOnTop", false);

        public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes((text ?? string.Empty) + '\0');

        public static IntPtr GetX11WindowId(IntPtr window) => GetNativeWindow(window, SysWMX11);

        public static IntPtr GetWin32WindowHandle(IntPtr window) => GetNativeWindow(window, SysWMWindows);

        private static IntPtr GetNativeWindow(IntPtr window, int subsystem)
        {
            //SDL_SysWMinfo: SDL_version (3 bytes), subsystem (int @ 4), union (@ 8)
            //  windows: { HWND window, ... }   x11: { Display* display, Window window }
            IntPtr info = Marshal.AllocHGlobal(256);
            try
            {
                for (int i = 0; i < 256; i++)
                    Marshal.WriteByte(info, i, 0);
                byte[] version = new byte[3];
                GetVersion(version);
                Marshal.Copy(version, 0, info, 3);

                if (GetWindowWMInfo(window, info) == 0 || Marshal.ReadInt32(info, 4) != subsystem)
                    return IntPtr.Zero;
                return subsystem is SysWMX11
                    ? Marshal.ReadIntPtr(info, 8 + IntPtr.Size)
                    : Marshal.ReadIntPtr(info, 8);
            }
            finally
            {
                Marshal.FreeHGlobal(info);
            }
        }

        private static IntPtr GetLibrary()
        {
            //monogame keeps the handle of the sdl library it loaded in the internal "Sdl" class
            Type sdl = typeof(Game).Assembly.GetType("Sdl", true);
            return (IntPtr)sdl.GetField("NativeLibrary", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static T Load<T>(string function, bool required = true) where T : Delegate
        {
            IntPtr address = Platform.IsWindows
                ? GetProcAddress(Library, function)
                : dlsym(Library, function);

            if (address == IntPtr.Zero)
            {
                if (required)
                    throw new EntryPointNotFoundException(function);
                return null;
            }
            return (T)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string function);

        [DllImport("libdl.so.2")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);
    }
}
