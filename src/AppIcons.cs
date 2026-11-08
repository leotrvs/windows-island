using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsIsland
{
    // The icon of the app that sent a notification, looked up by its app id in the shell's Apps folder
    // (the same list the Start menu shows). The id comes from the notification database, which any app
    // can write to, so only plain ids reach the shell: no paths, no "shell:" names, nothing is opened or run.
    static class AppIcons
    {
        const int Size = 64;
        const int MaxCached = 64;
        const int TimeoutMs = 1500;
        const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;
        const int BI_RGB = 0, DIB_RGB_COLORS = 0;

        // "Microsoft.WindowsStore_8wekyb3d8bbwe!App", "com.squirrel.Discord.Discord"
        static readonly Regex ValidId = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._!\- ]{0,199}\z", RegexOptions.CultureInvariant); // \z: "$" would let a trailing newline through

        static readonly ConcurrentDictionary<string, ImageSource> cache =
            new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        // One loader thread for the whole app; a small queue so a hung shell can't pile up work
        static readonly BlockingCollection<Request> queue = new BlockingCollection<Request>(4);
        static readonly object workerLock = new object();
        static Thread worker;

        sealed class Request
        {
            public string Id;
            public ImageSource Icon;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr bitmap);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr obj, int size, out BITMAP bitmap);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER info, uint usage);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        // A frozen 64x64 icon, or null when the id is not a plain app id or the shell has no icon for it.
        public static ImageSource Load(string appId)
        {
            try
            {
                if (string.IsNullOrEmpty(appId) || appId.Contains("..") || !ValidId.IsMatch(appId)) return null;
                ImageSource cached;
                if (cache.TryGetValue(appId, out cached)) return cached;

                // Shell objects want a single-threaded apartment, and a hung shell must never hold up polling.
                // The loader stores its own result, so one that finishes after the timeout still fills the cache.
                EnsureWorker();
                Request request = new Request { Id = appId };
                if (!queue.TryAdd(request)) return null; // The shell is hanging; try again later
                return request.Done.Wait(TimeoutMs) ? request.Icon : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void EnsureWorker()
        {
            lock (workerLock)
            {
                if (worker != null) return;
                worker = new Thread(Work) { IsBackground = true, Name = "App icons" };
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
            }
        }

        static void Work()
        {
            foreach (Request request in queue.GetConsumingEnumerable())
            {
                try
                {
                    ImageSource icon;
                    if (!cache.TryGetValue(request.Id, out icon))
                    {
                        icon = LoadFromShell(request.Id);
                        if (cache.Count >= MaxCached) cache.Clear();
                        cache[request.Id] = icon; // Failures too, so a bad id is not retried every notification
                    }
                    request.Icon = icon;
                }
                catch (Exception)
                {
                }
                finally
                {
                    request.Done.Set();
                }
            }
        }

        static ImageSource LoadFromShell(string appId)
        {
            IShellItemImageFactory factory = null;
            IntPtr bitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(@"shell:AppsFolder\" + appId, IntPtr.Zero, ref iid, out factory);
                if (factory == null) return null;
                if (factory.GetImage(new SIZE { cx = Size, cy = Size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out bitmap) != 0) return null;
                if (bitmap == IntPtr.Zero) return null;
                return ToBitmapSource(bitmap);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        // Copies the pixels out as 32-bit top-down rows. Imaging.CreateBitmapSourceFromHBitmap drops the alpha
        // channel of these bitmaps, so the transparency would turn black.
        static BitmapSource ToBitmapSource(IntPtr bitmap)
        {
            BITMAP info;
            if (GetObject(bitmap, Marshal.SizeOf<BITMAP>(), out info) == 0) return null;
            int width = info.bmWidth, height = Math.Abs(info.bmHeight);
            if (width <= 0 || height <= 0 || width > 512 || height > 512) return null;

            BITMAPINFOHEADER header = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // Top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            IntPtr dc = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref header, DIB_RGB_COLORS) != height) return null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }

            // The shell hands back premultiplied alpha; an icon without any alpha is fully opaque.
            bool anyAlpha = false, premultiplied = true;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a != 0) anyAlpha = true;
                if (pixels[i] > a || pixels[i + 1] > a || pixels[i + 2] > a) premultiplied = false;
            }
            if (!anyAlpha)
                for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            PixelFormat format = anyAlpha && premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32;

            BitmapSource source = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
            if (width != Size || height != Size)
            {
                // Keep only a 64x64 copy in memory, not a 256x256 one from BIGGERSIZEOK
                TransformedBitmap scaled = new TransformedBitmap(source, new ScaleTransform((double)Size / width, (double)Size / height));
                int scaledWidth = scaled.PixelWidth, scaledHeight = scaled.PixelHeight;
                byte[] small = new byte[scaledWidth * 4 * scaledHeight];
                scaled.CopyPixels(small, scaledWidth * 4, 0);
                source = BitmapSource.Create(scaledWidth, scaledHeight, 96, 96, scaled.Format, null, small, scaledWidth * 4);
            }
            source.Freeze();
            return source;
        }
    }
}
