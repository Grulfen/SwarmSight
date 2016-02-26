using Classes;
using SwarmSight.Filters;
using SwarmSight.VideoPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SwarmSight.MotionTracking
{
    public class MotionDetector : VideoProcessorBase
    {
        private FrameBuffer<Frame> buffer = new FrameBuffer<Frame>();
        private int framesNeeed = 2;
        public int Threshold = 30;

        private double LeftBoundPCT;
        private double RightBoundPCT;
        private double TopBoundPCT;
        private double BottomBoundPCT;

        public override object OnProcessing(Frame frame)
        {
            var result = new FrameComparerResults();

            var current = frame.Clone();

            buffer.AddLast(current);

            if (buffer.Count > framesNeeed)
            {
                var oldest = buffer.First.Value;
                oldest.Dispose();
                buffer.Remove(oldest);
            }

            if (buffer.Count == framesNeeed && current.SameSizeAs(buffer.First.Value))
            {
                var prev = buffer.First.Value;

                var roi = new Rect
                (
                    new Point(frame.Width * LeftBoundPCT, frame.Height * TopBoundPCT),
                    new Point(frame.Width * RightBoundPCT, frame.Height * BottomBoundPCT)
                );

                var changedPixels = current.ChangeExtentPoints(prev, Threshold, roi);

                result.Threshold = Threshold;

                result.ChangedPixels = changedPixels;
                result.ChangedPixelsCount = changedPixels.Count;

                result.Frame = frame;
                result.FrameIndex = frame.FrameIndex;
                result.FrameTime = frame.FrameTime;
            }

            return result;
        }

        public void SetBounds(double leftPercent, double topPercent, double rightPercent, double bottomPercent)
        {
            LeftBoundPCT = leftPercent;
            RightBoundPCT = rightPercent;
            TopBoundPCT = topPercent;
            BottomBoundPCT = bottomPercent;
        }
    }

}
