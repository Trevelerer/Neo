﻿using System;
using System.Collections.Generic;
using SharpDX;

namespace WoWEditor6.IO.Files.Terrain
{
    abstract class MapChunk : IDisposable
    {
        public int IndexX { get; protected set; }
        public int IndexY { get; protected set; }

        public int StartVertex => (IndexX + IndexY * 16) * 145;

        public AdtVertex[] Vertices { get; } = new AdtVertex[145];
        public uint[] AlphaValues { get; } = new uint[4096];
        public IList<Graphics.Texture> Textures { get; protected set; }
        public BoundingBox BoundingBox { get; protected set; }
        public BoundingBox ModelBox { get; protected set; }
        public float[] TextureScales { get; protected set; }

        public int[] DoodadReferences { get; protected set; } = new int[0];

        public abstract void AsyncLoad();
        public abstract void Dispose();
    }
}