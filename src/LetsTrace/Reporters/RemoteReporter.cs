using System;
using System.Threading;
using LetsTrace.Exceptions;
using LetsTrace.Metrics;
using LetsTrace.Transport;

namespace LetsTrace.Reporters
{
    // TODO: use this to load up spans into a processing queue that will be taken care of by a thread
    public class RemoteReporter : IReporter
    {
        // TODO: Constants
        public static readonly TimeSpan REMOTE_REPORTER_DEFAULT_FLUSH_INTERVAL_MS = TimeSpan.FromMilliseconds(100);
        public const int REMOTE_REPORTER_DEFAULT_MAX_QUEUE_SIZE = 100;

        private readonly ITransport _transport;
        private readonly IMetrics _metrics;

        public RemoteReporter(ITransport transport, IMetrics metrics)
        {
            _transport = transport;
            _metrics = metrics;
        }

        public async void Dispose()
        {
            try
            {
                int n = await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                _metrics.ReporterSuccess.Inc(n);
            }
            catch (SenderException e)
            {
                _metrics.ReporterFailure.Inc(e.DroppedSpans);
            }
        }

        // TODO: Make async!
        public async void Report(ILetsTraceSpan span)
        {
            try
            {
                // TODO: This Task should be queued and be processed in a separate thread
                await _transport.AppendAsync(span, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                _metrics.ReporterDropped.Inc(1);
            }
        }

        // TODO: Make async!
        private async void Flush()
        {
            try
            {
                // TODO: Not exposed, this should be the list of unprocessed Report calls
                //_metrics.ReporterQueueLength.Update(_commandQueue.Count);
                int n = await _transport.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                _metrics.ReporterSuccess.Inc(n);
            }
            catch (SenderException e)
            {
                _metrics.ReporterFailure.Inc(e.DroppedSpans);
            }
        }

        public class Builder
        {
            private readonly ITransport transport;
            //private TimeSpan flushInterval = REMOTE_REPORTER_DEFAULT_FLUSH_INTERVAL_MS;
            //private int maxQueueSize = REMOTE_REPORTER_DEFAULT_MAX_QUEUE_SIZE;
            private IMetrics metrics;

            public Builder(ITransport transport)
            {
                this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            }

            //public Builder WithFlushInterval(TimeSpan flushInterval)
            //{
            //    this.flushInterval = flushInterval;
            //    return this;
            //}

            //public Builder WithMaxQueueSize(int maxQueueSize)
            //{
            //    this.maxQueueSize = maxQueueSize;
            //    return this;
            //}

            public Builder WithMetrics(IMetrics metrics)
            {
                this.metrics = metrics;
                return this;
            }

            public RemoteReporter Build()
            {
                if (metrics == null)
                {
                    metrics = NoopMetricsFactory.Instance.CreateMetrics();
                }
                return new RemoteReporter(transport/*, flushInterval, maxQueueSize*/, metrics);
            }
        }
    }
}