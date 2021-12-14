﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
namespace DeskTopTimer
{
    public class BitmapToImageSourceHelper
    {
        /// <summary>
        /// 将bitmap转换为BitmapImage
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        static public BitmapImage Convert(Bitmap src)
        {
            try
            {
                if (src == null)
                    return null;
                MemoryStream ms = new MemoryStream();
                ((System.Drawing.Bitmap)src).Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                ms.Seek(0, SeekOrigin.Begin);
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();

                return image;


            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }
    }

    public static class MyDirectory
    {   // Regex version
        public static IEnumerable<string> GetFiles(string path,
                            string searchPatternExpression = "",
                            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Regex reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
            return Directory.EnumerateFiles(path, "*", searchOption)
                            .Where(file =>
                                     reSearchPattern.IsMatch(Path.GetExtension(file)));
        }

        // Takes same patterns, and executes in parallel
        public static IEnumerable<string> GetFiles(string path,
                            string[] searchPatterns,
                            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return searchPatterns.AsParallel()
                   .SelectMany(searchPattern =>
                          Directory.EnumerateFiles(path, searchPattern, searchOption));
        }
    }

    public class ImageTool
    {
        public struct Dpi
        {
            public double X { get; set; }

            public double Y { get; set; }

            public Dpi(double x, double y)
            {
                X = x;
                Y = y;
            }
        }


        public static BitmapImage GetImage(string imagePath)
        {
            try
            {
                BitmapImage bitmap = null;

                if (imagePath.StartsWith("pack://"))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    Uri current;
                    if (Uri.TryCreate(imagePath, UriKind.RelativeOrAbsolute, out current))
                    {
                        bitmap.UriSource = current;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                }
                else if (File.Exists(imagePath))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    using (Stream ms = new MemoryStream(File.ReadAllBytes(imagePath)))
                    {
                        if (ms.Length <= 0)
                        {
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return new BitmapImage();
            }

        }



        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);


        public static Dpi GetDpiByGraphics()
        {
            double dpiX;
            double dpiY;

            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpiX = graphics.DpiX;
                dpiY = graphics.DpiY;
            }

            return new Dpi(dpiX, dpiY);
        }

        public static ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }


        static public DrawingImage CreateABitMap(string DrawingText, double FontSize, Typeface cur)
        {
            var pixels = new byte[1080 * 1080 * 4];
            for (int i = 0; i < 1080 * 1080 * 4; i += 4)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }
            BitmapSource bitmapSource = BitmapSource.Create(1080, 1080, 96, 96, PixelFormats.Pbgra32, null, pixels, 1080 * 4);
            var visual = new DrawingVisual();

            var CenterX = 540;
            var CenterY = 540;
            var Dpi = GetDpiByGraphics();//GetSystemDpi
            var formatText = new FormattedText(DrawingText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        cur, FontSize, System.Windows.Media.Brushes.White, Dpi.X / 96d);
            System.Windows.Point textLocation = new System.Windows.Point(CenterX - formatText.WidthIncludingTrailingWhitespace / 2, CenterY - formatText.Height / 2);

            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.DrawImage(bitmapSource, new Rect(0, 0, 1080, 1080));
                drawingContext.DrawText(formatText, textLocation);
            }
            return new DrawingImage(visual.Drawing);
        }

        static public bool SaveDrawingToFile(DrawingImage drawing, string fileName, double scale = 1d)
        {
            drawing.Freeze();
            return System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var drawingImage = new System.Windows.Controls.Image { Source = drawing };
                    var width = drawing.Width * scale;
                    var height = drawing.Height * scale;
                    drawingImage.Arrange(new Rect(0, 0, width, height));

                    var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(drawingImage);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using (var stream = new FileStream(fileName, FileMode.Create))
                    {
                        encoder.Save(stream);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    return false;
                }
            });


        }




    }

    public class Configure
    {
        public bool? isOnlineSeSeMode { set;get;}

        public string? localFilePath { set;get;}

        public string? currentSeSeApi { set;get;}

        public double? windowWidth { set;get;}

        public double? windowHeight { set;get;}

        public double? backgroundImgOpacity { set;get;}

        public long? maxCacheCount { set;get;}

        public long? flushTime { set;get;}
    }

    public static class CommonFuncTool
    {
        /// <summary>
        /// 压缩文件
        /// </summary>
        /// <param name="DestName">压缩文件的存储绝对路径</param>
        /// <param name="FilePathToEntryDic">文件名：压缩文件内文件名Map</param>
        /// <param name="compressionLevel">压缩等级</param>
        /// <returns></returns>
        public static bool ZipFiles(string DestName, Dictionary<string, string> FilePathToEntryDic, CompressionLevel compressionLevel = CompressionLevel.Fastest)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using (ZipArchive zipArchive = ZipFile.Open(DestName, ZipArchiveMode.Create, Encoding.GetEncoding("GBK")))
                {

                    foreach (var itr in FilePathToEntryDic.Keys)
                    {
                        zipArchive.CreateEntryFromFile(itr, FilePathToEntryDic[itr], compressionLevel);
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 解压文件
        /// </summary>
        /// <param name="ZipFilePath">压缩文件所在路径</param>
        /// <param name="UnZipPath">解压到</param>
        /// <returns></returns>
        public static bool UnZipFile(string ZipFilePath, string UnZipPath)
        {
            try
            {
                var startPath = System.IO.Path.GetDirectoryName(ZipFilePath);
                if (Directory.Exists(UnZipPath))
                {
                    Directory.Delete(UnZipPath, true);
                }
                ZipFile.ExtractToDirectory(ZipFilePath, UnZipPath);
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("解压出现问题" + ex.Message);
                return false;
            }
        }
    }

    public static class FileMapper
    {
        public static string PictureCacheDir
        {
            get
            {
                string currentDir = System.Environment.CurrentDirectory + "\\PictureCache";
                if (!Directory.Exists(currentDir))
                    Directory.CreateDirectory(currentDir);
                return currentDir;
            }
        }

        public static string VideoCacheDir
        {
            get
            {
                string currentDir = System.Environment.CurrentDirectory + "\\Videos";
                if (!Directory.Exists(currentDir))
                    Directory.CreateDirectory(currentDir);
                return currentDir;
            }
        }
      
        public static string NormalSeSePictureDir
        {
            get
            {
                var cur = Path.Combine(PictureCacheDir,"Normal");
                if(!Directory.Exists(cur))
                    Directory.CreateDirectory(cur); 
                return cur;
            }
        }

        public static string PixivSeSePictureDir
        {
            get
            {
                var cur = Path.Combine(PictureCacheDir, "Pixiv");
                if (!Directory.Exists(cur))
                    Directory.CreateDirectory(cur);
                return cur;
            }
        }

        public static string LocalSeSePictureDir
        {
            get
            {
                string currentFile = Path.Combine(System.Environment.CurrentDirectory, "Local");
                if(!Directory.Exists(currentFile))
                    Directory.CreateDirectory(currentFile);
                return currentFile;
            }
        }

        public static string LocalCollectionPictureDir
        {
             get
             {
               string currentFile = Path.Combine(System.Environment.CurrentDirectory, "Collect");
               if(!Directory.Exists(currentFile))
                    Directory.CreateDirectory(currentFile);
                  return currentFile; 
             }
        }

        public static string ConfigureJson
        {
            get
            {
                string currentFile = Path.Combine(System.Environment.CurrentDirectory, "Configuration.Json");
                if (!File.Exists(currentFile))
                    File.Create(currentFile).Close();
                return currentFile;
            }
        }

        public static string ModelsJson
        {
            get
            {
                string currentFile = Path.Combine(System.Environment.CurrentDirectory, "Models.Json");
                if (!File.Exists(currentFile))
                    File.Create(currentFile).Close();
                return currentFile;
            }
        }
    }
}
