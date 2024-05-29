using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FaceRecognationWPF.Models
{
    public class Person : BindableBase, ICloneable
    {
        private string name;
        private BitmapSource image;

        public int Id { get; set; }
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }
        public BitmapSource Image
        {
            get => image;
            set => SetProperty(ref image, value);
        }
        public float[] Embedding { get; set; }
        public FaceAiSharp.FaceDetectorResult FaceRectangle { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
