using DrugSearcher.Models;
using DrugSearcher.ViewModels;
using System.Windows;

namespace DrugSearcher.Views.Controls;

public partial class DosageCalculatorControl
{
    public DosageCalculatorControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty DrugInfoProperty =
        DependencyProperty.Register(nameof(DrugInfo), typeof(BaseDrugInfo), typeof(DosageCalculatorControl),
            new PropertyMetadata(null, OnDrugInfoChanged));

    public BaseDrugInfo? DrugInfo
    {
        get => (BaseDrugInfo?)GetValue(DrugInfoProperty);
        set => SetValue(DrugInfoProperty, value);
    }

    private static void OnDrugInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DosageCalculatorControl { DataContext: DosageCalculatorViewModel viewModel }) return;
        if (e.NewValue is BaseDrugInfo drugInfo)
        {
            _ = viewModel.LoadCalculatorsForDrugAsync(drugInfo);
        }
        else
        {
            viewModel.Reset();
        }
    }
}