using System.Runtime.InteropServices;
using TerabithiaRemote.Shared.Dtos;

namespace TerabithiaRemote.Server.Input
{
    public static class InputSimulator
    {
        public static void ApplyMouse(MouseInputDto dto)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = dto.X,
                    dy = dto.Y,
                    dwFlags = GetMouseFlag(dto.Action)
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public static void ApplyKeyboard(KeyboardInputDto dto)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)dto.VirtualKeyCode,
                    dwFlags = dto.IsKeyDown ? 0u : KEYEVENTF_KEYUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        private static uint GetMouseFlag(MouseAction action)
        {
            return action switch
            {
                MouseAction.Move => MOUSEEVENTF_MOVE,
                MouseAction.LeftDown => MOUSEEVENTF_LEFTDOWN,
                MouseAction.LeftUp => MOUSEEVENTF_LEFTUP,
                MouseAction.RightDown => MOUSEEVENTF_RIGHTDOWN,
                MouseAction.RightUp => MOUSEEVENTF_RIGHTUP,
                _ => 0
            };
        }

        #region WinAPI

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion
    }
}
