using System.Collections.Concurrent;

namespace sZIP.Watcher
{
    public sealed class RecursiveArchiveWatcher : IDisposable
    {
        private readonly ArchiveWatchOptions _options;
        private readonly BlockingCollection<string> _candidates = new();
        private readonly ConcurrentDictionary<string, byte> _pending =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _activeExclusions =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FileStamp> _processed =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource _cancellation = new();
        private FileSystemWatcher? _watcher;
        private Task? _worker;
        private Task? _reconciliationWorker;
        private DateTime _startedAtUtc;
        private bool _disposed;

        public RecursiveArchiveWatcher(ArchiveWatchOptions options)
        {
            _options = options;
        }

        public event EventHandler<string>? ArchiveReady;

        public void Start()
        {
            ThrowIfDisposed();
            if (_watcher is not null)
            {
                return;
            }

            if (!Directory.Exists(_options.RootPath))
            {
                throw new DirectoryNotFoundException($"Watch folder not found: {_options.RootPath}");
            }

            _watcher = new FileSystemWatcher(_options.RootPath)
            {
                IncludeSubdirectories = _options.IncludeSubdirectories,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = false
            };
            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;
            _startedAtUtc = DateTime.UtcNow;
            _worker = Task.Run(() => ProcessCandidatesAsync(_cancellation.Token));
            _reconciliationWorker = Task.Run(() => ReconcilePeriodicallyAsync(_cancellation.Token));
            _watcher.EnableRaisingEvents = true;
        }

        public IDisposable ExcludePath(string rootPath)
        {
            ThrowIfDisposed();
            var normalized = NormalizeRoot(rootPath);
            _activeExclusions.TryAdd(normalized, 0);
            return new PathExclusion(_activeExclusions, normalized);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
            _cancellation.Cancel();
            _candidates.CompleteAdding();

            if (_worker is not null)
            {
                WaitForCancellation(_worker);
            }

            if (_reconciliationWorker is not null)
            {
                WaitForCancellation(_reconciliationWorker);
            }

            _candidates.Dispose();
            _cancellation.Dispose();
        }

        private void OnFileChanged(object sender, FileSystemEventArgs eventArgs) =>
            QueueCandidate(eventArgs.FullPath);

        private void OnFileRenamed(object sender, RenamedEventArgs eventArgs) =>
            QueueCandidate(eventArgs.FullPath);

        private void OnWatcherError(object sender, ErrorEventArgs eventArgs) =>
            _ = Task.Run(() => ReconcileOnce(_cancellation.Token));

        private void QueueCandidate(string path)
        {
            if (!_options.Supports(path)
                || IsExcluded(path)
                || IsAlreadyProcessed(path)
                || !_pending.TryAdd(path, 0))
            {
                return;
            }

            try
            {
                _candidates.Add(path, _cancellation.Token);
            }
            catch (InvalidOperationException)
            {
                _pending.TryRemove(path, out _);
            }
            catch (OperationCanceledException)
            {
                _pending.TryRemove(path, out _);
            }
        }

        private async Task ProcessCandidatesAsync(CancellationToken cancellationToken)
        {
            foreach (var path in _candidates.GetConsumingEnumerable(cancellationToken))
            {
                try
                {
                    if (!IsExcluded(path)
                        && await ArchiveStabilityProbe.WaitUntilReadyAsync(path, _options, cancellationToken))
                    {
                        RememberProcessed(path);
                        ArchiveReady?.Invoke(this, path);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                finally
                {
                    _pending.TryRemove(path, out _);
                }
            }
        }

        private async Task ReconcilePeriodicallyAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.ReconcileInterval, cancellationToken);
                ReconcileOnce(cancellationToken);
            }
        }

        private void ReconcileOnce(CancellationToken cancellationToken)
        {
            try
            {
                var searchOption = _options.IncludeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                foreach (var path in Directory.EnumerateFiles(_options.RootPath, "*.*", searchOption))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) >= _startedAtUtc.Subtract(_options.RequiredStableTime))
                        {
                            QueueCandidate(path);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        private bool IsExcluded(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return _activeExclusions.Keys.Any(root =>
                fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAlreadyProcessed(string path)
        {
            if (!_processed.TryGetValue(path, out var processedStamp)
                || !TryGetFileStamp(path, out var currentStamp))
            {
                return false;
            }

            return processedStamp.Equals(currentStamp);
        }

        private void RememberProcessed(string path)
        {
            if (TryGetFileStamp(path, out var stamp))
            {
                _processed[path] = stamp;
            }
        }

        private static bool TryGetFileStamp(string path, out FileStamp stamp)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    stamp = default;
                    return false;
                }

                stamp = new FileStamp(file.Length, file.LastWriteTimeUtc);
                return true;
            }
            catch (IOException)
            {
                stamp = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                stamp = default;
                return false;
            }
        }

        private static string NormalizeRoot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return EndsInDirectorySeparator(fullPath)
                ? fullPath
                : fullPath + Path.DirectorySeparatorChar;
        }

        private static bool EndsInDirectorySeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal);

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RecursiveArchiveWatcher));
            }
        }

        private static void WaitForCancellation(Task task)
        {
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private readonly struct FileStamp : IEquatable<FileStamp>
        {
            public FileStamp(long length, DateTime lastWriteTimeUtc)
            {
                Length = length;
                LastWriteTimeUtc = lastWriteTimeUtc;
            }

            private long Length { get; }
            private DateTime LastWriteTimeUtc { get; }

            public bool Equals(FileStamp other) =>
                Length == other.Length && LastWriteTimeUtc == other.LastWriteTimeUtc;

            public override bool Equals(object? obj) => obj is FileStamp other && Equals(other);

            public override int GetHashCode() => (Length, LastWriteTimeUtc).GetHashCode();
        }

        private sealed class PathExclusion : IDisposable
        {
            private readonly ConcurrentDictionary<string, byte> _exclusions;
            private readonly string _path;

            public PathExclusion(ConcurrentDictionary<string, byte> exclusions, string path)
            {
                _exclusions = exclusions;
                _path = path;
            }

            public void Dispose() => _exclusions.TryRemove(_path, out _);
        }
    }
}
