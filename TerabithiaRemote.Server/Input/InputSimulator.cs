using System;
using System.Runtime.InteropServices;
using TerabithiaRemote.Shared.Dtos;

namespace TerabithiaRemote.Server.Input
{
    public static class InputSimulator
    {
        public static void ApplyMouse(MouseInputDto dto)
        {
            // Güvenlik: bölme hatası olmasın
            if (dto.ViewWidth <= 0 || dto.ViewHeight <= 0)
                return;

            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // Viewer coords -> Screen pixel coords
            int targetX = (int)Math.Round(dto.X * (screenW / (double)dto.ViewWidth));
            int targetY = (int)Math.Round(dto.Y * (screenH / (double)dto.ViewHeight));

            // Clamp
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetX > screenW - 1) targetX = screenW - 1;
            if (targetY > screenH - 1) targetY = screenH - 1;

            // Absolute coords (0..65535)
            int absX = (int)Math.Round(targetX * 65535.0 / (screenW - 1));
            int absY = (int)Math.Round(targetY * 65535.0 / (screenH - 1));

            uint flags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;

            // Tıklama ise move + click flag aynı pakette
            flags |= dto.Action switch
            {
                MouseAction.LeftDown => MOUSEEVENTF_LEFTDOWN,
                MouseAction.LeftUp => MOUSEEVENTF_LEFTUP,
                MouseAction.RightDown => MOUSEEVENTF_RIGHTDOWN,
                MouseAction.RightUp => MOUSEEVENTF_RIGHTUP,
                _ => 0
            };

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = flags
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
                    dwFlags = dto.IsKeyDown ? 0u : KEYEVENTF_KEYUP
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
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
