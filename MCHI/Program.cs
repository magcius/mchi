namespace MCHI
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>

        public static void Main()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            Gui.GuiController.Init();
            var manager = new JORManager();

            while (true)
            {
                manager.Update();
                Gui.GuiController.Update(manager);
            }
        }
    }
}
