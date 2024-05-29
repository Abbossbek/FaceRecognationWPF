using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Camera;
using FaceAiSharp.Extensions;

using FaceAiSharp;

using FaceRecognationWPF.Models;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace FaceRecognationWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>  
    public partial class MainWindow : System.Windows.Window
    {
        private IFaceDetectorWithLandmarks det;
        private IFaceEmbeddingsGenerator rec;
        UsbCamera camera;
        Timer faceTimer;
        ObservableCollection<Person> persons = new();
        ObservableCollection<Person> savedPersons = new();
        public MainWindow()
        {
            InitializeComponent();
            var devices = UsbCamera.FindDevices();
            if (devices.Length == 0) return;
            var cameraIndex = 1;
            var format = UsbCamera.GetVideoFormat(cameraIndex).FirstOrDefault();

            det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
            rec = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();

            camera = new UsbCamera(cameraIndex, format);
            camera.PreviewCaptured += Camera_PreviewCaptured;
            faceTimer = new Timer(faceTimerCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
            lbPersons.ItemsSource = persons;
            lbSavedPersons.ItemsSource = savedPersons;

        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            camera.Start();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            camera.Stop();
            base.OnClosing(e);
        }
        private void Camera_PreviewCaptured(BitmapSource source)
        {
            Dispatcher.Invoke(() =>
            {
                if (source != null && source != image.Source)
                    image.Source = source;
            });
        }
        private async void faceTimerCallback(object state)
        {
            var b = camera.GetBitmap();
            using var img = SixLabors.ImageSharp.Image.Load<Rgb24>(BitmapSourceToByteArray(b));
            IReadOnlyCollection<FaceDetectorResult> faces = det.DetectFaces(img);
            if (faces.Count == 0) return;
            var currentPersons = new List<Person>();
            Parallel.ForEach(faces, async face =>
            {
                var f = persons.FirstOrDefault(p =>
                    Math.Abs(p.FaceRectangle.Box.X - face.Box.X) < 50 &&
                    Math.Abs(p.FaceRectangle.Box.Y - face.Box.Y) < 50 &&
                    Math.Abs(p.FaceRectangle.Box.Width - face.Box.Width) < 35 &&
                    Math.Abs(p.FaceRectangle.Box.Height - face.Box.Height) < 35);
                if (f == null)
                {
                    var person = new Person
                    {
                        Id = currentPersons.Count + 1,
                        FaceRectangle = face,
                        Name = $"Unknown {Guid.NewGuid().ToString().Substring(0, 5)}",
                    };
                    currentPersons.Add(person);
                }
                else
                {
                    f.FaceRectangle = face;
                    currentPersons.Add(f);
                }
            });
            // Detect faces (rectangles
            await Dispatcher.InvokeAsync(() =>
            {
                // Clear the canvas.
                canvas.Children.Clear();
                foreach (var rect in currentPersons.Select(x => x.FaceRectangle))
                {
                    var rectangle = new System.Windows.Shapes.Rectangle
                    {
                        Width = (int)rect.Box.Width * (canvas.Width / img.Width),
                        Height = (int)rect.Box.Height * (canvas.Height / img.Height),
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent
                    };
                    canvas.Children.Add(rectangle);
                    Canvas.SetLeft(rectangle, (int)rect.Box.X * (canvas.Width / img.Width));
                    Canvas.SetTop(rectangle, (int)rect.Box.Y * (canvas.Height / img.Height));
                }
            });
            // Extract embeddings
            foreach (var person in currentPersons)
            {
                using var image = img.Clone();
                rec.AlignFaceUsingLandmarks(image, person.FaceRectangle.Landmarks!);
                var imgBase64 = image.ToBase64String(SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance).Substring(23);
                person.Image = ByteArrayToBitmapSource(Convert.FromBase64String(imgBase64));
                person.Embedding = rec.GenerateEmbedding(image);
                var fp = savedPersons.FirstOrDefault(p => p.Embedding != null && FaceAiSharp.Extensions.GeometryExtensions.Dot(p.Embedding, person.Embedding) >= 0.42);
                if (fp != null)
                {
                    person.Name = fp.Name;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock textBlock = new TextBlock
                        {
                            Text = person.Name,
                            Foreground = Brushes.Red,
                            FontSize = 20
                        };
                        canvas.Children.Add(textBlock);
                        Canvas.SetLeft(textBlock, (int)person.FaceRectangle.Box.X * (canvas.Width / img.Width));
                        Canvas.SetTop(textBlock, (int)person.FaceRectangle.Box.Y * (canvas.Height / img.Height) - 20);
                    });
                }
            }

            lock (persons)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var person in persons.ToList())
                    {
                        if (!currentPersons.Contains(person))
                        {
                            persons.Remove(person);
                        }
                    }
                    foreach (var person in currentPersons)
                    {
                        if (!persons.Contains(person))
                        {
                            persons.Add(person);
                        }
                    }
                });
            }
        }
        private static byte[] BitmapSourceToByteArray(BitmapSource source)
        {
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
        private static BitmapSource ByteArrayToBitmapSource(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }

        private void btnSavePerson_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Person person)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    savedPersons.Add((Person)person.Clone());
                });
            }
        }
    }
}