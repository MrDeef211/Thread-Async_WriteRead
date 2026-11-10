namespace AsyncWriteRead
{
    internal class WaitEventSlim
    {
        private event EventHandler? SetEvent;
        private event EventHandler? QueueChanged;

        private object _lock = new();

        private volatile bool _locked;

        private int _autoGoneCount;

        private readonly LinkedList<TaskCompletionSource<bool>> _oneWaitersQueue = new();
        private readonly LinkedList<TaskCompletionSource<bool>> _autoWaitersQueue = new();

        public bool Unlocked
        {
            get
            {
                lock (_lock) 
                    return !_locked;
            }
        }

        public int WaitingCount
        {
            get
            {
                lock (_lock)
                {
                    int eventListeners = SetEvent?.GetInvocationList().Length ?? 0;
                    return eventListeners + _oneWaitersQueue.Count + _autoWaitersQueue.Count;
                }
            }
        }



        public WaitEventSlim(bool unlocked)
        {
            _locked = !unlocked;
        }

        /// <summary>
        /// Ждёт сигнал каждый раз
        /// Пропускает только один поток по одному
        /// </summary>
        /// <remarks>
        /// Не удаляется из памяти после "неудачного" вызова Set() ждёт непосредственно своей очереди.
        /// Разблокирует только если Set() будет вызван после блокировки
        /// </remarks>
        /// <param name="token"></param>
        /// <returns>True - ручной пропуск</returns>
        public Task<bool> WaitOneAsync(CancellationToken token = default)
        {
            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                if (token.IsCancellationRequested)
                    return Task.FromResult(false);

                if (!_locked && _oneWaitersQueue.Count == 0)
                {
                    _locked = true;
                    return Task.FromResult(true);
                }

                var node = _oneWaitersQueue.AddLast(tcs);

                if (token.CanBeCanceled)
                {
                    var registration = token.Register(() =>
                    {
                        lock (_lock)
                        {
                            if (node.List != null)
                            {
                                _oneWaitersQueue.Remove(node);
                            }
                            tcs.TrySetResult(false);
                        }
                    });

                    _ = tcs.Task.ContinueWith(
                        _ => registration.Dispose(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }

            return tcs.Task;
        }


        /// <summary>
        /// Ждёт сигнал каждый раз
        /// </summary>
        /// <remarks>
        /// Разблокирует только если Set() будет вызван после блокировки
        /// </remarks>
        /// <param name="token"></param>
        /// <returns>True - первый выходящий</returns>
        public Task<bool> WaitAutoAsync(CancellationToken token = default)
        {
            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                if (token.IsCancellationRequested)
                    return Task.FromResult(false);

                var node = _autoWaitersQueue.AddLast(tcs);
                QueueChanged?.Invoke(this, EventArgs.Empty);

                if (token.CanBeCanceled)
                {
                    var registration = token.Register(() =>
                    {
                        lock (_lock)
                        {
                            if (node.List != null)
                                _autoWaitersQueue.Remove(node);

                            tcs.TrySetResult(false);
                        }
                    });

                    _ = tcs.Task.ContinueWith(
                        _ => registration.Dispose(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }

            return tcs.Task;
        }

        /// <summary>
        /// Будит всех текущих WaitAutoAsync-ожидающих (Set) и ждёт,
        /// пока каждый из них снова не встанет в очередь ожидания
        /// (или не выйдут по отмене через NotifyWaiterLeft).
        /// </summary>
        public async Task SetAndWaitAllAsync(CancellationToken token = default)
        {
            int snapshot;
            int goneAtSnapshot;

            lock (_lock)
            {
                snapshot = _autoWaitersQueue.Count;
                goneAtSnapshot = _autoGoneCount;
            }

            if (snapshot == 0)
            {
                return;
            }

            Set();

            while (true)
            {
                token.ThrowIfCancellationRequested();

                TaskCompletionSource<bool> tcs;
                EventHandler? handler = null;

                lock (_lock)
                {
                    if (_autoWaitersQueue.Count + (_autoGoneCount - goneAtSnapshot) >= snapshot)   
                        return;

                    tcs = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    handler = (_, _) =>
                    {
                        lock (_lock)
                        {
                            QueueChanged -= handler;
                        }
                        tcs.TrySetResult(true);
                    };
                    QueueChanged += handler;
                }

                using var reg = token.Register(() => tcs.TrySetResult(false));                     
                await tcs.Task.ConfigureAwait(false);
            }
        }

        public void NotifyWaiterLeft()
        {
            lock (_lock)
            {
                _autoGoneCount++;
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Ждёт разблокировки
        /// Если блокировка уже разблокирована пропускает сразу
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<bool> WaitManualAsync(CancellationToken token = default)
        {
            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler? e = null;
            CancellationTokenRegistration registration = default;

            e = (_, _) =>
            {
                lock (_lock)
                {
                    SetEvent -= e;
                }
                registration.Dispose();
                tcs.TrySetResult(true);
            };

            lock (_lock)
            {

                if (token.IsCancellationRequested)
                {
                    return Task.FromResult(false);
                }

                if (!_locked)
                {
                    return Task.FromResult(true);
                }

                registration = token.Register(() =>
                {
                    lock (_lock)
                    {
                        SetEvent -= e;
                    }
                    tcs.TrySetResult(false);
                });

                SetEvent += e;
            }


            return tcs.Task;
        }

        public void Reset()
        {
            lock (_lock)
                _locked = true;
        }

        public IDisposable UseReset()
        {
            lock (_lock)
                _locked = true;
            return new EventReleaseWrapper(this);
        }

        public bool Set()
        {
            lock (_lock)
            {
                _locked = false;
                bool anyReleased = false;

                while (_oneWaitersQueue.Count > 0)
                {
                    var waiterTcs = _oneWaitersQueue.First!.Value;
                    _oneWaitersQueue.RemoveFirst();

                    if (waiterTcs.TrySetResult(true))
                    {
                        _locked = true;
                        anyReleased = true;
                        break;
                    }
                }

                if (_autoWaitersQueue.Count > 0)
                {
                    var first = _autoWaitersQueue.First!.Value;

                    foreach (var tcs in _autoWaitersQueue)
                    {
                        tcs.TrySetResult(tcs == first);
                    }

                    _autoWaitersQueue.Clear();
                    QueueChanged?.Invoke(this, EventArgs.Empty);
                    anyReleased = true;
                }

                if (SetEvent != null)
                {
                    SetEvent.Invoke(this, EventArgs.Empty);
                    anyReleased = true;
                }

                return anyReleased;
            }
        }

        private class EventReleaseWrapper : IDisposable
        {

            public EventReleaseWrapper(WaitEventSlim waitEvent)

            {
                _e = waitEvent;
            }

            public void Dispose()
            {
                _e?.Set();
            }

            private WaitEventSlim? _e;
        }
    }
}
