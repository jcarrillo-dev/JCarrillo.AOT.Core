using System.Collections.Concurrent;
using System.Diagnostics;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    internal sealed class ValueLINQGC
    {
        private static readonly ConcurrentBag<Action> _cleanupActions = [];
        private static readonly PeriodicTimer _timer;
        private static readonly CancellationTokenSource _cts;
        private static readonly Task _backgroundTask;

        static ValueLINQGC()
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(RunLoopAsync);
        }

        public static void Registrar(Action cleanupAction)
        {
            if (cleanupAction != null)
            {
                _cleanupActions.Add(cleanupAction);
            }
        }

        private static async Task RunLoopAsync()
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    EjecutarLimpieza();
                }
            }
            catch (OperationCanceledException)
            {
                // Apagado controlado
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en el bucle de limpieza de ValueLINQGC: {ex}");
            }
        }

        private static void EjecutarLimpieza()
        {
            foreach (Action action in _cleanupActions)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error ejecutando acción de limpieza en ValueLINQGC: {ex}");
                }
            }
        }

        internal static async Task ShutdownAsync()
        {
            _cts.Cancel();
            _timer.Dispose();
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignorar excepciones durante el apagado
            }
            _cts.Dispose();
        }
    }
}
