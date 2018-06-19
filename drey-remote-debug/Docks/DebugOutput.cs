using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace drey_remote_debug.Docks
{
    public class DebugOutputContent : WeifenLuo.WinFormsUI.Docking.DockContent
    {

        private TextBox _text;

        public DebugOutputContent()
        {
            Text = "Debug Output";
            _text = new TextBox() { Multiline = true, ScrollBars = ScrollBars.Both };
            _text.TextChanged += _text_TextChanged;
            _text.Dock = DockStyle.Fill;
            this.Controls.Add(_text);
        }

        private void _text_TextChanged(object sender, EventArgs e)
        {
            _text.SelectionStart = _text.Text.Length;
            _text.ScrollToCaret();
        }

        public void AppendText(string text)
        {
            _text.Text += text;
        }

        
    }
}
