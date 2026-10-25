using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;
internal sealed class CustomKeyWindow : Window
{
    private readonly Func<int,string?> validate;
    private readonly TextBlock selected = new() { FontSize=28, FontWeight=FontWeights.SemiBold, Margin=new(0,16,0,12), Text="Press a key…" };
    private readonly TextBlock feedback = new() { TextWrapping=TextWrapping.Wrap, Margin=new(0,8,0,14) };
    private readonly Button use = new() { Content="Use key", IsEnabled=false, MinWidth=100 };
    private bool capturing=true;
    internal int ChosenKey { get; private set; }
    internal CustomKeyWindow(Func<int,string?> validate)
    {
        this.validate=validate; DialogLayout.Setup(this,"Custom input key",470,350,false);MinHeight=320;Height=350;
        var panel=new StackPanel{Margin=new(22)};
        panel.Children.Add(new TextBlock{Text="Custom input key",FontSize=23,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text="Press one keyboard key, then choose Use key. It will repeat at the configured CPS. Clicking stays stopped while choosing.",TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,0),Foreground=(Brush)FindResource("Muted")});
        selected.SetResourceReference(TextBlock.ForegroundProperty,"Accent"); feedback.SetResourceReference(TextBlock.ForegroundProperty,"Warning");
        panel.Children.Add(selected);panel.Children.Add(feedback);
        var actions=new WrapPanel(); var record=new Button{Content="Record again",Margin=new(0,0,8,0)};var cancel=new Button{Content="Cancel",Margin=new(8,0,0,0)};
        use.SetResourceReference(StyleProperty,"PrimaryButton");use.Content=new TextBlock{Text="Use key",Foreground=(Brush)FindResource("Ink"),FontWeight=FontWeights.SemiBold};actions.Children.Add(record);actions.Children.Add(use);actions.Children.Add(cancel);panel.Children.Add(actions);
        Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        record.Click+=(_,_)=>{capturing=true;ChosenKey=0;use.IsEnabled=false;selected.Text="Press a key…";feedback.Text="";Focus();};
        use.Click+=(_,_)=>{if(ChosenKey!=0){DialogResult=true;}};cancel.Click+=(_,_)=>{DialogResult=false;};
        PreviewKeyDown+=(_,e)=>{if(!capturing)return;e.Handled=true; Capture(KeyInterop.VirtualKeyFromKey(e.Key==Key.System?e.SystemKey:e.Key),Keyboard.Modifiers!=ModifierKeys.None);};
    }
    internal bool Capture(int key,bool modifiers=false)
    {
        string? error=modifiers?"Choose a single key without Ctrl, Alt, Shift or Windows modifiers.":!KeyboardOutput.IsValid(key)?"That key is unsupported. Choose a letter, number, Space, function or navigation key.":validate(key);
        if(error is not null){feedback.Text=error;return false;}
        ChosenKey=key;selected.Text=KeyboardOutput.Label(key);feedback.Text="Press Record again to change it.";use.IsEnabled=true;capturing=false;use.Focus();return true;
    }
}
