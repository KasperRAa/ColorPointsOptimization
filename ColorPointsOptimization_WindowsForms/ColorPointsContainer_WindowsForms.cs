using ColorPointsOptimization_Library;
using ILGPU;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorPointsOptimization_WindowsForms
{
    internal class ColorPointsContainer_WindowsForms : ColorPointsContainer
    {
        private Action<Index2D, ArrayView<PointF>, ArrayView<int>, ArrayView<int>, int, int> _kernel_CalculateColors;

        public ColorPointsContainer_WindowsForms(float x, float y, float width, float height, int colorPointCount, float speedFactor) : this(new PointF(x, y), new SizeF(width, height), colorPointCount, speedFactor) { }
        public ColorPointsContainer_WindowsForms(RectangleF container, int colorPointCount, float speedFactor) : this(container.Location, container.Size, colorPointCount, speedFactor) { }
        public ColorPointsContainer_WindowsForms(PointF position, SizeF size, int colorPointCount, float speedFactor) : base(position, size, colorPointCount, speedFactor)
        {
            _kernel_CalculateColors = _accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView<PointF>, ArrayView<int>, ArrayView<int>, int, int>(AddKernel_CalculateColors);
        }

        public int[] CalculateColors(out long msAllocate, out long msLoadTo, out long msExecute, out long msLoadFrom)
        {
            int width = (int)Size.Width;
            int height = (int)Size.Height;
            int area = width * height;
            int[] nearestColor = new int[area];
            Stopwatch sw = Stopwatch.StartNew();//
            var index = new Index2D(width, height);
            using var devicePoints = _accelerator.Allocate1D(_points);
            using var deviceColors = _accelerator.Allocate1D(_colors);
            using var deviceResult = _accelerator.Allocate1D(nearestColor);
            msAllocate = sw.ElapsedMilliseconds;//
            deviceResult.CopyFromCPU(nearestColor);
            msLoadTo = sw.ElapsedMilliseconds - msAllocate;//
            _kernel_CalculateColors(index, devicePoints.View, deviceColors.View, deviceResult.View, _count, width);
            _accelerator.Synchronize();
            msExecute = sw.ElapsedMilliseconds - msAllocate - msLoadTo;//
            deviceResult.CopyToCPU(nearestColor);
            msLoadFrom = sw.ElapsedMilliseconds - msAllocate - msLoadTo - msExecute;//
            return nearestColor;
        }
        private static void AddKernel_CalculateColors(Index2D xy, ArrayView<PointF> points, ArrayView<int> colors, ArrayView<int> result, int count, int width)
        {
            int color = 0;
            float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float dx = xy.X - points[i].X;
                float dy = xy.Y - points[i].Y;
                float dl = dx * dx + dy * dy;
                if (dl < distance)
                {
                    distance = dl;
                    color = colors[i];
                }
            }
            result[xy.Y * width + xy.X] = color;
        }

    }
}
