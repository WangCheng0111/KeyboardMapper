using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;

namespace KeyboardMapper
{
    public partial class MainWindow : Window
    {
        private bool isSpacePressed = false;
        private bool isMappingEnabled = false;
        private bool hasPerformedMapping = false;
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        // 常量定义
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int INPUT_KEYBOARD = 1;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // 虚拟键码
        private const byte VK_SPACE = 0x20;
        private const byte VK_UP = 0x26;
        private const byte VK_LEFT = 0x25;
        private const byte VK_DOWN = 0x28;
        private const byte VK_RIGHT = 0x27;
        private const byte VK_HOME = 0x24;
        private const byte VK_END = 0x23;
        private const byte VK_RMENU = 0xA5;
        private const byte VK_LSHIFT = 0xA0;

        // 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion input;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
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
        private struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public MainWindow()
        {
            InitializeComponent();
            _proc = HookCallback;
            hookID = SetHook(_proc);
            this.Closing += MainWindow_Closing;

            // 默认开启映射
            isMappingEnabled = true;
            // 设置初始状态的提示文本
            notifyIcon.ToolTipText = "键盘映射工具 - 映射已开启";
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookID);
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && isMappingEnabled)
            {
                var keyboardStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                if (keyboardStruct.vkCode == VK_RMENU)
                {
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        SendShiftKey(true);
                        return (IntPtr)1;
                    }
                    else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        SendShiftKey(false);
                        return (IntPtr)1;
                    }
                }

                if (keyboardStruct.vkCode == VK_SPACE)
                {
                    if (wParam == (IntPtr)WM_KEYDOWN && !isSpacePressed)
                    {
                        isSpacePressed = true;
                        hasPerformedMapping = false;
                    }
                    else if (wParam == (IntPtr)WM_KEYUP)
                    {
                        if (!hasPerformedMapping)
                        {
                            INPUT[] inputs = new INPUT[2];
                            inputs[0].type = INPUT_KEYBOARD;
                            inputs[0].input.ki.wVk = VK_SPACE;
                            inputs[0].input.ki.wScan = 0;
                            inputs[0].input.ki.dwFlags = 0;

                            inputs[1].type = INPUT_KEYBOARD;
                            inputs[1].input.ki.wVk = VK_SPACE;
                            inputs[1].input.ki.wScan = 0;
                            inputs[1].input.ki.dwFlags = KEYEVENTF_KEYUP;

                            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                        }
                        isSpacePressed = false;
                        hasPerformedMapping = false;
                    }
                    return (IntPtr)1;
                }
                else if (isSpacePressed && (wParam == (IntPtr)WM_KEYDOWN))
                {
                    uint scanCode;
                    byte vkCode = 0;

                    switch (keyboardStruct.vkCode)
                    {
                        case 0x4A: scanCode = MapVirtualKey(VK_UP, 0); vkCode = VK_UP; break;
                        case 0x4B: scanCode = MapVirtualKey(VK_LEFT, 0); vkCode = VK_LEFT; break;
                        case 0x4C: scanCode = MapVirtualKey(VK_RIGHT, 0); vkCode = VK_RIGHT; break;
                        case 0xBA: scanCode = MapVirtualKey(VK_DOWN, 0); vkCode = VK_DOWN; break;
                        case 0x49: scanCode = MapVirtualKey(VK_HOME, 0); vkCode = VK_HOME; break;
                        case 0x4F: scanCode = MapVirtualKey(VK_END, 0); vkCode = VK_END; break;
                        default: return CallNextHookEx(hookID, nCode, wParam, lParam);
                    }

                    INPUT[] inputs = new INPUT[2];
                    inputs[0].type = INPUT_KEYBOARD;
                    inputs[0].input.ki.wScan = (short)scanCode;
                    inputs[0].input.ki.wVk = vkCode;
                    inputs[0].input.ki.dwFlags = 0;

                    inputs[1].type = INPUT_KEYBOARD;
                    inputs[1].input.ki.wScan = (short)scanCode;
                    inputs[1].input.ki.wVk = vkCode;
                    inputs[1].input.ki.dwFlags = KEYEVENTF_KEYUP;

                    SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                    hasPerformedMapping = true;
                    return (IntPtr)1;
                }

                if (isSpacePressed)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        private void SendShiftKey(bool isDown)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].input.ki.wVk = 0;
            inputs[0].input.ki.wScan = 0x2A;
            inputs[0].input.ki.dwFlags = 0x0008;
            if (!isDown)
            {
                inputs[0].input.ki.dwFlags |= KEYEVENTF_KEYUP;
            }
            inputs[0].input.ki.time = 0;
            inputs[0].input.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void ToggleMapping_Click(object sender, RoutedEventArgs e)
        {
            isMappingEnabled = !isMappingEnabled;
            toggleMenuItem.Header = isMappingEnabled ? "关闭映射" : "开启映射";
            notifyIcon.ToolTipText = "键盘映射工具 - 映射" + (isMappingEnabled ? "已开启" : "已关闭");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}