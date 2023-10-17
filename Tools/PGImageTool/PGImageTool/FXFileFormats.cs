using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;


namespace PGImageTool
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FXIHeader
    {
        public FXIHeader(ushort w, ushort h) { Magic = 0x465849; Width = w; Height = h; Flags = 0; Flags2 = 0; }

        public uint Magic;
        public ushort Width;
        public ushort Height;
        public uint Flags;
        public uint Flags2;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FXVHeader
    {
        public FXVHeader(ushort w, ushort h) { Magic = 0x465856; Width = w; Height = h; Flags = 0; Flags2 = 0; }

        public uint Magic;
        public ushort Width;
        public ushort Height;
        public uint Flags;
        public uint Flags2;
    };


}
