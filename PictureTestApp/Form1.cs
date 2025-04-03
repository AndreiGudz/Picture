using System.Drawing;
using System.Diagnostics;

namespace PictureTestApp
{
    public partial class Form1 : Form
    {
        private List<Bitmap> _bitmaps = new List<Bitmap>();
        private Random _random = new Random();
        public Form1()
        {
            InitializeComponent();
        }

        private async void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var sw = Stopwatch.StartNew();
                this.menuStrip.Enabled = this.trackBar.Enabled = false;
                pictureBox.Image = null;
                _bitmaps.Clear();
                var bitmap = new Bitmap(openFileDialog.FileName);
                await Task.Run(() => { RunProcessing(bitmap); });
                pictureBox.Image = _bitmaps[trackBar.Value];
                this.menuStrip.Enabled = this.trackBar.Enabled = true;
                sw.Stop();
                this.Text = $"{Math.Floor(sw.Elapsed.TotalSeconds / 60)} minutes {Math.Round(sw.Elapsed.TotalSeconds % 60, 3)}";
            }
        }

        public List<Pixel> GetPixels(Bitmap bitmap)
        { 
            var pixels = new List<Pixel>(bitmap.Width * bitmap.Height);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    pixels.Add(new Pixel()
                    {
                        Point = new Point(x, y),
                        Color = bitmap.GetPixel(x, y)
                    });
                }
            }
            return pixels;
        }

        private void RunProcessing(Bitmap bitmap)
        {
            var pixels = GetPixels(bitmap);
            var pixelsInStep = (bitmap.Width * bitmap.Height) / 100;
            var currentPixelList = new List<Pixel>(pixels.Count - pixelsInStep);

            _bitmaps.Add(new Bitmap(bitmap.Width, bitmap.Height));  // пустой битмап
            for (int i = 1; i < trackBar.Maximum; i++)
            {
                CurrentPixelListUpdate(pixels, pixelsInStep, currentPixelList);
                Bitmap currentBitmap = GetNewCurrentBitmap(bitmap, currentPixelList);
                _bitmaps.Add(currentBitmap);

                this.Invoke(new Action(() =>
                {
                    this.Text = $"{i}%";
                }));
            }
            _bitmaps.Add(bitmap); // картинка целиком
        }

        private static Bitmap GetNewCurrentBitmap(Bitmap bitmap, List<Pixel> currentPixelList)
        {
            var currentBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            foreach (var pixel in currentPixelList)
                currentBitmap.SetPixel(pixel.Point.X, pixel.Point.Y, pixel.Color);
            return currentBitmap;
        }

        private void CurrentPixelListUpdate(List<Pixel> pixels, int pixelsInStep, List<Pixel> currentPixelList)
        {
            var count = pixels.Count;
            for (int j = 0; j < pixelsInStep; j++)
            {
                var index = _random.Next(pixels.Count);
                currentPixelList.Add(pixels[index]);
                pixels.RemoveAt(index);
                --count;
            }
        }

        private void trackBar_Scroll(object sender, EventArgs e)
        {
            if (_bitmaps is null || _bitmaps.Count == 0)
                return;
            pictureBox.Image = _bitmaps[trackBar.Value];
        }
    }
}
