using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;

namespace ColorPointsOptimization_Library
{
    public class ColorPointsContainer
    {
        protected Accelerator _accelerator;
        protected Action<Index1D, ArrayView<PointF>, ArrayView<Vector2>, SizeF, float, float> _kernel_TakeStep;

        public PointF Position { get; set; }
        public SizeF Size { get; set; }

        protected int _count;
        protected int[] _colors { get; set; }//ARGB is an integer
        protected PointF[] _points { get; set; }
        protected Vector2[] _velocities { get; set; }
        protected float _speedFactor;

        public ColorPointsContainer(float x, float y, float width, float height, int colorPointCount, float speedFactor) : this(new PointF(x, y), new SizeF(width, height), colorPointCount, speedFactor) { }
        public ColorPointsContainer(RectangleF container, int colorPointCount, float speedFactor) : this(container.Location, container.Size, colorPointCount, speedFactor) { }
        public ColorPointsContainer(PointF position, SizeF size, int colorPointCount, float speedFactor)
        {
            Position = position;
            Size = size;

            _count = colorPointCount;

            _colors = new int[colorPointCount];
            _points = new PointF[colorPointCount];
            _velocities = new Vector2[colorPointCount];

            float rgbFactor = 16_777_216f / (colorPointCount - 1);
            for (int i = 0; i < colorPointCount; i++)
            {
                float rgb = rgbFactor * i;
                _colors[i] = ((int)rgb) - 16777216;
            }
            ResetPosVel();

            Context context = Context.CreateDefault();
            _accelerator = context.GetPreferredDevice(false).CreateAccelerator(context);
            _kernel_TakeStep = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<PointF>, ArrayView<Vector2>, SizeF, float, float>(AddKernel_TakeStep);

            _speedFactor = speedFactor;
        }
        
        public void ResetPosVel()
        {
            Random rng = new Random();
            for (int i = 0; i < _count; i++)
            {
                _points[i].X = rng.Next(0, (int)Size.Width);
                _points[i].Y = rng.Next(0, (int)Size.Height);
                _velocities[i] = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                _velocities[i] = Vector2.Normalize(_velocities[i]);
            }
        }

        public void TakeStep(float deltaTime)
        {
            using var devicePoints = _accelerator.Allocate1D(_points);
            using var deviceVelocities = _accelerator.Allocate1D(_velocities);

            _kernel_TakeStep(_count, devicePoints.View, deviceVelocities.View, Size, _speedFactor, deltaTime);

            devicePoints.CopyToCPU(_points);
            deviceVelocities.CopyToCPU(_velocities);
        }

        #region Kernels
        private static void AddKernel_TakeStep(Index1D i, ArrayView<PointF> points, ArrayView<Vector2> velocities, SizeF size, float speedFactor, float deltaTime)
        {
            PointF pos = points[i];
            Vector2 vel = velocities[i];

            float deltaX = vel.X * deltaTime * speedFactor;
            float deltaY = vel.Y * deltaTime * speedFactor;

            pos.X += deltaX;
            pos.Y += deltaY;

            while (pos.X < 0 || pos.X > size.Width)
            {
                if (pos.X < 0) pos.X = -pos.X;
                else pos.X = -pos.X + size.Width * 2;
                vel.X = -vel.X;
            }
            while (pos.Y < 0 || pos.Y > size.Height)
            {
                if (pos.Y < 0) pos.Y = -pos.Y;
                else pos.Y = -pos.Y + size.Height * 2;
                vel.Y = -vel.Y;
            }

            points[i] = pos;
            velocities[i] = vel;
        }
        #endregion

    }
}
