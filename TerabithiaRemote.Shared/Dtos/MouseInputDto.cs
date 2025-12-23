namespace TerabithiaRemote.Shared.Dtos
{
    public class MouseInputDto
    {
        // Viewer tarafında Image üzerindeki koordinat
        public int X { get; set; }
        public int Y { get; set; }

        // Viewer'da görüntünün (Image control) gerçek çizilen boyutu
        public int ViewWidth { get; set; }
        public int ViewHeight { get; set; }

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
