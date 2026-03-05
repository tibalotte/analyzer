using System.Windows;
using System.Windows.Controls;
using FFBAnalyzer.Models;
using FFBAnalyzer.ViewModels;

namespace FFBAnalyzer.Views;

public partial class SessionView : UserControl
{
    public SessionView()
    {
        InitializeComponent();
    }

    private void OnRunIndividualTest(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (DataContext is not SessionViewModel vm) return;

        var testDef = (string)btn.Tag switch
        {
            "StepResponse"      => TestDefinition.StepResponse(),
            "SineSweep"         => TestDefinition.SineSweep(),
            "Chirp"             => TestDefinition.Chirp(),
            "SquareWave"        => TestDefinition.SquareWave(),
            "Impulse"           => TestDefinition.Impulse(),
            "ConstantTorque"    => TestDefinition.ConstantTorque(),
            "FrictionEmulation" => TestDefinition.FrictionEmulation(),
            _                   => TestDefinition.StepResponse()
        };

        vm.StartNewRunCommand.Execute(testDef);
    }
}
