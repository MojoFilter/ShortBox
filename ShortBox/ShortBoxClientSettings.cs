namespace ShortBox;

public interface IShortBoxClientSettings
{
    IObservable<Uri> BaseAddress { get; }
    string Host { get; set; }
    int Port { get; set; }
}

internal class ShortBoxClientSettings : IShortBoxClientSettings
{
    public string Host
    {
        get => _hostSubject.Value;
        set => _hostSubject.OnNext(value);
    }

    public int Port 
    { 
        get => _portSubject.Value;
        set => _portSubject.OnNext(value);
    }

    public IObservable<Uri> BaseAddress => 
        _hostSubject
        .CombineLatest(_portSubject, (host, port) => $"http://{host}:{port}")
        .Select(uri => new Uri(uri));

    private BehaviorSubject<string> _hostSubject = new BehaviorSubject<string>("192.168.86.31");
    private BehaviorSubject<int> _portSubject = new BehaviorSubject<int>(5000);
}
