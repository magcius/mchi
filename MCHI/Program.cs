using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ImGuiNET;
namespace MCHI
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>



        private static int fslU = 0; 
        

        static void Main()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            JORManager.init();
            guStart();
        }

        static void guStart()
        {
            Gui.GuiController.init(); // Initializes GUI controller
            while (true)
            {
                Gui.GuiController.update(); // Call update routine
                fslU++;
                if (fslU > 8) // Updates every 8 rames
                {
                    JORManager.processUpdateTasks(); // Call update
                    fslU = 0; // Reset update counter. 
                }
            }            
        }
    }
}
