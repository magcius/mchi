using System;
using System.Text;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using ImGuiNET;

namespace MCHI.Gui
{
    static class GuiController
    {

        private static Sdl2Window _window;
        private static GraphicsDevice _gd;
        private static CommandList _cl;

        private static ImGuiController _controller;
        private static Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
        public static JORNode CurrentEditNode;

        public static StringDictionary translationDictionary = new StringDictionary("../../../tp_dict.json");
        public static bool UseTranslation = true;

        public static void Init()
        {
            VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, 1024, 768, WindowState.Normal, "MCHI"),
            new GraphicsDeviceOptions(true, null, true),
            out _window,
            out _gd);
            _window.Resized += () =>
            {
                _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
                _controller.WindowResized(_window.Width, _window.Height);
            };
            _cl = _gd.ResourceFactory.CreateCommandList();
            _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        }

        public static void Update(JORManager manager)
        {
            if (!_window.Exists) { Environment.Exit(0); return; }
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) { return; }
            _controller.Update(1f / 60f, snapshot);
            SubmitUI(manager);
            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        private static void DrawStyledTextInstance(string text, uint color)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Text(text);
            ImGui.PopStyleColor();
        }

        private static string GetText(String text, JORControl cnt)
        {
            if (UseTranslation)
                return translationDictionary.Translate(text, cnt);
            else
                return text;
        }

        private static void DrawTree(JORServer server, JORNode node)
        {
            if (node == null)
                return;
            if (node.Name == null)
                return;
            if (node.Status == JORNodeStatus.Invalid)
                return;
            if (node.Status == JORNodeStatus.GenRequestSent)
            {
                if (node.LastRequestTime == null || (DateTime.Now - node.LastRequestTime).Seconds > 2)
                    server.SendGenObjectInfo(node);
                return;
            }

            var nodeLabel = GetText(node.Name, null);
            if (node.Children != null && node.Children.Count != 0)
            {
                if (ImGui.TreeNode(nodeLabel + "##" + node.NodePtr))
                {
                    if (node.Controls.Count > 0 && ImGui.Selectable("$ - " + nodeLabel, CurrentEditNode == node))
                        CurrentEditNode = node;
                    foreach (JORNode jorNode in node.Children)
                        DrawTree(server, jorNode);

                    ImGui.TreePop();
                }
            }
            else if (ImGui.Selectable(nodeLabel + "##" + node.NodePtr, CurrentEditNode == node))
                CurrentEditNode = node;
        }

        public static void DrawControlContainer(JORServer server, JORControl control)
        {
            if (control.Node.Status == JORNodeStatus.Invalid)
                return;

            var scale = 2.0f;
            if (control.Location.X != -1 && control.Location.Y != -1)
            {
                ImGui.SetCursorPosX(control.Location.X * scale);
                ImGui.SetCursorPosY(control.Location.Y * scale);
            }
            ImGui.SetNextItemWidth(control.Location.Width);

            switch (control.Type)
            {
                case "LABL": // Label
                    {
                        var jorLabel = control as JORControlLabel;
                        ImGui.Text(GetText(jorLabel.Name, jorLabel));
                        break;
                    }
                case "BUTN": // Button
                    {
                        var jorButton = control as JORControlButton;
                        if (ImGui.Button(GetText(jorButton.Name, jorButton) + "##" + control.ID))
                            jorButton.Click(server);
                        break;
                    }
                case "CHBX": // Checkbox
                    {
                        var jorCheckbox = control as JORControlCheckBox;
                        var val = jorCheckbox.Value;
                        ImGui.Checkbox(GetText(jorCheckbox.Name, jorCheckbox) + "##" + control.ID, ref val);
                        jorCheckbox.SetValue(server, val);
                        break;
                    }
                case "RNGi": // Integer Range
                    {
                        var jorRange = control as JORControlRangeInt;
                        var val = jorRange.Value;
                        ImGui.SliderInt(GetText(jorRange.Name, jorRange) + "##" + control.ID, ref val, jorRange.RangeMin, jorRange.RangeMax);
                        jorRange.SetValue(server, val);
                        break;
                    }

                case "RNGf": // Float Range
                    {
                        var jorRange = control as JORControlRangeFloat;
                        var val = jorRange.Value;
                        ImGui.SliderFloat(GetText(jorRange.Name, jorRange) + "##" + control.ID, ref val, jorRange.RangeMin, jorRange.RangeMax);
                        jorRange.SetValue(server, val);
                        break;
                    }
                case "CMBX": // ComboBox
                    {
                        var jorSelector = control as JORControlSelector;

                        var names = new string[jorSelector.Items.Count];
                        for (int i = 0; i < jorSelector.Items.Count; i++)
                            names[i] = jorSelector.Items[i].Name;

                        var val = (int)jorSelector.SelectedIndex;
                        ImGui.Combo(GetText(jorSelector.Name, jorSelector) + "##" + control.ID, ref val, names, names.Length);
                        jorSelector.SetSelectedIndex(server, (uint)val);

                        break;
                    }
                case "RBTN": // Radio Button
                    {
                        var jorSelector = control as JORControlSelector;
                        var names = new string[jorSelector.Items.Count];
                        for (int i = 0; i < jorSelector.Items.Count; i++)
                            names[i] = jorSelector.Items[i].Name;

                        var val = (int)jorSelector.SelectedIndex;
                        ImGui.Combo(GetText(jorSelector.Name, jorSelector) + "##" + control.ID, ref val, names, names.Length);
                        jorSelector.SetSelectedIndex(server, (uint)val);

                        break;
                    }
                case "EDBX": // EditBox
                    {
                        /// TODO!!!
                        /// If someone types in the textbox, it might overflow the buffer. I don't know yet. I haven't found a text box to type in.
                        /// Figure out a way to introduce a typable text buffer. 

                        var jorEdit = control as JORControlEditBox;
                        var val = jorEdit.Text;
                        var buff = Encoding.UTF8.GetBytes(val);
                        ImGui.InputText(GetText(jorEdit.Name, jorEdit) + "##" + control.ID, buff, (uint)buff.Length);
                        var newBuff = Encoding.UTF8.GetString(buff);
                        jorEdit.SetValue(server, newBuff);
                        break;
                    }
                case "GRBX": // GroupBox
                    {
                        // Group boxes can have a text label, but they're never used in Twilight Princess.
                        ImGui.Separator();
                        break;
                    }
                default:
                    DrawStyledTextInstance($"Unimplemented control '{control.Type}'", 0xFF0000FF);
                    break;
            }
        }

        private static void EnsureTranslation(JORControlSelectorItem jorSelectorItem)
        {
            translationDictionary.EnsureKey(jorSelectorItem.Name);
        }

        private static void EnsureTranslation(JORControl jorControl)
        {
            translationDictionary.EnsureKey(jorControl.Name);

            if (jorControl is JORControlSelector)
            {
                var jorSelector = jorControl as JORControlSelector;
                foreach (var jorSelectorItem in jorSelector.Items)
                    EnsureTranslation(jorSelectorItem);
            }
        }

        private static void EnsureTranslation(JORNode jorNode)
        {
            if (jorNode.Name != null)
                translationDictionary.EnsureKey(jorNode.Name);
            foreach (var jorControl in jorNode.Controls)
                EnsureTranslation(jorControl);
            foreach (var childJorNode in jorNode.Controls)
                EnsureTranslation(childJorNode);
        }

        static int largestBufferUntil0 = 1;

        public static void SubmitUI(JORManager manager)
        {
            ImGui.Begin("MAIN_WINDOW", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar);

            var windowX = 0;
            ImGui.SetWindowPos(new Vector2(windowX, 0), ImGuiCond.Once);
            var leftPaneWidth = 350;
            ImGui.SetWindowSize(new Vector2(leftPaneWidth, _window.Height));
            windowX += leftPaneWidth + 1;

            var statusTextColor = 0xFF0000FF;
            var statusText = "Not Connected";

            if (manager.currentClient != null && manager.currentClient.IsConnected())
            {
                var datsize = manager.jhiClient.GetUnprocessedDataSize() + 1;
                statusTextColor = 0xFF00FF00;
                statusText = $"Connected, buffer size {datsize - 1:X8}";
                if (datsize == 1)
                    largestBufferUntil0 = 1;
                else if (datsize > largestBufferUntil0)
                    largestBufferUntil0 = datsize;
                ImGui.ProgressBar((largestBufferUntil0 - (float)datsize) / (float)largestBufferUntil0);
            }

            DrawStyledTextInstance(statusText, statusTextColor);

            var jorServer = manager.jorServer;
            if (jorServer != null)
            {
                // Sort of a hack to do this here, but go through all nodes and make sure that their controls
                // have translation entries.
                EnsureTranslation(jorServer.Root.TreeRoot);

                if (ImGui.Button("Request JOR Root"))
                    manager.jorServer.SendGetRootObjectRef();

                ImGui.Checkbox("Use Translation", ref UseTranslation);

                ImGui.BeginChild("JorTreeContainer");
                DrawTree(jorServer, jorServer.Root.TreeRoot);
                ImGui.EndChild();
                ImGui.End();

                ImGui.Begin("CONTROL_WINDOW", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar); // Init window
                ImGui.SetWindowPos(new Vector2(windowX, 0), ImGuiCond.Once);
                ImGui.SetWindowSize(new Vector2(_window.Width - windowX, _window.Height));

                if (CurrentEditNode != null && CurrentEditNode.Status == JORNodeStatus.Valid)
                {
                    foreach (JORControl control in CurrentEditNode.Controls)
                        DrawControlContainer(jorServer, control);
                }

                ImGui.End();
            }
        }
    }
}
