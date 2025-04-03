using System.Drawing;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace PictureTestApp
{
    public partial class Form1 : Form
    {
        private const int ProgressUpdateInterval = 5; // �������� ���������� ��������� � ��������� (������ 5%)
        private List<Bitmap> _bitmaps = new List<Bitmap>(101); // ������ ��� �������� ���� ������������� �����������
        private Random _random = new Random(); // ��������� ��������� ����� ��� ������ ��������

        public Form1()
        {
            InitializeComponent(); // ������������� ����������� �����
        }

        /// <summary>
        /// ������������ ������� �������� ����� ����� ����, ��������� ����������� � ��������� ��� ���������.
        /// </summary>
        /// <param name="sender">�������� �������.</param>
        /// <param name="e">��������� �������.</param>
        private async void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK) // ���� ������������ ������ ����
            {
                this.Invoke(new Action(() => this.Text = $"{0}%")); // ������������� ��������� �������� � ��������� �����
                var sw = Stopwatch.StartNew(); // ��������� ������ ��� ��������� ������� ����������
                this.menuStrip.Enabled = this.trackBar.Enabled = false; // ��������� �������� ����������
                pictureBox.Image = null; // ������� ������� ����������� � pictureBox
                _bitmaps.Clear(); // ������� ������ ���������� ��������
                var bitmap = new Bitmap(openFileDialog.FileName); // ��������� ��������� �����������
                await Task.Run(() => { RunProcessing(bitmap); }); // ���������� ������������ �����������
                pictureBox.Image = _bitmaps[trackBar.Value]; // ���������� �����������, ��������������� �������� �������� trackBar
                this.menuStrip.Enabled = this.trackBar.Enabled = true; // �������� �������� ���������� �������
                sw.Stop(); // ������������� ������
                // ���������� ����� ���������� � ������� � ��������
                this.Text = $"{Math.Floor(sw.Elapsed.TotalSeconds / 60)} minutes " +
                            $"{Math.Round(sw.Elapsed.TotalSeconds % 60, 3)} seconds";
            }
        }

        /// <summary>
        /// ��������� ������ �������� �� ��������� �����������, ��������� ������ ������ � ������.
        /// </summary>
        /// <param name="bitmap">�������� ����������� ��� ���������.</param>
        /// <returns>������ �������� Pixel � ������������ � �������.</returns>
        public List<Pixel> GetPixels(Bitmap bitmap)
        {
            var pixels = new List<Pixel>(bitmap.Width * bitmap.Height); // ������� ������ � ������� ���������� ��������
            // ��������� ������ ����������� ��� �������� ������ ������
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0; // ��������� �� ������ ������ �����������
                for (int y = 0; y < bitmap.Height; y++) // �������� �� ���� �������
                {
                    for (int x = 0; x < bitmap.Width; x++) // �������� �� ���� ��������
                    {
                        // ��������� �������� ��� �������� ������� (������ * stride + ������� * 4 �����)
                        int offset = (y * data.Stride) + (x * 4);
                        // ������� ������ Pixel � ������������ � ������ (ARGB)
                        pixels.Add(new Pixel
                        {
                            Point = new Point(x, y),
                            Color = Color.FromArgb(ptr[offset + 3], ptr[offset + 2], ptr[offset + 1], ptr[offset])
                        });
                    }
                }
            }

            bitmap.UnlockBits(data); // ������������ ������ �����������
            return pixels; // ���������� ������ ��������
        }

        /// <summary>
        /// ������������ �����������, �������� ������������������ �������� � ��������������� ����������� ��������.
        /// </summary>
        /// <param name="bitmap">�������� ����������� ��� ���������.</param>
        private void RunProcessing(Bitmap bitmap)
        {
            var pixels = GetPixels(bitmap); // �������� ������ ���� �������� �����������
            var pixelsInStep = (bitmap.Width * bitmap.Height) / 100; // ���������� ��������, ����������� �� ��� (1% �� ������ �����)
            var currentPixelList = new List<Pixel>(pixels.Count - pixelsInStep); // ������ ��� �������� ������� ��������� ��������

            _bitmaps.Add(new Bitmap(bitmap.Width, bitmap.Height)); // ��������� ������ ������ ��� ��������� �����������
            for (int i = 1; i < trackBar.Maximum; i++) // �������� �� ���� ����� (�� 1% �� 99%)
            {
                CurrentPixelListUpdate(pixels, pixelsInStep, currentPixelList); // ��������� ������ ������� ��������
                Bitmap currentBitmap = new Bitmap(bitmap.Width, bitmap.Height); // ������� ����� ������ ��� �������� ����
                UpdateCurrentBitmap(currentPixelList, currentBitmap); // ��������� ������ ���������� ���������
                _bitmaps.Add(currentBitmap); // ��������� ������ � ������

                // ��������� �������� � UI ������ ProgressUpdateInterval ���������
                if (i % ProgressUpdateInterval == 0)
                {
                    this.Invoke(new Action(() => this.Text = $"{i}%"));
                }
            }
            _bitmaps.Add(bitmap); // ��������� ������ �������� ����������� ��� ��������� ���
        }

        /// <summary>
        /// ��������� �������� ������, ��������� � ���� ������� �� ������ � �������������� ������� ������� � ������.
        /// </summary>
        /// <param name="currentPixelList">������ �������� ��� ������.</param>
        /// <param name="bitmap">������, � ������� ������������ �������.</param>
        private void UpdateCurrentBitmap(List<Pixel> currentPixelList, Bitmap bitmap)
        {
            // ��������� ������ ������� ��� ������ ������ ������
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0; // ��������� �� ������ ������ �������
                // ����������� ���������� ��� ������� � ������
                Parallel.ForEach(currentPixelList, pixel =>
                {
                    // ��������� �������� ��� �������� �������
                    int offset = (pixel.Point.Y * data.Stride) + (pixel.Point.X * 4);
                    ptr[offset] = pixel.Color.B;     // ���������� ����� ���������
                    ptr[offset + 1] = pixel.Color.G; // ���������� ������� ���������
                    ptr[offset + 2] = pixel.Color.R; // ���������� ������� ���������
                    ptr[offset + 3] = pixel.Color.A; // ���������� �����-��������� (������������)
                });
            }

            bitmap.UnlockBits(data); // ������������ ������ �������
        }

        /// <summary>
        /// �������� �������� �������� ���������� �������� �� ������ � ��������� ������ ��������.
        /// </summary>
        /// <param name="pixels">�������� ������ ��������, �� �������� ���������� ��������.</param>
        /// <param name="pixelsInStep">���������� �������� ��� ������ �� ���� ���.</param>
        /// <param name="currentPixelList">������, � ������� ����������� ��������� �������.</param>
        private void CurrentPixelListUpdate(List<Pixel> pixels, int pixelsInStep, List<Pixel> currentPixelList)
        {
            pixelsInStep = Math.Min(pixelsInStep, pixels.Count); // ����������, ��� �� �������� ������ ��������, ��� ��������

            // ���������� �������� "Fisher-Yates shuffle" ��� ���������� ������ ��������
            for (int j = 0; j < pixelsInStep; j++)
            {
                int index = _random.Next(pixels.Count - j); // �������� ��������� ������ �� ���������� ���������
                currentPixelList.Add(pixels[index]); // ��������� ��������� ������� � ������� ������
                pixels[index] = pixels[pixels.Count - 1 - j]; // ���������� ��������� ������� �� ����� ����������
            }

            // ������� ��� ��������� ������� � ����� ������ ����� �������
            pixels.RemoveRange(pixels.Count - pixelsInStep, pixelsInStep);
        }

        /// <summary>
        /// ������������ ������� ��������� trackBar, ��������� ��������������� ����������� �� ������.
        /// </summary>
        /// <param name="sender">�������� �������.</param>
        /// <param name="e">��������� �������.</param>
        private void trackBar_Scroll(object sender, EventArgs e)
        {
            if (_bitmaps is null || _bitmaps.Count == 0) // ���������, ��� ������ �������� �� ����
                return;
            pictureBox.Image = _bitmaps[trackBar.Value]; // ���������� �����������, ��������������� �������� �������� trackBar
        }
    }
}