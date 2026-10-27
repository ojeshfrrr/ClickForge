using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickForge.Core;

namespace ClickForge;
public partial class MainWindow
{
    private async Task RunCustomKeyDiagnosticsAsync(List<string> results,string output)
    {
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);results.Add("PASS "+message);}
        void Route(int id){bool handled=false;WindowMessage(0,0x0312,id,0,ref handled);}
        bool refusedEmergency=false,captured=false,blocked=false;
        settings = settings with { CustomKey = 0 }; engine.ConfigureOutput(settings.Button, 0); SynchronizeOutputControls();
        _=Dispatcher.BeginInvoke(new Action(()=>
        {
            refusedEmergency=!customKeyWindow!.Capture(settings.StopKey);
            Route(1);blocked=!engine.Enabled;
            captured=customKeyWindow.Capture(69);
            RenderDiagnostic(customKeyWindow,Path.Combine(Path.GetDirectoryName(output)!,"ui-custom-key-capture.png"));
            customKeyWindow.DialogResult=true;
        }));
        InputTypeInput.SelectedIndex = (int)OutputInputType.Keyboard;
        Check(refusedEmergency&&captured&&blocked,"Capture dialog rejects emergency-key conflicts and blocks global activation while choosing");
        Check(settings.Button==ClickButton.CustomKey&&settings.CustomKey==69&&engine.CustomKey==69&&!engine.Enabled&&ChangeCustomKeyButton.Visibility==Visibility.Visible,"Custom input key selection configures shared engine while stopped");
        Check(new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"ui-test-settings.json")).Load().CustomKey==69,"Captured custom key persists through actual UI");
        Route(1);await Task.Delay(100);UpdateStatus();
        Check(engine.Enabled&&engine.Session.InputPairsSent>0&&StatusText.Text=="Sending keys","Custom output uses existing activation worker and accurate status with mocked input");
        Route(2);long count=engine.Clicks;await Task.Delay(50);Check(engine.IsPaused&&engine.Clicks==count,"Custom key pause stops new input pairs");
        Route(2);await Task.Delay(70);Route(3);Check(!engine.Enabled&&engine.Clicks>count,"Custom key resume and emergency stop use shared controls");
        var profile=profiles.Create(settings,"Custom key diagnostic");InputTypeInput.SelectedIndex=0;ApplyProfile(profile);
        Check(settings.CustomKey==69&&engine.CustomKey==69&&InputTypeInput.SelectedIndex==1&&ClickButtonInput.Visibility==Visibility.Collapsed&&!engine.Enabled,"Applying custom keyboard profile synchronizes output controls without activation");
        RenderDiagnostic(this,Path.Combine(Path.GetDirectoryName(output)!,"ui-custom-key-dashboard.png"));
        profiles.Delete(profile.Id);settings=settings with{ActiveProfileId=null};InputTypeInput.SelectedIndex=0;
        settings = settings with { CustomKey = 0 }; engine.ConfigureOutput(settings.Button, 0); SynchronizeOutputControls();
        _=Dispatcher.BeginInvoke(new Action(()=>customKeyWindow!.DialogResult=false));InputTypeInput.SelectedIndex=1;
        Check(settings.Button==ClickButton.Left&&InputTypeInput.SelectedIndex==0&&ClickButtonInput.SelectedIndex==0&&!engine.Enabled,"Cancelling custom capture restores the previous output and leaves input stopped");
        using(var scope=new CustomKeyTestScope(this))
        {
            var test=scope.Window;
            var pad = test.TestPad;
            foreach(bool down in new[]{true,false})pad.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(test),Environment.TickCount,Key.E){RoutedEvent=down?Keyboard.PreviewKeyDownEvent:Keyboard.PreviewKeyUpEvent});
            Check(test.ReceivedDowns==1&&test.ReceivedUps==1&&!test.FixedTestEnabled&&!engine.Enabled,"Isolated keyboard pad receives routed down/up events without starting native or production input");
            RenderDiagnostic(test,Path.Combine(Path.GetDirectoryName(output)!,"ui-custom-key-safe-pad.png"));
        }
        ActivateInput.SelectedIndex=11;PauseInput.SelectedIndex=12;ApplyClicked(this,new());
        ApplyProfile(new TimingProfile{Button=ClickButton.CustomKey,CustomKey=0x75});
        ActivateInput.SelectedIndex=5;ApplyClicked(this,new());
        Check(settings.ActivateKey==5&&MessageText.Text.Contains("cannot match")&&!engine.Enabled,"Applying shortcuts cannot create a custom-output activation conflict");
        ResetClicked(this,new());
        Check(settings.ActivateKey==5&&settings.PauseKey==6&&settings.CustomKey==0x75&&MessageText.Text.Contains("cannot match"),"Reset shortcut defaults refuses a conflicting custom F6 key without mutating settings");
        settings=settings with{ActiveProfileId=null};InputTypeInput.SelectedIndex=0;ResetClicked(this,new());
    }
    private sealed class CustomKeyTestScope:IDisposable
    {
        internal TestWindow Window{get;}
        internal CustomKeyTestScope(Window owner){Window=new(SafeTestMode.FixedCps,ClickButton.CustomKey,69){Owner=owner,ShowActivated=false};Window.Show();}
        public void Dispose()=>Window.Close();
    }
}
