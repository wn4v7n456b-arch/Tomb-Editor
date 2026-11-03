namespace TombLib.LevelData
{
    public sealed class CausticsFaceRecord
    {
        public int RoomIndex { get; set; }
        public int SectorX { get; set; }
        public int SectorZ { get; set; }
        public string Face { get; set; } = string.Empty;
        public byte Fade { get; set; } = 3;
        public byte Brightness { get; set; } = 3;
        public byte Red { get; set; } = 255;
        public byte Green { get; set; } = 255;
        public byte Blue { get; set; } = 255;
        public bool Inherit { get; set; }
    }
}
