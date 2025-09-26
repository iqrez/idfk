using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WootMouseRemap
{
    public sealed class MacroEngine : IDisposable
    {
        private readonly Xbox360ControllerWrapper _pad;
        private readonly ConcurrentDictionary<Xbox360Button, CancellationTokenSource> _active = new();

        public MacroEngine(Xbox360ControllerWrapper pad) => _pad = pad;

        public void StartRapid(Xbox360Button button, double rateHz, int burst = 0)
        {
            StopRapid(button);
            var cts = new CancellationTokenSource();
            _active[button] = cts;
            Task.Run(async () =>
            {
                try
                {
                    int fired = 0;
                    int intervalMs = (int)Math.Max(5, 1000.0 / rateHz);
                    while (!cts.IsCancellationRequested && (burst <= 0 || fired < burst))
                    {
                        _pad.SetButton(button, true); _pad.Submit();
                        await Task.Delay(Math.Max(1, intervalMs / 2), cts.Token);
                        _pad.SetButton(button, false); _pad.Submit();
                        await Task.Delay(Math.Max(1, intervalMs / 2), cts.Token);
                        fired++;
                    }
                }
                catch { /* canceled */ }
            }, cts.Token);
        }

        public void StopRapid(Xbox360Button button)
        {
            if (_active.TryRemove(button, out var cts))
            { try { cts.Cancel(); cts.Dispose(); } catch { } }
            _pad.SetButton(button, false); _pad.Submit();
        }

        public void Dispose()
        {
            foreach (var kv in _active) { try { kv.Value.Cancel(); kv.Value.Dispose(); } catch { } }
            _active.Clear();
        }
    }
}
