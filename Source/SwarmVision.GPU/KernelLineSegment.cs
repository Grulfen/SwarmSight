﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cudafy;

namespace SwarmVision.Hardware
{
    [Cudafy(eCudafyType.Struct)]
    public struct KernelLineSegment
    {
        public float StartX;
        public float StartY;
        public float Dy;
        public float Dx;
        public float Product;
        public float Length;
        public float Thickness;
        public byte ColorB;
        public byte ColorG;
        public byte ColorR;
    }
}
