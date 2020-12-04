using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace MCHI
{
    class JORPanel : Panel
    {
        public JORServer Server;
        public JORNode Node;

        public JORPanel(JORServer server, JORNode node)
        {
            this.AutoScroll = true;
            this.Server = server;
            this.Node = node;
            BuildPanel(Node);
        }

        private void OnButtonClick(Object obj, EventArgs args)
        {
            var button = obj as Button;
            var jorControl = button.Tag as JORControlButton;
            jorControl.Click(this.Server);
        }

        private void OnCheckboxChecked(Object obj, EventArgs args)
        {
            var checkbox = obj as CheckBox;
            var jorControl = checkbox.Tag as JORControlCheckBox;
            jorControl.SetValue(this.Server, checkbox.Checked);
        }

        private void OnSliderChangedInt(Object obj, EventArgs args)
        {
            var trackbar = obj as TrackBar;
            var jorControl = trackbar.Tag as JORControlRangeInt;
            jorControl.SetValue(this.Server, trackbar.Value);
        }

        const float RANGE_FLOAT_STEPS = 100.0f;

        private void OnSliderChangedFloat(Object obj, EventArgs args)
        {
            var trackbar = obj as TrackBar;
            var jorControl = trackbar.Tag as JORControlRangeFloat;
            jorControl.SetValue(this.Server, trackbar.Value / RANGE_FLOAT_STEPS);
        }

        private void OnTextBoxTextChanged(Object obj, EventArgs args)
        {
            var textbox = obj as TextBox;
            var jorControl = textbox.Tag as JORControlEditBox;
            jorControl.SetValue(this.Server, textbox.Text);
        }

        private void OnComboBoxSelectedIndexChanged(Object obj, EventArgs args)
        {
            var combobox = obj as ComboBox;
            var jorControl = combobox.Tag as JORControlSelector;
            var jorControlItem = jorControl.Items[combobox.SelectedIndex];
            jorControl.SetValue(this.Server, jorControlItem.Value);
        }

        private void OnRadioButtonSelectedIndexChanged(Object obj, EventArgs args)
        {
            var radiobutton = obj as RadioButton;
            if (!radiobutton.Checked)
                return;
            var gb = radiobutton.Parent as GroupBox;
            var jorControl = gb.Tag as JORControlSelector;
            var jorControlItem = radiobutton.Tag as JORControlSelectorItem;
            jorControl.SetValue(this.Server, jorControlItem.Value);
        }

        private Control MakeControl(JORControl jorControl)
        {
            if (jorControl.Type == "LABL")
            {
                Label label = new Label();
                label.Text = jorControl.Name;
                return label;
            }
            else if (jorControl.Type == "BUTN")
            {
                Button button = new Button();
                button.Text = jorControl.Name;
                button.Tag = jorControl;
                button.Click += OnButtonClick;
                return button;
            }
            else if (jorControl.Type == "CHBX")
            {
                var jorCheckBox = jorControl as JORControlCheckBox;
                CheckBox c = new CheckBox();
                c.Tag = jorCheckBox;
                c.Text = jorCheckBox.Name;
                c.Checked = jorCheckBox.Value;
                c.CheckedChanged += OnCheckboxChecked;
                return c;
            }
            else if (jorControl.Type == "EDBX")
            {
                var jorEditBox = jorControl as JORControlEditBox;
                var table = new TableLayoutPanel();
                table.RowCount = 1;
                table.ColumnCount = 2;
                var label = new Label();
                label.Text = jorEditBox.Name;
                table.Controls.Add(label);
                var textbox = new TextBox();
                textbox.Tag = jorEditBox;
                textbox.Text = jorEditBox.Text;
                textbox.MaxLength = (int)jorEditBox.MaxChars;
                textbox.TextChanged += OnTextBoxTextChanged;
                return table;
            }
            else if (jorControl.Type == "RNGi")
            {
                var jorRange = jorControl as JORControlRangeInt;
                var table = new TableLayoutPanel();
                table.RowCount = 1;
                table.ColumnCount = 2;
                var label = new Label();
                label.Text = jorRange.Name;
                table.Controls.Add(label);
                TrackBar t = new TrackBar();
                t.Tag = jorRange;
                // drawing tickmarks seems to be incredibly slow https://bugs.freepascal.org/view.php?id=36046
                t.TickStyle = TickStyle.None;
                t.Minimum = jorRange.RangeMin;
                t.Maximum = jorRange.RangeMax;
                if (jorRange.Value >= jorRange.RangeMin && jorRange.Value <= jorRange.RangeMax)
                    t.Value = (int)jorRange.Value;
                t.ValueChanged += OnSliderChangedInt;
                table.Controls.Add(t);
                return table;
            }
            else if (jorControl.Type == "RNGf")
            {
                var jorRange = jorControl as JORControlRangeFloat;
                var table = new TableLayoutPanel();
                table.RowCount = 1;
                table.ColumnCount = 2;
                var label = new Label();
                label.Text = jorRange.Name;
                table.Controls.Add(label);
                TrackBar t = new TrackBar();
                t.Tag = jorRange;
                // drawing tickmarks seems to be incredibly slow https://bugs.freepascal.org/view.php?id=36046
                t.TickStyle = TickStyle.None;
                t.Minimum = (int)(jorRange.RangeMin * RANGE_FLOAT_STEPS);
                t.Maximum = (int)(jorRange.RangeMax * RANGE_FLOAT_STEPS);
                if (jorRange.Value >= jorRange.RangeMin && jorRange.Value <= jorRange.RangeMax)
                    t.Value = (int)(jorRange.Value * RANGE_FLOAT_STEPS);
                t.ValueChanged += OnSliderChangedFloat;
                table.Controls.Add(t);
                return table;
            }
            else if (jorControl.Type == "CMBX")
            {
                var jorSelector = jorControl as JORControlSelector;
                var c = new ComboBox();
                c.Tag = jorSelector;
                c.DropDownStyle = ComboBoxStyle.DropDownList;
                c.Text = jorSelector.Name;
                foreach (var item in jorSelector.Items)
                    c.Items.Add(item.Name);
                if (jorSelector.Value < jorSelector.Items.Count)
                    c.SelectedIndex = (int)jorSelector.Value;
                c.SelectedIndexChanged += OnComboBoxSelectedIndexChanged;
                return c;
            }
            else if (jorControl.Type == "RBTN")
            {
                var jorSelector = jorControl as JORControlSelector;
                var g = new GroupBox();
                g.Tag = jorSelector;
                g.Text = jorSelector.Name;
                foreach (var item in jorSelector.Items)
                {
                    var c = new RadioButton();
                    c.Text = item.Name;
                    c.Tag = item;
                    g.Controls.Add(c);
                }
                g.Controls[(int)jorSelector.Value].Enabled = true;
                return g;
            }
            else if (jorControl.Type == "GRBX")
            {
                var g = new GroupBox();
                g.Text = jorControl.Name;
                return g;
            }
            else
            {
                throw new Exception("Unimplemented control!");
            }
        }

        private void LayoutControl(Control control, ref int x, ref int y, JORControlLocation location)
        {
            if (location.Y < 0)
            {
                control.Top = y;
                y += location.Height;
            }
            else
            {
                control.Top = location.Y;
            }

            if (location.X < 0)
            {
                control.Left = 0;
            }
            else
            {
                control.Left = location.X;
            }

            control.Width = location.Width;
            control.Height = location.Height;
        }

        private void BuildPanel(JORNode node)
        {
            int x = 0, y = 0;
            foreach (var jorControl in node.Controls)
            {
                Control control = MakeControl(jorControl);
                LayoutControl(control, ref x, ref y, jorControl.Location);
                Controls.Add(control);
            }
        }
    }
}
