using System.Drawing;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace PictureTestApp
{
    public partial class Form1 : Form
    {
        private const int ProgressUpdateInterval = 5; // Интервал обновления прогресса в процентах (каждые 5%)
        private List<Bitmap> _bitmaps = new List<Bitmap>(101); // Список для хранения всех промежуточных изображений
        private Random _random = new Random(); // Генератор случайных чисел для выбора пикселей

        public Form1()
        {
            InitializeComponent(); // Инициализация компонентов формы
        }

        /// <summary>
        /// Обрабатывает событие открытия файла через меню, загружает изображение и запускает его обработку.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы события.</param>
        private async void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK) // Если пользователь выбрал файл
            {
                this.Invoke(new Action(() => this.Text = $"{0}%")); // Устанавливаем начальный прогресс в заголовке формы
                var sw = Stopwatch.StartNew(); // Запускаем таймер для измерения времени выполнения
                this.menuStrip.Enabled = this.trackBar.Enabled = false; // Отключаем элементы управления
                pictureBox.Image = null; // Очищаем текущее изображение в pictureBox
                _bitmaps.Clear(); // Очищаем список предыдущих битмапов
                var bitmap = new Bitmap(openFileDialog.FileName); // Загружаем выбранное изображение
                await Task.Run(() => { RunProcessing(bitmap); }); // Асинхронно обрабатываем изображение
                pictureBox.Image = _bitmaps[trackBar.Value]; // Показываем изображение, соответствующее текущему значению trackBar
                this.menuStrip.Enabled = this.trackBar.Enabled = true; // Включаем элементы управления обратно
                sw.Stop(); // Останавливаем таймер
                // Отображаем время выполнения в минутах и секундах
                this.Text = $"{Math.Floor(sw.Elapsed.TotalSeconds / 60)} minutes " +
                            $"{Math.Round(sw.Elapsed.TotalSeconds % 60, 3)} seconds";
            }
        }

        /// <summary>
        /// Извлекает список пикселей из заданного изображения, используя прямой доступ к памяти.
        /// </summary>
        /// <param name="bitmap">Исходное изображение для обработки.</param>
        /// <returns>Список объектов Pixel с координатами и цветами.</returns>
        public List<Pixel> GetPixels(Bitmap bitmap)
        {
            var pixels = new List<Pixel>(bitmap.Width * bitmap.Height); // Создаем список с заранее выделенной емкостью
            // Блокируем память изображения для быстрого чтения данных
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0; // Указатель на начало данных изображения
                for (int y = 0; y < bitmap.Height; y++) // Проходим по всем строкам
                {
                    for (int x = 0; x < bitmap.Width; x++) // Проходим по всем столбцам
                    {
                        // Вычисляем смещение для текущего пикселя (строка * stride + столбец * 4 байта)
                        int offset = (y * data.Stride) + (x * 4);
                        // Создаем объект Pixel с координатами и цветом (ARGB)
                        pixels.Add(new Pixel
                        {
                            Point = new Point(x, y),
                            Color = Color.FromArgb(ptr[offset + 3], ptr[offset + 2], ptr[offset + 1], ptr[offset])
                        });
                    }
                }
            }

            bitmap.UnlockBits(data); // Разблокируем память изображения
            return pixels; // Возвращаем список пикселей
        }

        /// <summary>
        /// Обрабатывает изображение, создавая последовательность битмапов с увеличивающимся количеством пикселей.
        /// </summary>
        /// <param name="bitmap">Исходное изображение для обработки.</param>
        private void RunProcessing(Bitmap bitmap)
        {
            var pixels = GetPixels(bitmap); // Получаем список всех пикселей изображения
            var pixelsInStep = (bitmap.Width * bitmap.Height) / 100; // Количество пикселей, добавляемых за шаг (1% от общего числа)
            var currentPixelList = new List<Pixel>(pixels.Count - pixelsInStep); // Список для хранения текущих выбранных пикселей

            _bitmaps.Add(new Bitmap(bitmap.Width, bitmap.Height)); // Добавляем пустой битмап как начальное изображение
            for (int i = 1; i < trackBar.Maximum; i++) // Проходим по всем шагам (от 1% до 99%)
            {
                CurrentPixelListUpdate(pixels, pixelsInStep, currentPixelList); // Обновляем список текущих пикселей
                Bitmap currentBitmap = new Bitmap(bitmap.Width, bitmap.Height); // Создаем новый битмап для текущего шага
                UpdateCurrentBitmap(currentPixelList, currentBitmap); // Заполняем битмап выбранными пикселями
                _bitmaps.Add(currentBitmap); // Добавляем битмап в список

                // Обновляем прогресс в UI каждые ProgressUpdateInterval процентов
                if (i % ProgressUpdateInterval == 0)
                {
                    this.Invoke(new Action(() => this.Text = $"{i}%"));
                }
            }
            _bitmaps.Add(bitmap); // Добавляем полное исходное изображение как финальный шаг
        }

        /// <summary>
        /// Обновляет заданный битмап, записывая в него пиксели из списка с использованием прямого доступа к памяти.
        /// </summary>
        /// <param name="currentPixelList">Список пикселей для записи.</param>
        /// <param name="bitmap">Битмап, в который записываются пиксели.</param>
        private void UpdateCurrentBitmap(List<Pixel> currentPixelList, Bitmap bitmap)
        {
            // Блокируем память битмапа для прямой записи данных
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0; // Указатель на начало данных битмапа
                // Параллельно записываем все пиксели в память
                Parallel.ForEach(currentPixelList, pixel =>
                {
                    // Вычисляем смещение для текущего пикселя
                    int offset = (pixel.Point.Y * data.Stride) + (pixel.Point.X * 4);
                    ptr[offset] = pixel.Color.B;     // Записываем синий компонент
                    ptr[offset + 1] = pixel.Color.G; // Записываем зеленый компонент
                    ptr[offset + 2] = pixel.Color.R; // Записываем красный компонент
                    ptr[offset + 3] = pixel.Color.A; // Записываем альфа-компонент (прозрачность)
                });
            }

            bitmap.UnlockBits(data); // Разблокируем память битмапа
        }

        /// <summary>
        /// Случайно выбирает заданное количество пикселей из списка и обновляет списки пикселей.
        /// </summary>
        /// <param name="pixels">Исходный список пикселей, из которого выбираются элементы.</param>
        /// <param name="pixelsInStep">Количество пикселей для выбора за один шаг.</param>
        /// <param name="currentPixelList">Список, в который добавляются выбранные пиксели.</param>
        private void CurrentPixelListUpdate(List<Pixel> pixels, int pixelsInStep, List<Pixel> currentPixelList)
        {
            pixelsInStep = Math.Min(pixelsInStep, pixels.Count); // Убеждаемся, что не выбираем больше пикселей, чем осталось

            // Используем алгоритм "Fisher-Yates shuffle" для случайного выбора пикселей
            for (int j = 0; j < pixelsInStep; j++)
            {
                int index = _random.Next(pixels.Count - j); // Выбираем случайный индекс из оставшихся элементов
                currentPixelList.Add(pixels[index]); // Добавляем выбранный пиксель в текущий список
                pixels[index] = pixels[pixels.Count - 1 - j]; // Перемещаем последний элемент на место выбранного
            }

            // Удаляем все выбранные пиксели с конца списка одним вызовом
            pixels.RemoveRange(pixels.Count - pixelsInStep, pixelsInStep);
        }

        /// <summary>
        /// Обрабатывает событие прокрутки trackBar, отображая соответствующее изображение из списка.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы события.</param>
        private void trackBar_Scroll(object sender, EventArgs e)
        {
            if (_bitmaps is null || _bitmaps.Count == 0) // Проверяем, что список битмапов не пуст
                return;
            pictureBox.Image = _bitmaps[trackBar.Value]; // Показываем изображение, соответствующее текущему значению trackBar
        }
    }
}