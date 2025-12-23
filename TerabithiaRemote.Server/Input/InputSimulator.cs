using System;
using System.Runtime.InteropServices;
using TerabithiaRemote.Shared.Dtos;

namespace TerabithiaRemote.Server.Input
{
    public static class InputSimulator
    {
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        public static void ApplyMouse(MouseInputDto dto)
        {

            // Ekran çözünürlüğü
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // Güvenlik
            if (dto.ScreenWidth <= 0 || dto.ScreenHeight <= 0) return;

            // Viewer koordinatını (dto.X,Y) -> gerçek ekran koordinatına map et
            double nx = (dto.X / (double)dto.ScreenWidth) * screenW;
            double ny = (dto.Y / (double)dto.ScreenHeight) * screenH;

            // SendInput ABSOLUTE 0..65535 ister
            int absX = (int)(nx * 65535 / (screenW - 1));
            int absY = (int)(ny * 65535 / (screenH - 1));

            uint flags = GetMouseFlag(dto.Action);

            // Move ise absolute zorunlu
            // ApplyMouse içinde flags kısmına ekle:
            if (dto.Action == MouseAction.Move)
                flags |= (MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK);

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = absX,
                    dy = absY,
                    mouseData = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void ApplyKeyboard(KeyboardInputDto dto)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)dto.VirtualKeyCode,
                    wScan = 0,
                    dwFlags = dto.IsKeyDown ? 0u : KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
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
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

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
