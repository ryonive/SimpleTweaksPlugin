using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Game.ClientState.Keys;

// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
#pragma warning disable CS0169
#pragma warning disable CS0649

namespace SimpleTweaksPlugin.Utility;

public partial class NativeKeyState : IDisposable {
    
    private const int WH_KEYBOARD_LL = 13;

    private const uint WM_KEYUP = 0x101;
    private const uint WM_KEYDOWN = 0x100;

    [Flags]
    private enum KeyInfoFlags {
        Up = 0x80,
    }

    private struct KeyInfoStruct {
        public int vkCode;
        private int scanCode;
        public KeyInfoFlags flags;
        private int time;
        private int dwExtraInfo;
    }

    private delegate nint HookHandlerDelegate(int nCode, nint wParam, ref KeyInfoStruct lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowsHookExW(int idHook, HookHandlerDelegate lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, ref KeyInfoStruct lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint hWndParent, nint hWndChildAfter, string lpszClass,
        string? lpszWindow);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial int GetWindowThreadProcessId(nint hWnd, out int processId);

    [LibraryImport("user32.dll")]
    private static partial int GetMessageW(out nint lpMsg, nint hWnd, uint wMsgFilterMin = 0, uint wMsgFilterMax = 0);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint TranslateMessage(nint msg);

    [LibraryImport("user32.dll")]
    private static partial nint DispatchMessageW(nint msg);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);
    

    private nint _keyboardHookId;
    private Thread? _thread;

    // Storing the delegate as a class member prevents it from being GC'd and causing crashes
    // See: https://stackoverflow.com/a/65250050
    private HookHandlerDelegate? _delegate;

    private CancellationTokenSource? _cts;

    private bool enabled;
    private bool disposed;
    
    internal NativeKeyState() {
        
    }
    
    internal void Enable() {
        if (disposed) throw new ObjectDisposedException(nameof(NativeKeyState));
        SimpleLog.Debug("Enable NativeKeyState");
        enabled = true;
        this._delegate = this.OnKeystrokeDetour;
        this._cts = new CancellationTokenSource();

        this._thread = new Thread(() => {
            using var currentModule = Process.GetCurrentProcess().MainModule!;

            // SetWindowsHookEx will bind to the currently-used thread.
            // We offload this so that we don't hang the entire system on waiting for framework ticks and all.
            this._keyboardHookId = SetWindowsHookExW(
                WH_KEYBOARD_LL,
                this._delegate,
                GetModuleHandleW(currentModule.ModuleName),
                0);

            while (!this._cts.IsCancellationRequested) {
                // FIXME: This isn't great because we can hang the thread here for a *long* time in theory.
                // In practice, this won't actually happen because our message bus is almost constantly getting data,
                // but this is a slight code smell I can't be bothered to resolve.
                if (GetMessageW(out var msg, 0) != 0) break;

                TranslateMessage(msg);
                DispatchMessageW(msg);
            }
            
        });
        this._thread.Start();
    }

    public void Disable() {
        SimpleLog.Debug("Disable NativeKeyState");
        if (this._keyboardHookId != nint.Zero) {
            UnhookWindowsHookEx(this._keyboardHookId);
            this._keyboardHookId = nint.Zero;
        }
       
        this._cts?.Cancel();
        enabled = false;
    }
    
    public void Dispose() {
        disposed = true;
        Disable(); 
        this._cts?.Dispose();
    }

    public delegate void OnKeystrokeDelegate(VirtualKey key, bool down, ref KeyHandleType handleType);

    private event OnKeystrokeDelegate onKeystroke;
    public event OnKeystrokeDelegate OnKeystroke {
        add {
            if (disposed) throw new ObjectDisposedException(nameof(NativeKeyState));
            onKeystroke -= value;
            onKeystroke += value;
            if (!enabled) Enable();
        }
        remove {
            if (disposed) throw new ObjectDisposedException(nameof(NativeKeyState));
            onKeystroke -= value;
            if ((onKeystroke?.GetInvocationList().Length ?? 0) == 0) {
                Disable();
            }
        }
    }

    public enum KeyHandleType {
        Allow,
        Block,
        BlockAndPassToGame,
    }
    
    private nint OnKeystrokeDetour(int nCode, nint wParam, ref KeyInfoStruct lParam) {
        // DANGER: This method is *highly sensitive* to performance impacts! Keep it light!!
        // When this tweak runs, this method runs on *every keyboard event across the entire system*. As such, if this
        // takes too long, it *will* be noticeable to the user, including if/when they're not in the game. Yes, this
        // does in fact turn FFXIV into a de facto keylogger. We have to do this to capture certain keys.
        if (onKeystroke == null) goto ORIGINAL;
        if (!TryFindGameWindow(out var handle)) goto ORIGINAL;
        if (GetForegroundWindow() != handle) goto ORIGINAL;
        var handleType = KeyHandleType.Allow;
        onKeystroke.Invoke((VirtualKey) lParam.vkCode, (lParam.flags & KeyInfoFlags.Up) == KeyInfoFlags.Up, ref handleType);
        if (handleType == KeyHandleType.Block) {
            return 1;
        }

        if (handleType == KeyHandleType.BlockAndPassToGame) {
            SendMessageW(handle, lParam.flags == KeyInfoFlags.Up ? WM_KEYUP : WM_KEYDOWN, lParam.vkCode, 0);
            return 1;
        }
        
        ORIGINAL:
        return CallNextHookEx(this._keyboardHookId, nCode, wParam, ref lParam);
    }

    public static bool IsKeyDown(VirtualKey vKey) {
        return (GetAsyncKeyState((int) vKey) & 0x8000) > 0;
    }

    private static bool TryFindGameWindow(out nint handle) {
        handle = nint.Zero;
        while (true) {
            handle = FindWindowExW(nint.Zero, handle, "FFXIVGAME", null);
            if (handle == nint.Zero) break;
            var _ = GetWindowThreadProcessId(handle, out var pid);
            if (pid == Environment.ProcessId) break;
        }

        return handle != nint.Zero;
    }
}