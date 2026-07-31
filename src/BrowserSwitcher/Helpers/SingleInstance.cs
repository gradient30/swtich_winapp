using System.Threading;

namespace BrowserSwitcher.Helpers;

/// <summary>
/// 通过命名 Mutex 保证单实例，并用 EventWaitHandle 通知主实例显示窗口。
/// </summary>
public sealed class SingleInstance : IDisposable
{
    /// <summary>全局互斥体名称。</summary>
    public const string MutexName = @"Global\BrowserSwitcher_SingleInstance";

    /// <summary>用于请求显示主窗口的事件名称。</summary>
    public const string ShowEventName = @"Global\BrowserSwitcher_ShowWindow";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly bool _isPrimary;
    private CancellationTokenSource? _waitCts;
    private Thread? _waitThread;
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent, bool isPrimary)
    {
        _mutex = mutex;
        _showEvent = showEvent;
        _isPrimary = isPrimary;
    }

    /// <summary>当前进程是否为本应用的主实例。</summary>
    public bool IsPrimary => _isPrimary;

    /// <summary>
    /// 尝试成为主实例；若已有实例则返回次要实例句柄。
    /// </summary>
    /// <returns>单实例控制器。</returns>
    /// <exception cref="InvalidOperationException">无法创建同步对象时抛出。</exception>
    public static SingleInstance Create()
    {
        try
        {
            var mutex = new Mutex(true, MutexName, out bool createdNew);
            var showEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ShowEventName);
            return new SingleInstance(mutex, showEvent, createdNew);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "无法创建单实例同步对象，可能权限不足。",
                ex);
        }
        catch (WaitHandleCannotBeOpenedException ex)
        {
            throw new InvalidOperationException(
                "无法打开单实例同步对象。",
                ex);
        }
    }

    /// <summary>
    /// 通知已运行的主实例显示主窗口。
    /// </summary>
    public void SignalShowWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _showEvent.Set();
    }

    /// <summary>
    /// 在后台线程等待“显示窗口”信号，并在收到时回调。
    /// 仅主实例应调用。
    /// </summary>
    /// <param name="onShowRequested">收到信号后的回调（通常切回 UI 线程）。</param>
    /// <exception cref="InvalidOperationException">非主实例调用时抛出。</exception>
    public void BeginWaitForShowSignal(Action onShowRequested)
    {
        ArgumentNullException.ThrowIfNull(onShowRequested);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isPrimary)
        {
            throw new InvalidOperationException(
                "仅主实例可等待显示窗口信号。");
        }

        if (_waitThread != null)
        {
            return;
        }

        _waitCts = new CancellationTokenSource();
        var token = _waitCts.Token;

        _waitThread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 短超时以便及时响应取消
                    if (_showEvent.WaitOne(500))
                    {
                        if (!token.IsCancellationRequested)
                        {
                            onShowRequested();
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "BrowserSwitcher.ShowWindowWaiter"
        };

        _waitThread.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _waitCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 忽略重复释放
        }

        try
        {
            // 唤醒等待线程以便退出
            _showEvent.Set();
        }
        catch (ObjectDisposedException)
        {
            // 忽略
        }

        // 不在可能持有 UI 调度器的线程上同步 Join，
        // 以免与回调中的 Dispatcher 操作互相等待。
        // 等待线程为 IsBackground，进程退出时会自动结束。
        _waitThread = null;

        if (_isPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 未持有所有权时忽略
            }
        }

        _waitCts?.Dispose();
        _showEvent.Dispose();
        _mutex.Dispose();
    }
}
