namespace TerabithiaRemote.Shared.Dtos
{
    public class MouseInputDto
    {
        public int X { get; set; }
        public int Y { get; set; }

        // Viewer'daki Image'ın gerçek görünen boyutu (pixel)
        public int ViewWidth { get; set; }
        public int ViewHeight { get; set; }

        // Stream edilen frame'in gerçek çözünürlüğü
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }

        public MouseAction Action { get; set; }
    }

    public enum MouseAction
    {
        Move,
        LeftDown,
        LeftUp,
        RightDown,
        RightUp
    }
}
