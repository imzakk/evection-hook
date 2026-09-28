using Avalonia.Controls;
using Avalonia.Input;
using Evection.GUI.ViewModels;

namespace Evection.GUI.Views;

public sealed partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (DataContext is not ShellViewModel vm)
                return;
            if (e.Key == Key.Escape && vm.Dialog != null)
                vm.CancelDialog();
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Left && vm.GoBackCommand.CanExecute(null))
                vm.GoBackCommand.Execute(null);
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Right && vm.GoForwardCommand.CanExecute(null))
                vm.GoForwardCommand.Execute(null);
        };
        PointerPressed += (_, e) =>
        {
            if (DataContext is not ShellViewModel vm)
                return;
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsXButton1Pressed && vm.GoBackCommand.CanExecute(null))
                vm.GoBackCommand.Execute(null);
            else if (props.IsXButton2Pressed && vm.GoForwardCommand.CanExecute(null))
                vm.GoForwardCommand.Execute(null);
        };
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as ShellViewModel)?.CancelDialog();
}
