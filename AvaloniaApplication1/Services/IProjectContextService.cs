using System;
using System.ComponentModel;

namespace AvaloniaApplication1.Services
{
    public interface IProjectContextService : INotifyPropertyChanged
    {
        bool IsDesignMode { get; set; }
    }
}