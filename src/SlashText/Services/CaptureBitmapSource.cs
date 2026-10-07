using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SlashText.Services;

/// <summary>Pixel transfer for editing previews; no PNG compression on the UI thread.</summary>
public static class CaptureBitmapSource
{
    public static BitmapSource Create(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            BitmapSource image;
            if (data.Stride > 0)
                image = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32,
                    null, data.Scan0, checked(data.Stride * bitmap.Height), data.Stride);
            else
            {
                var stride = checked(bitmap.Width * 4);
                var pixels = new byte[checked(stride * bitmap.Height)];
                for (var y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * stride, stride);
                image = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            }
            image.Freeze(); return image;
        }
        finally { bitmap.UnlockBits(data); }
    }
}
