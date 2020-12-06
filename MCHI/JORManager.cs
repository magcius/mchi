using System;
using System.Collections.Generic;
using System.Text;

namespace MCHI
{
    static class JORManager
    {

        public static HIO2Server server;
        public static JHIClient jhiClient;
        public static JORServer jorServer;
        public static HIO2ServerClient currentClient = null;


        public static void init()
        {
            server = new HIO2Server();
        }
        private static void SetCurrentClient(HIO2ServerClient client)
        {
            if (client == currentClient)
                return;

            currentClient = client;
            jhiClient = currentClient != null ? new JHIClient(currentClient) : null;
            jorServer = currentClient != null ? new JORServer(jhiClient) : null;
            Console.WriteLine("Updated client");
        }

        public static void processUpdateTasks()
        {
            server.Update();
            SetCurrentClient(server.Client);
            if (jhiClient != null && currentClient.IsConnected())
                jhiClient.Update();
            if (jorServer != null)
            {
                jorServer.Update();
            }
        }
    }
}
