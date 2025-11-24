using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartCaraTest.util
{
    public partial class EnterBox : TextBox
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Return)
                Keyboard.ClearFocus();
        }
    }
}
