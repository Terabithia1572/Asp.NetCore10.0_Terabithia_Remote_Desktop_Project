namespace TerabithiaRemote.Shared.Dtos
{
    public class MouseInputDto
    {
        public int X { get; set; }
        public int Y { get; set; }
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
