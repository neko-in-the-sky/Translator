using System;
using System.Windows.Input;

namespace Translator;

/// <summary>
/// A command that is always enabled and runs an action.
/// </summary>
public class RelayCommand(Action execute) : ICommand
{
    public bool CanExecute(object parameter) => true;

    public void Execute(object parameter) => execute();

    // Never raised: the command is always enabled.
    public event EventHandler CanExecuteChanged
    {
        add { }
        remove { }
    }
}
