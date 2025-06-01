using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SystemMonitorApp
{
    public class ObservableObject : INotifyPropertyChanged, IDisposable
    {
        private readonly SynchronizationContext _syncContext;
        private bool _isDisposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableObject()
        {
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler == null) return;

            _syncContext.Post(_ =>
            {
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }, null);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (!SetProperty(ref field, value, propertyName))
                return false;

            onChanged?.Invoke();
            return true;
        }

        protected void OnPropertiesChanged(params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                OnPropertyChanged(name);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                PropertyChanged = null;
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ObservableObject()
        {
            Dispose(false);
        }
    }
}