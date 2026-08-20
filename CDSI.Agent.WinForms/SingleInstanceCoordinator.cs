namespace CDSI.Agent.WinForms;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private int _disposed;

    public SingleInstanceCoordinator(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        var instanceName = $"Local\\{applicationId}";
        _mutex = new Mutex(
            initiallyOwned: true,
            $"{instanceName}.Mutex",
            out var createdNew);
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            $"{instanceName}.Activate");
        IsPrimaryInstance = createdNew;
    }

    public bool IsPrimaryInstance { get; }

    public void SignalPrimaryInstance()
    {
        ThrowIfDisposed();
        _activationEvent.Set();
    }

    public void StartListening(Action activationHandler)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);
        ThrowIfDisposed();
        if (!IsPrimaryInstance)
        {
            throw new InvalidOperationException(
                "只有主实例可以监听窗口激活请求。");
        }

        var registration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut && Volatile.Read(ref _disposed) == 0)
                {
                    activationHandler();
                }
            },
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        if (Interlocked.CompareExchange(
                ref _activationRegistration,
                registration,
                comparand: null) is not null)
        {
            registration.Unregister(waitObject: null);
            throw new InvalidOperationException("激活请求监听已启动。");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _activationRegistration, null)?
            .Unregister(waitObject: null);
        _activationEvent.Dispose();
        if (IsPrimaryInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
