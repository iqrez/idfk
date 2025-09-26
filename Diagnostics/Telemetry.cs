namespace WootMouseRemap
{
    public sealed class Telemetry
    {
        public int RawDx { get; set; }
        public int RawDy { get; set; }
        public short StickRX { get; set; }
        public short StickRY { get; set; }
        public bool Suppressing { get; set; }
        public bool ViGEmConnected { get; set; }
        public string ProfileName { get; set; } = "default";
        public void ResetMove() { RawDx = 0; RawDy = 0; }
    }
}
