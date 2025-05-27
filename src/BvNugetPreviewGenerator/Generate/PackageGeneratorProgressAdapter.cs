using BvNugetPreviewGenerator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public class PackageGeneratorProgressAdapter : IProgressReporter, IDisposable
    {
        private readonly Action<string> _logAction;
        private readonly Action<int, string> _progressAction;
        private readonly CancellationToken _cancellationToken;
        private bool _disposed;

        public PackageGeneratorProgressAdapter(
            Action<string> logAction,
            Action<int, string> progressAction,
            CancellationToken cancellationToken = default)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _progressAction = progressAction ?? throw new ArgumentNullException(nameof(progressAction));
            _cancellationToken = cancellationToken;
        }

        public void ReportProgress(int percentage, string message)
        {
            ThrowIfDisposedOrCancelled();
            _progressAction(percentage, message);
        }

        public void LogMessage(string message)
        {
            ThrowIfDisposedOrCancelled();
            _logAction(message);
        }

        private void ThrowIfDisposedOrCancelled()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PackageGeneratorProgressAdapter));
            _cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
