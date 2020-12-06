using System;
using System.Text;
using System.Collections.Generic;

using System.IO;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using ImGuiNET;
using static ImGuiNET.ImGuiNative;

namespace MCHI.Gui
{
    static class GuiController
    {

        private static Sdl2Window _window;
        private static GraphicsDevice _gd;
        private static CommandList _cl;

        private static ImGuiController _controller;
        private static Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
        public static JORNode currentEditNode;


        public static void init()
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

        public static void update()
        {
            if (!_window.Exists) { Environment.Exit(0); return; }
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) { return; }
            _controller.Update(1f / 60f, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui
            SubmitUI();
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


        private static string getText(JORControl cnt) // FOR LOCALIZATION OVERRIDE
        {

            return cnt.Name;
        }

        private static string getText(JORNode cnt) // FOR LOCALIZATION OVERRIDE
        {
            return cnt.Name;
        }


        private static void DrawTree(JORNode node)
        {
            if (node == null) // does the node even exist?
                return;
            if (node.Name == null) // Does the node not have a name?
                return;
            if (node.Status == JORNodeStatus.Invalid) // Is the node invalid?
                return;
            if (node.Status == JORNodeStatus.GenRequestSent) // Is the node waiting for a generator request?
            {
                if (node.lastRequestTime == null || (DateTime.Now - node.lastRequestTime).Seconds > 2) // If it is not set, or has been two seconds -- rerequest. 
                    JORManager.jorServer.SendGenObjectInfo(node);  // rerequest the node
                return; // return 
            }

            if (node.Children != null && node.Children.Count != 0) // Check if there's nothing to draw in this node
            {
                if (ImGui.TreeNode(getText(node) + "##" + node.NodePtr)) // Draw the node name, use the pointer as an ID Index to prevent duplicates in IMGUI Memory
                {
                    if (ImGui.Selectable("$ - " + getText(node), currentEditNode == node)) // Create a button for the root, since tree's don't have a "clicked" event. 
                        currentEditNode = node; // if the root object has been clicked, assign the node as the currently edited node.
                    foreach (JORNode cnode in node.Children) // Draw the children 
                        DrawTree(cnode); // Draw each child as the tree recursivelyt                 

                    DrawStyledTextInstance("____________________________________", 0xFF0000FF);
                    ImGui.TreePop(); // Push tree level left once we're done drawing that sublevel               
                }

            }
            else if (ImGui.Selectable(node.Name + "##" + node.NodePtr, currentEditNode == node)) // If it doesn't have any children, just create a selectable button for it. 
                currentEditNode = node; // When clicked, assign it as the currently edited node. 
        }


      

        public static void DrawControlContainer(JORControl control)
        {
            // ImGui.PushFont(fnt);
            if (control.Node.Status == JORNodeStatus.Invalid)
                return;
            switch (control.Type)
            {
                case "LABL": // Label
                    {
                        var jorLabel = control as JORControlLabel; // Cast to appropriate type
                        ImGui.Text(getText(control)); // labels are simple, just draw the text
                        break;
                    }
                case "BUTN": // Button
                    {
                        var jorButton = control as JORControlButton; // Cast to appropriate type
                        if (ImGui.Button(getText(jorButton) + "##" + control.ID))
                            jorButton.Click(JORManager.jorServer); // send click
                        break;
                    }
                case "CHBX": // Checkbox
                    {
                        var jorCheckbox = control as JORControlCheckBox; // Cast to appropriate type
                        var val = jorCheckbox.Value; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        ImGui.Checkbox(jorCheckbox.Name + "##" + control.ID, ref val); // Draw control, reference to val, feed into SetValue (has changed check)
                        jorCheckbox.SetValue(JORManager.jorServer, val);
                        break;
                    }
                case "RNGi": // Integer Range
                    {
                        var jorRange = control as JORControlRangeInt; // Cast to appropriate type
                        var val = jorRange.Value; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        ImGui.SliderInt(getText(jorRange)+ "##" + control.ID, ref val, jorRange.RangeMin, jorRange.RangeMax); // Draw control, reference to val, feed into SetValue (has changed check)
                        jorRange.SetValue(JORManager.jorServer, val);
                        break;
                    }

                case "RNGf": // Float Range
                    {
                        var jorRange = control as JORControlRangeFloat; // Cast to appropriate type
                        var val = jorRange.Value; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        ImGui.SliderFloat(getText(jorRange) + "##" + control.ID, ref val, jorRange.RangeMin, jorRange.RangeMax); // Draw control, reference to val, feed into SetValue (has changed check)
                        jorRange.SetValue(JORManager.jorServer, val);
                        break;
                    }
                case "CMBX":
                    {
                        var jorSelector = control as JORControlSelector;   // Cast to appropriate type
                        var names = new string[jorSelector.Items.Count]; // Storage for control names
                        for (int i = 0; i < jorSelector.Items.Count; i++) // Loop through each and 
                            names[i] = jorSelector.Items[i].Name; // Put their names into the string array.
                        var val = (int)jorSelector.SelectedIndex; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        ImGui.Combo(getText(jorSelector) + "##" + control.ID, ref val, names, names.Length); // Draw control, reference to val, feed into SetValue (has changed check)
                        jorSelector.SetSelectedIndex(JORManager.jorServer, (uint)val);

                        break;
                    }
                case "RBTN":
                    {
                        var jorSelector = control as JORControlSelector;  // Cast to appropriate type
                        var names = new string[jorSelector.Items.Count]; // Storage for control names
                        for (int i = 0; i < jorSelector.Items.Count; i++)// Loop through each and 
                            names[i] = jorSelector.Items[i].Name;  // Put their names into the string array.
                        var val = (int)jorSelector.SelectedIndex; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        ImGui.Combo(getText(jorSelector) + "##" + control.ID, ref val, names, names.Length); // Draw control, reference to val, feed into SetValue (has changed check)
                        jorSelector.SetSelectedIndex(JORManager.jorServer, (uint)val);
                        break;
                    }
                case "EDBX":
                    {
                        /// TODO!!!
                        /// If someone types in the textbox, it might overflow the buffer. I don't know yet. I haven't found a text box to type in.
                        /// Figure out a way to introduce a typable text buffer. 
                        var jorEdit = control as JORControlEditBox;
                        var val = jorEdit.Text; // Store the old value (if we update it on the object, the code in JORServer will never send it to the game)
                        var buff = Encoding.UTF8.GetBytes(val);
                        ImGui.InputText(getText(jorEdit) + "##" + control.ID, buff, (uint)buff.Length); // Draw control, reference to val, feed into SetValue (has changed check)
                        var newBuff = Encoding.UTF8.GetString(buff);
                        jorEdit.SetValue(JORManager.jorServer, newBuff);
                        break;
                    }
                case "GBRX": // Should be GRBX, but imgui doesn't have a good control for this.
                    {
                        break;
                    }
                default:
                    DrawStyledTextInstance($"Unimplemented control '{control.Type}'", 0xFF0000FF);
                    break;
            }

        }
        static bool ye = true;
        static int largestBufferUntil0 = 1;
        public static void SubmitUI()
        {
            //ImGui.ShowDemoWindow();

            ImGui.Begin("MAIN_WINDOW", ref ye, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar); // draw main window
            ImGui.SetWindowPos(new Vector2(0, 0), ImGuiCond.Once); // set winddow pos
            ImGui.SetWindowSize(new Vector2(350, 768)); // Set its size 

            var col = 0xFF0000FF; // init status text color
            var Text = "Not Connected"; // init status text text
            //if (Program.currentClient != null)
            //Console.WriteLine(Program.currentClient.IsConnected());
            if (JORManager.currentClient != null && JORManager.currentClient.IsConnected()) // check if the client exists and is connected
            {
                var datsize = JORManager.jhiClient.GetUnprocessedDataSize() + 1; // do +1 so we don't divide by zero.
                col = 0xFF00FF00; // Green 
                Text = $"Connected, buffer size {datsize - 1:X8}"; // New text
                if (datsize == 1) // Empty buffer
                    largestBufferUntil0 = 1; // Clear progress bar status
                else if (datsize > largestBufferUntil0) // Set progress bar to newer buffer size
                    largestBufferUntil0 = datsize; // New size
                ImGui.ProgressBar((largestBufferUntil0 - (float)datsize) / (float)largestBufferUntil0); // draw progress bar
            }

            if (ImGui.Button("Sync root")) // Sync root button
                JORManager.jorServer.SendGetRootObjectRef();  // Tell server to refresh

            DrawStyledTextInstance(Text, col); // Draw status text

            ImGui.BeginChild("JorTreeContainer"); // Creates a child panel to put the tree in. Prevents the main window from having to scroll, leaving the bar and the "sync root" always visible.
            if (JORManager.jorServer != null && JORManager.jorServer.Root != null && JORManager.jorServer.Root.TreeRoot != null)
                DrawTree(JORManager.jorServer.Root.TreeRoot); // Draw the root tree
            ImGui.EndChild();
            ImGui.End();

            /// CONTROL WINDOW
            ImGui.Begin("CONTROL_WINDOW", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar); // Init window
            ImGui.SetWindowPos(new Vector2(351, 0), ImGuiCond.Once); // Init pos
            ImGui.SetWindowSize(new Vector2(673, 768), ImGuiCond.Once); // Innit size

            if (currentEditNode != null && currentEditNode.Name != null && currentEditNode.Status == JORNodeStatus.Valid) // Is node valid?
            {
                foreach (JORControl control in currentEditNode.Controls) // Draw every control.
                    DrawControlContainer(control);
            }
            ImGui.End(); // End frame.
        }
    }
}
