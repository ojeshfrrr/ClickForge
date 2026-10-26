using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;
public partial class MainWindow
{
    // Explicit diagnostic mode only: isolated settings, mock input, no screenshots,
    // no Rendering subscription that would itself keep the WPF frame pump active.
    private async Task RunPerformanceTestAsync(string output)
    {
        var lines=new List<string>();int code=0,layouts=0,textChanges=0;
        EventHandler onLayout=(_,_)=>layouts++;
        EventHandler onText=(_,_)=>textChanges++;
        var descriptor=DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));
        var texts=new List<TextBlock>();
        void Visit(DependencyObject item){if(item is TextBlock t)texts.Add(t);for(int n=0;n<VisualTreeHelper.GetChildrenCount(item);n++)Visit(VisualTreeHelper.GetChild(item,n));}
        try
        {
            settings=settings with{Mode=ActivationMode.Toggle,Cps=10,TimingMode=ClickTimingMode.Fixed,Button=ClickButton.Left,CustomKey=0};LoadControls();
            Visit(this);foreach(var t in texts)descriptor.AddValueChanged(t,onText);LayoutUpdated+=onLayout;
            lines.Add($"ENV version={InstalledVersion.Value}; DPI={VisualTreeHelper.GetDpi(this).DpiScaleX}; renderTier={RenderCapability.Tier>>16}; processors={Environment.ProcessorCount}; size={ActualWidth}x{ActualHeight}");
            await Task.Delay(2500);
            using var process=Process.GetCurrentProcess();
            async Task Measure(string name)
            {
                process.Refresh();var cpu=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(false),pairs=engine.Clicks;int l=layouts,t=textChanges;var clock=Stopwatch.StartNew();
                await Task.Delay(4000);process.Refresh();
                lines.Add($"MEASURE {name}: cpuOneCore={(process.TotalProcessorTime-cpu).TotalMilliseconds/clock.Elapsed.TotalMilliseconds*100:0.00}%; workingMiB={process.WorkingSet64/1048576d:0.0}; privateMiB={process.PrivateMemorySize64/1048576d:0.0}; allocatedKiB={(GC.GetTotalAllocatedBytes(false)-allocated)/1024d:0.0}; layouts={layouts-l}; textChanges={textChanges-t}; pairs={engine.Clicks-pairs}; seconds={clock.Elapsed.TotalSeconds:0.000}");
            }
            await Measure("idle-visible");refresh.Stop();await Task.Delay(300);await Measure("idle-refresh-off");
            refresh.Start();engine.Start();await Task.Delay(500);await Measure("mock-10cps-visible");
            refresh.Stop();await Task.Delay(300);await Measure("mock-10cps-refresh-off");engine.Stop();
            engine.Cps=50;refresh.Start();engine.Start();await Task.Delay(500);await Measure("mock-50cps-visible");engine.Stop();
            Hide();refresh.Interval=TimeSpan.FromSeconds(1);await Task.Delay(300);await Measure("idle-hidden");
            lines.Add("RESULT Performance probe completed; input mocked; no target input or screenshots generated.");
        }
        catch(Exception e){lines.Add("FAIL "+e);code=1;}
        finally
        {
            LayoutUpdated-=onLayout;foreach(var t in texts)descriptor.RemoveValueChanged(t,onText);
            engine.Stop();File.WriteAllLines(output,lines);PrepareExit();Application.Current.Shutdown(code);
        }
    }
}
