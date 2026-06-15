using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvaloniaApplication1.Services
{
    public class ProjectContextService : IProjectContextService
    {
        private bool _isDesignMode;

        public bool IsDesignMode
        {
            get => _isDesignMode;
            set
            {
                if (_isDesignMode != value)
                {
                    _isDesignMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}