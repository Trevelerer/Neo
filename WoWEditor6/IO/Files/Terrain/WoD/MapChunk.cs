﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX;
using WoWEditor6.Scene;

namespace WoWEditor6.IO.Files.Terrain.WoD
{
    class MapChunk : Terrain.MapChunk
    {
        private readonly ChunkStreamInfo mMainInfo;
        private readonly ChunkStreamInfo mTexInfo;
        private readonly ChunkStreamInfo mObjInfo;

        private readonly BinaryReader mReader;
        private readonly BinaryReader mTexReader;
        private readonly BinaryReader mObjReader;

        private Mcnk mHeader;

        private IntPtr mAlphaDataCompressed;

        private readonly WeakReference<MapArea> mParent;

        private readonly List<Mcly> mLayerInfos = new List<Mcly>();

        public MapChunk(ChunkStreamInfo mainInfo, ChunkStreamInfo texInfo, ChunkStreamInfo objInfo,  int indexX, int indexY, MapArea parent)
        {
            mParent = new WeakReference<MapArea>(parent);
            mMainInfo = mainInfo;
            mTexInfo = texInfo;
            mObjInfo = objInfo;

            mReader = mainInfo.Stream;
            mTexReader = texInfo.Stream;
            mObjReader = objInfo.Stream;

            IndexX = indexX;
            IndexY = indexY;
        }

        public override void AsyncLoad()
        {
            mReader.BaseStream.Position = mMainInfo.PosStart;
            var chunkSize = mReader.ReadInt32();
            mHeader = mReader.Read<Mcnk>();
            var hasMccv = false;

            while (mReader.BaseStream.Position + 8 <= mMainInfo.PosStart + 8 + chunkSize)
            {
                var id = mReader.ReadUInt32();
                var size = mReader.ReadInt32();

                if (mReader.BaseStream.Position + size > mMainInfo.PosStart + 8 + chunkSize)
                    break;

                var cur = mReader.BaseStream.Position;

                switch(id)
                {
                    case 0x4D435654:
                        LoadMcvt();
                        break;

                    case 0x4D434E52:
                        LoadMcnr();
                        break;

                    case 0x4D434356:
                        hasMccv = true;
                        LoadMccv();
                        break;
                }

                mReader.BaseStream.Position = cur + size;
            }

            if (hasMccv == false)
            {
                for (var i = 0; i < 145; ++i)
                    Vertices[i].Color = 0x7F7F7F7F;
            }

            LoadTexData();
            LoadObjData();

            WorldFrame.Instance.MapManager.OnLoadProgress();
        }

        private void LoadObjData()
        {
            mObjReader.BaseStream.Position = mObjInfo.PosStart;
            var chunkSize = mObjReader.ReadInt32();

            while(mObjReader.BaseStream.Position + 8 <= mObjInfo.PosStart + 8 + chunkSize)
            {
                var id = mObjReader.ReadUInt32();
                var size = mObjReader.ReadInt32();

                if (mObjReader.BaseStream.Position + size > mObjInfo.PosStart + 8 + chunkSize)
                    break;

                var cur = mObjReader.BaseStream.Position;
                switch(id)
                {
                    case 0x4D435244:
                        LoadMcrd(size);
                        break;
                }

                mObjReader.BaseStream.Position = cur + size;
            }
        }

        private void LoadMcrd(int size)
        {
            DoodadReferences = mObjReader.ReadArray<int>(size / 4);
            var minPos = BoundingBox.Minimum;
            var maxPos = BoundingBox.Maximum;

            MapArea parent;
            if (mParent.TryGetTarget(out parent) == false)
                return;

            foreach (var reference in DoodadReferences)
            {
                var inst = parent.DoodadInstances[reference];
                var min = inst.BoundingBox.Minimum;
                var max = inst.BoundingBox.Maximum;

                if (min.X < minPos.X)
                    minPos.X = min.X;
                if (min.Y < minPos.Y)
                    minPos.Y = min.Y;
                if (min.Z < minPos.Z)
                    minPos.Z = min.Z;
                if (max.X > maxPos.X)
                    maxPos.X = max.X;
                if (max.Y > maxPos.Y)
                    maxPos.Y = max.Y;
                if (max.Z > maxPos.Z)
                    maxPos.Z = max.Z;
            }

            ModelBox = new BoundingBox(minPos, maxPos);
        }

        private void LoadTexData()
        {
            try
            {
                mTexReader.BaseStream.Position = mTexInfo.PosStart;
                var chunkSize = mTexReader.ReadInt32();

                while (mTexReader.BaseStream.Position + 8 <= mTexInfo.PosStart + 8 + chunkSize)
                {
                    var id = mTexReader.ReadUInt32();
                    var size = mTexReader.ReadInt32();

                    if (mTexReader.BaseStream.Position + size > mTexInfo.PosStart + 8 + chunkSize)
                        break;

                    var cur = mTexReader.BaseStream.Position;

                    switch (id)
                    {
                        case 0x4D434C59:
                            LoadMcly(size);
                            break;

                        case 0x4D43414C:
                            mAlphaDataCompressed = Marshal.AllocHGlobal(size);
                            mTexReader.ReadToPointer(mAlphaDataCompressed, size);
                            break;
                    }

                    mTexReader.BaseStream.Position = cur + size;
                }

                LoadAlpha();

                var textures = new List<Graphics.Texture>();
                MapArea parent;
                mParent.TryGetTarget(out parent);
                if (parent == null)
                    throw new InvalidOperationException("Parent got disposed but loading was still invoked");

                TextureScales = new[] { 1.0f, 1.0f, 1.0f, 1.0f };
                for (var i = 0; i < mLayerInfos.Count && i < 4; ++i)
                {
                    textures.Add(parent.GetTexture(mLayerInfos[i].TextureId));
                    TextureScales[i] = parent.GetTextureScale(mLayerInfos[i].TextureId);
                }

                Textures = textures;
            }
            finally
            {
                if (mAlphaDataCompressed != IntPtr.Zero)
                    Marshal.FreeHGlobal(mAlphaDataCompressed);
            }
        }

        private void LoadAlpha()
        {
            var nLayers = Math.Min(mLayerInfos.Count, 4);
            for(var i = 0; i < nLayers; ++i)
            {
                if ((mLayerInfos[i].Flags & 0x200) != 0)
                    LoadLayerRle(mLayerInfos[i], i);
                else if ((mLayerInfos[i].Flags & 0x100) != 0)
                {
                    if (WorldFrame.Instance.MapManager.HasNewBlend)
                        LoadUncompressed(mLayerInfos[i], i);
                    else
                        LoadLayerCompressed(mLayerInfos[i], i);
                }
                else
                {
                    for (var j = 0; j < 4096; ++j)
                        AlphaValues[j] |= 0xFFu << (8 * i);
                }
            }

            //mAlphaDataCompressed = null;
        }

        private unsafe void LoadUncompressed(Mcly layerInfo, int layer)
        {
            var ptr = mAlphaDataCompressed.ToPointer();
            var startPos = layerInfo.OfsMcal;
            for (var i = 0; i < 4096; ++i)
                AlphaValues[i] |= (uint) ((byte*)ptr)[startPos++] << (8 * layer);
        }

        private unsafe void LoadLayerCompressed(Mcly layerInfo, int layer)
        {
            var ptr = mAlphaDataCompressed.ToPointer();
            var startPos = layerInfo.OfsMcal;
            var counter = 0;
            for (var k = 0; k < 64; ++k)
            {
                for (var j = 0; j < 32; ++j)
                {
                    var alpha = ((byte*)ptr)[startPos++];
                    var val1 = alpha & 0xF;
                    var val2 = alpha >> 4;
                    val2 = j == 31 ? val1 : val2;
                    val1 = (byte)((val1 / 15.0f) * 255.0f);
                    val2 = (byte)((val2 / 15.0f) * 255.0f);
                    AlphaValues[counter++] |= (uint)val1 << (8 * layer);
                    AlphaValues[counter++] |= (uint)val2 << (8 * layer);
                }
            }
        }

        private unsafe void LoadLayerRle(Mcly layerInfo, int layer)
        {
            var ptr = mAlphaDataCompressed.ToPointer();
            var counterOut = 0;
            var startPos = layerInfo.OfsMcal;
            while (counterOut < 4096)
            {
                var indicator = ((byte*)ptr)[startPos++];
                if ((indicator & 0x80) != 0)
                {
                    var value = ((byte*)ptr)[startPos++];
                    var repeat = indicator & 0x7F;
                    for (var k = 0; k < repeat && counterOut < 4096; ++k)
                        AlphaValues[counterOut++] |= (uint)value << (layer * 8);
                }
                else
                {
                    for (var k = 0; k < (indicator & 0x7F) && counterOut < 4096; ++k)
                        AlphaValues[counterOut++] |= (uint)((byte*)ptr)[startPos++] << (8 * layer);
                }
            }
        }

        private void LoadMcvt()
        {
            var heights = mReader.ReadArray<float>(145);

            var posx = Metrics.MapMidPoint - mHeader.Position.Y;
            var posy = Metrics.MapMidPoint + mHeader.Position.X;
            var posz = mHeader.Position.Z;

            var counter = 0;

            var minPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var maxPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for(var i = 0; i < 17; ++i)
            {
                for(var j = 0; j < (((i % 2) != 0) ? 8 : 9); ++j)
                {
                    var height = posz + heights[counter];
                    var x = posx + j * Metrics.UnitSize;
                    if ((i % 2) != 0)
                        x += 0.5f * Metrics.UnitSize;
                    var y = posy - i * Metrics.UnitSize * 0.5f;

                    Vertices[counter].Position = new Vector3(x, y, height);

                    if (height < minPos.Z)
                        minPos.Z = height;
                    if (height > maxPos.Z)
                        maxPos.Z = height;

                    if (x < minPos.X)
                        minPos.X = x;
                    if (x > maxPos.X)
                        maxPos.X = x;
                    if (y < minPos.Y)
                        minPos.Y = y;
                    if (y > maxPos.Y)
                        maxPos.Y = y;

                    Vertices[counter].TexCoordAlpha = new Vector2(j / 8.0f + ((i % 2) != 0 ? (0.5f / 8.0f) : 0), i / 16.0f);
                    Vertices[counter].TexCoord = new Vector2(j + ((i % 2) != 0 ? 0.5f : 0.0f), i * 0.5f);
                    ++counter;
                }
            }

            BoundingBox = new BoundingBox(minPos, maxPos);
        }

        private void LoadMcnr()
        {
            var normals = mReader.ReadArray<sbyte>(145 * 3);
            var counter = 0;

            for (var i = 0; i < 17; ++i)
            {
                for (var j = 0; j < (((i % 2) != 0) ? 8 : 9); ++j)
                {
                    var nx = normals[counter * 3] / -127.0f;
                    var ny = normals[counter * 3 + 1] / -127.0f;
                    var nz = normals[counter * 3 + 2] / 127.0f;

                    Vertices[counter].Normal = new Vector3(nx, ny, nz);
                    ++counter;
                }
            }
        }

        private void LoadMccv()
        {
            var colors = mReader.ReadArray<uint>(145);
            for (var i = 0; i < 145; ++i)
                Vertices[i].Color = colors[i];
        }

        private void LoadMcly(int size)
        {
            mLayerInfos.AddRange(mTexReader.ReadArray<Mcly>(size / SizeCache<Mcly>.Size));
        }

        public override void Dispose()
        {
            Textures.Clear();
        }
    }
}