using ColorPointsOptimization_Library;
using ColorPointsOptimization_WindowsForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorPointsOptimization
{
    public partial class FullscreenForm : Form
    {
        private ColorPointsContainer_WindowsForms _colorPointContainer;
        private bool _isLoaded;


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]//Asked AI for a way to keep the screen from slumbering
        static extern uint SetThreadExecutionState(uint esFlags);
        // Flags to keep the screen and system awake
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        public FullscreenForm()
        {
            InitializeComponent();

            // Remove borders and title bar
            FormBorderStyle = FormBorderStyle.None;
            // Fill the screen
            WindowState = FormWindowState.Maximized;

            DoubleBuffered = true;
            Application.Idle += HandleRendering;
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _colorPointContainer = new(new PointF(0, 0), Size, 1234, 10);
            _isLoaded = true;

            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);//Make the screen never slumber while the program lives
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            SetThreadExecutionState(ES_CONTINUOUS);//Technically not needed, as the program just dies after closing
        }

        // Allow user to exit with Escape key
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            if (keyData == Keys.Space) { display = !display; return true; }
            if (keyData == Keys.R) { _colorPointContainer.ResetPosVel(); return true; }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void HandleRendering(object sender, EventArgs e)
        {
            Invalidate();
        }

        Queue<long> frameDurations = new Queue<long>();
        bool display = true;
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_isLoaded) return;

            Stopwatch sw = Stopwatch.StartNew();
            _colorPointContainer.TakeStep(1);
            long msSimulation = sw.ElapsedMilliseconds;

            Graphics g = e.Graphics;
            var borders = g.ClipBounds;

            #region Drawing
            long msAllocate; long msLoadTo; long msExecute; long msLoadFrom;
            sw.Restart();
            var colors = _colorPointContainer.CalculateColors(out msAllocate, out msLoadTo, out msExecute, out msLoadFrom);
            long msCalculateColors = sw.ElapsedMilliseconds;
            var bitmap = ArrayToBitmap(colors, (int)borders.Width, (int)borders.Height);
            long msBitmap = sw.ElapsedMilliseconds - msCalculateColors;
            g.DrawImage(bitmap, _colorPointContainer.Position);
            #endregion

            long now = DateTime.Now.Ticks;
            frameDurations.Enqueue(now);
            while (now - frameDurations.Peek() > 10_000_000) frameDurations.Dequeue();
            float framerate = frameDurations.Count();
            if (display)
            {
                string str;
                g.FillRectangle(Brushes.White, new Rectangle(0, 0, (int)borders.Width, Font.Height * 4));
                str = $"Framrate: {framerate}fps";
                g.DrawString(str, Font, Brushes.Black, (borders.Width - g.MeasureString(str, Font).Width) / 2, Font.Height * 0);
                str = $"Simulation: {msSimulation}ms";
                g.DrawString(str, Font, Brushes.Black, (borders.Width - g.MeasureString(str, Font).Width) / 2, Font.Height * 1);
                str = $"CalculateColors: {{Allocate: {msAllocate}ms | LoadTo: {msLoadTo}ms | Execute: {msExecute}ms | LoadFrom: {msLoadFrom}ms | total: {msCalculateColors}ms }}";
                g.DrawString(str, Font, Brushes.Black, (borders.Width - g.MeasureString(str, Font).Width) / 2, Font.Height * 2);
                str = $"Bitmap: {msBitmap}ms";
                g.DrawString(str, Font, Brushes.Black, (borders.Width - g.MeasureString(str, Font).Width) / 2, Font.Height * 3);

                g.DrawString($"Size: {Size} => Area: {Size.Width * Size.Height}", Font, Brushes.Black, 0, Font.Height * 0);
                g.DrawString($"[SPACE] to open/close help-display", Font, Brushes.Black, 0, Font.Height * 1);
                g.DrawString($"[R] to reset positions and velocities", Font, Brushes.Black, 0, Font.Height * 2);
            }
        }

        private Bitmap ArrayToBitmap(int[] data, int width, int height)//Mostly AI
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);

            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);

            bmp.UnlockBits(bmpData);
            return bmp;
        }

    }
}
