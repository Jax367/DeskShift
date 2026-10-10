using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskShift {
    // This pool is accessed only by the application's UI thread.
    internal static class TransitionWindowPool {
        sealed class Entry {public TransitionWindow Window;public Rectangle Bounds;public bool Busy;}
        static readonly List<Entry> entries=new List<Entry>();
        internal static List<TransitionWindow> Acquire(Rectangle[] displays,Palette palette,int style,int waitingStyle) {
            foreach(var old in entries.Where(e=>!e.Busy && !displays.Contains(e.Bounds)).ToArray()){old.Window.Dispose();entries.Remove(old);}
            var result=new List<TransitionWindow>();
            try{foreach(var bounds in displays){var entry=entries.FirstOrDefault(e=>!e.Busy && !e.Window.IsDisposed && e.Bounds==bounds);if(entry==null){entry=new Entry{Bounds=bounds,Window=new TransitionWindow(bounds,palette,style,waitingStyle)};entries.Add(entry);}entry.Busy=true;entry.Window.Configure(palette,style,waitingStyle);result.Add(entry.Window);}return result;}catch{Release(result);throw;}
        }
        internal static void Release(IEnumerable<TransitionWindow> windows){foreach(var window in windows){window.Hide();window.ClearSession();var entry=entries.FirstOrDefault(e=>e.Window==window);if(entry!=null)entry.Busy=false;}}
        internal static async Task WarmUpAsync(Palette palette) {
            var windows=Acquire(Array.ConvertAll(Screen.AllScreens,s=>s.Bounds),palette,0,0);
            try{await Task.Run(()=>{foreach(var window in windows)window.PrepareArtwork();});foreach(var window in windows)window.WarmNativeSurface();}finally{Release(windows);}
        }
        internal static void DisposeIdle(){foreach(var entry in entries.Where(e=>!e.Busy).ToArray()){entry.Window.Dispose();entries.Remove(entry);}}
    }
}
