using Microsoft.AspNetCore.SignalR;
using Pythagoras.ClockSignal.WebApi.Hubs;
using Pythagoras.Infrastructure.Configuration;
using Pythagoras.Infrastructure.CubeClients.ClockSignal;
using Pythagoras.Infrastructure.Realtime;

namespace Pythagoras.ClockSignal.WebApi.Services
{
    public sealed class ClockSignalService : IDisposable
    {
        private readonly ILogger<ClockSignalService> _logger;
        private readonly ILogger<Quartz> _quartzLogger;
        private readonly CustomConfigurationStore<ClockSignalSettings> _configurationStore;
        private readonly IHubContext<ClockSignalHub, IClockSignalHub> _hubContext;

        private readonly object _syncObj = new object();
        private Task? _quartzTask;
        private CancellationTokenSource? _tokenSource;
        private string? _errorMessage;

        //public event EventHandler<QuartzEventArgs>? ErrorOccurred;
        //public event EventHandler? Starting;
        //public event EventHandler? Completed;
        //public event EventHandler<QuartzEventArgs>? VirtualTimeChanged;
        //public event EventHandler<QuartzEventArgs>? ClockTimeChanged;

        public bool IsRunning => _quartzTask != null;
        public bool IsError => _errorMessage != null;
        public string? ErrorMessage => _errorMessage;
 
        public ClockSignalService(ILogger<ClockSignalService> logger, ILogger<Quartz> quartzLogger,
            CustomConfigurationStore<ClockSignalSettings> configurationStore,
            IHubContext<ClockSignalHub, IClockSignalHub> hubContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quartzLogger = quartzLogger ?? throw new ArgumentNullException(nameof(quartzLogger));
            _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            lock (_syncObj)
            {
                //var settings = await _configurationStore.GetAsync();
                //var quartzSettings = new QuartzSettings
                if (IsRunning)
                {
                    _logger.LogError("QuartzTask already started");
                    throw new InvalidOperationException("QuartzTask already started");
                }
                _tokenSource = new CancellationTokenSource();
                _quartzTask = WorkerProcAsync(_tokenSource.Token);
            }
        }

        public void Stop()
        {
            lock (_syncObj)
            {
                _tokenSource?.Cancel();
                if (!_quartzTask?.Wait(3000) ?? false)
                {
                    _logger.LogError("Couldnot stop QuartzTask. Timeout=3000ms");
                    throw new InvalidOperationException("Couldnot stop QuartzTask. Timeout=3000ms");
                }
                _tokenSource?.Dispose();
                _tokenSource = null;
                _quartzTask = null;
            }
        }

        private async Task WorkerProcAsync(CancellationToken cancellationToken)
        {
            var settings = await _configurationStore.GetAsync();
            for (var dt = settings.StartDate.Date; dt <= settings.EndDate.Date; dt = dt.AddDays(1))
            {
                if (cancellationToken.IsCancellationRequested) break;

                using var finishedEvent = new AutoResetEvent(false);

                var quartzSettings = GetQuartzSettings(settings, dt);
                var quartz = new Quartz(quartzSettings, _quartzLogger);
                quartz.Starting += (o, e) =>
                {
                    _hubContext.Clients.All.StateChanged("Starting");
                    //Starting?.Invoke(this, e);
                };
                quartz.Stopped += (o, e) =>
                {
                    _hubContext.Clients.All.StateChanged("Stopped");
                    //Completed?.Invoke(this, e);
                    finishedEvent.Set();
                };
                quartz.ErrorOccurred += (o, e) =>
                {
                    _errorMessage = e.Message ?? "Unknown error";
                    _hubContext.Clients.All.StateChanged("Error");
                    //rrorOccurred?.Invoke(this, new QuartzEventArgs { Message = _errorMessage });
                    finishedEvent.Set();
                };
                quartz.VirtualTimeChanged += (o, e) =>
                {
                    //VirtualTimeChanged?.Invoke(this, e);
                    _hubContext.Clients.All.NewVirtualTime(e.Time);
                    if (settings.Mode == ClockSignalSettings.ClockSignalMode.Realtime)
                    {
                        _hubContext.Clients.All.NewClockTime(e.Time);//.SendAsync("NewClockTime", e.Time?.ToString("dd.MM.yy HH:mm:ss.fff"));
                        //ClockTimeChanged?.Invoke(this, e);
                    }
                };
                quartz.ClockTimeChanged += (o, e) =>
                {
                    if (settings.Mode == ClockSignalSettings.ClockSignalMode.Backtesting)
                    {
                        _hubContext.Clients.All.NewClockTime(e.Time);//.SendAsync("NewClockTime", e.Time?.ToString("dd.MM.yy HH:mm:ss.fff"));
                        //ClockTimeChanged?.Invoke(this, e);
                    }
                };

                quartz.Start(cancellationToken);
                await Task.Run(() => finishedEvent.WaitOne());
            }
        }

        private static QuartzSettings GetQuartzSettings(ClockSignalSettings settings, DateTime date)
        {
            if (date < settings.StartDate || date > settings.EndDate)
                throw new ArgumentOutOfRangeException(nameof(date));

            return new QuartzSettings
            {
                Date = date.Date,
                StartTime = settings.StartTime,
                EndTime = settings.EndTime,
                TimeFactor = settings.Mode == ClockSignalSettings.ClockSignalMode.Realtime ? 1.0 : settings.TimeFactor
            };
        }
    }
}
