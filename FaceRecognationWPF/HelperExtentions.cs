using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FaceRecognationWPF.Extentions
{
    public static class HelperExtentions
    {
        public static BitmapSource ToBitmapSource(this byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }

    }
}
