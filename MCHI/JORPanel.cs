using System;
using System.Drawing;
using System.Windows.Forms;

namespace MCHI
{
    class JORPanel : Panel
    {
        public JORServer Server;
        public JORNode Node;
        private bool SuppressEvent = false;

        const float RANGE_FLOAT_STEPS = 100.0f;

        public JORPanel(JORServer server, JORNode node)
        {
            this.AutoScroll = true;
            this.Server = server;
            this.Node = node;
            BuildPanel(Node);
        }

        private void SyncLabelFromJOR(Label label, JORControlLabel jorLabel)
        {
            label.Text = jorLabel.Name;
        }

        private void SyncButtonFromJOR(Button button, JORControlButton jorButton)
        {
        }

        private void SyncCheckBoxFromJOR(CheckBox checkBox, JORControlCheckBox jorCheckBox)
        {
            this.SuppressEvent = true;
            checkBox.Checked = jorCheckBox.Value;
            this.SuppressEvent = false;
        }

        private void SyncRangeIntFromJOR(TrackBar trackBar, JORControlRangeInt jorRange)
        {
            this.SuppressEvent = true;
            trackBar.Minimum = jorRange.RangeMin;
            trackBar.Maximum = jorRange.RangeMax;
            trackBar.TickStyle = (trackBar.Maximum - trackBar.Minimum > 20) ? TickStyle.None : TickStyle.BottomRight;
            trackBar.Value = Math.Clamp(jorRange.Value, jorRange.RangeMin, jorRange.RangeMax);
            this.SuppressEvent = false;
        }

        private void SyncRangeFloatFromJOR(TrackBar trackBar, JORControlRangeFloat jorRange)
        {
            this.SuppressEvent = true;
            trackBar.Minimum = (int)(jorRange.RangeMin * RANGE_FLOAT_STEPS);
            trackBar.Maximum = (int)(jorRange.RangeMax * RANGE_FLOAT_STEPS);
            trackBar.TickStyle = (trackBar.Maximum - trackBar.Minimum > 20) ? TickStyle.None : TickStyle.BottomRight;
            trackBar.Value = (int)(Math.Clamp(jorRange.Value, jorRange.RangeMin, jorRange.RangeMax) * RANGE_FLOAT_STEPS);
            this.SuppressEvent = false;
        }

        private void SyncComboBoxFromJOR(ComboBox comboBox, JORControlSelector jorSelector)
        {
            this.SuppressEvent = true;
            if (jorSelector.SelectedIndex < jorSelector.Items.Count)
                comboBox.SelectedIndex = (int)jorSelector.SelectedIndex;
            this.SuppressEvent = false;
        }

        private void SyncRadioButtonFromJOR(GroupBox groupBox, JORControlSelector jorSelector)
        {
            this.SuppressEvent = true;
            for (int i = 0; i < groupBox.Controls.Count; i++)
            {
                RadioButton radioButton = groupBox.Controls[i] as RadioButton;
                radioButton.Checked = i == jorSelector.SelectedIndex;
            }
            this.SuppressEvent = false;
        }

        private void SyncEditBoxFromJOR(TextBox textBox, JORControlEditBox jorEditBox)
        {
            this.SuppressEvent = true;
            textBox.Text = jorEditBox.Text;
            textBox.MaxLength = (int) jorEditBox.MaxChars;
            this.SuppressEvent = false;
        }

        private void OnButtonClick(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var button = obj as Button;
            var jorControl = button.Tag as JORControlButton;
            jorControl.Click(this.Server);
        }

        private void OnCheckboxChecked(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var checkbox = obj as CheckBox;
            var jorControl = checkbox.Tag as JORControlCheckBox;
            jorControl.SetValue(this.Server, checkbox.Checked);
        }

        private void OnSliderChangedInt(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var trackbar = obj as TrackBar;
            var jorControl = trackbar.Tag as JORControlRangeInt;
            jorControl.SetValue(this.Server, trackbar.Value);
        }

        private void OnSliderChangedFloat(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var trackbar = obj as TrackBar;
            var jorControl = trackbar.Tag as JORControlRangeFloat;
            jorControl.SetValue(this.Server, trackbar.Value / RANGE_FLOAT_STEPS);
        }

        private void OnComboBoxSelectedIndexChanged(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var combobox = obj as ComboBox;
            var jorControl = combobox.Tag as JORControlSelector;
            var jorControlItem = jorControl.Items[combobox.SelectedIndex];
            jorControl.SetSelectedIndex(this.Server, jorControlItem.Value);
        }

        private void OnRadioButtonCheckedChanged(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var radiobutton = obj as RadioButton;
            if (!radiobutton.Checked)
                return;
            var gb = radiobutton.Parent as GroupBox;
            var jorControl = gb.Tag as JORControlSelector;
            var jorControlItem = radiobutton.Tag as JORControlSelectorItem;
            jorControl.SetSelectedIndex(this.Server, jorControlItem.Value);
        }

        private void OnTextBoxTextChanged(Object obj, EventArgs args)
        {
            if (this.SuppressEvent)
                return;

            var textbox = obj as TextBox;
            var jorControl = textbox.Tag as JORControlEditBox;
            jorControl.SetValue(this.Server, textbox.Text);
        }

        private Control MakeControl(JORControl jorControl)
        {
            if (jorControl.Type == "LABL")
            {
                var jorLabel = jorControl as JORControlLabel;
                Label label = new Label();
                SyncLabelFromJOR(label, jorLabel);
                return label;
            }
            else if (jorControl.Type == "BUTN")
            {
                var jorButton = jorControl as JORControlButton;
                Button button = new Button();
                button.Text = jorControl.Name;
                button.Tag = jorControl;
                SyncButtonFromJOR(button, jorButton);
                jorButton.Updated += () =>
                {
                    SyncButtonFromJOR(button, jorButton);
                };

                button.Click += OnButtonClick;
                return button;
            }
            else if (jorControl.Type == "CHBX")
            {
                var jorCheckBox = jorControl as JORControlCheckBox;
                CheckBox checkBox = new CheckBox();
                checkBox.Tag = jorCheckBox;
                checkBox.Text = jorCheckBox.Name;
                SyncCheckBoxFromJOR(checkBox, jorCheckBox);
                jorCheckBox.Updated += () =>
                {
                    SyncCheckBoxFromJOR(checkBox, jorCheckBox);
                };

                checkBox.CheckedChanged += OnCheckboxChecked;
                return checkBox;
            }
            else if (jorControl.Type == "RNGi")
            {
                var jorRange = jorControl as JORControlRangeInt;
                var table = new TableLayoutPanel();
                table.CellBorderStyle = TableLayoutPanelCellBorderStyle.OutsetDouble;
                table.RowCount = 1;
                table.ColumnCount = 2;
                var label = new Label();
                label.Width = jorControl.Location.Width / 2;
                label.Text = jorRange.Name;
                table.Controls.Add(label);
                TrackBar trackBar = new TrackBar();
                trackBar.Width = jorControl.Location.Width / 2;
                trackBar.Tag = jorRange;
                SyncRangeIntFromJOR(trackBar, jorRange);
                jorRange.Updated += () =>
                {
                    SyncRangeIntFromJOR(trackBar, jorRange);
                };

                table.Controls.Add(trackBar);
                trackBar.ValueChanged += OnSliderChangedInt;
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
                TrackBar trackBar = new TrackBar();
                trackBar.Tag = jorRange;
                SyncRangeFloatFromJOR(trackBar, jorRange);
                jorRange.Updated += () =>
                {
                    SyncRangeFloatFromJOR(trackBar, jorRange);
                };

                trackBar.ValueChanged += OnSliderChangedFloat;
                table.Controls.Add(trackBar);
                return table;
            }
            else if (jorControl.Type == "CMBX")
            {
                var jorSelector = jorControl as JORControlSelector;
                var comboBox = new ComboBox();
                comboBox.Tag = jorSelector;
                comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                comboBox.Text = jorSelector.Name;
                foreach (var item in jorSelector.Items)
                    comboBox.Items.Add(item.Name);

                SyncComboBoxFromJOR(comboBox, jorSelector);
                jorSelector.Updated += () =>
                {
                    SyncComboBoxFromJOR(comboBox, jorSelector);
                };

                comboBox.SelectedIndexChanged += OnComboBoxSelectedIndexChanged;
                return comboBox;
            }
            else if (jorControl.Type == "RBTN")
            {
                var jorSelector = jorControl as JORControlSelector;
                var groupBox = new GroupBox();
                groupBox.Tag = jorSelector;
                groupBox.Text = jorSelector.Name;
                foreach (var item in jorSelector.Items)
                {
                    var radioButton = new RadioButton();
                    radioButton.CheckedChanged += OnRadioButtonCheckedChanged;
                    radioButton.Text = item.Name;
                    radioButton.Tag = item;
                    groupBox.Controls.Add(radioButton);
                }

                SyncRadioButtonFromJOR(groupBox, jorSelector);
                jorSelector.Updated = () =>
                {
                    SyncRadioButtonFromJOR(groupBox, jorSelector);
                };

                return groupBox;
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
                var textBox = new TextBox();
                textBox.Tag = jorEditBox;

                SyncEditBoxFromJOR(textBox, jorEditBox);
                jorEditBox.Updated = () =>
                {
                    SyncEditBoxFromJOR(textBox, jorEditBox);
                };

                textBox.TextChanged += OnTextBoxTextChanged;
                return table;
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
