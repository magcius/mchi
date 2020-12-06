using System;
using System.Collections.Generic;
using System.Text;

namespace MCHI
{
    class JORManager
    {
        public HIO2Server server;
        public JHIClient jhiClient;
        public JORServer jorServer;

        public HIO2ServerClient currentClient = null;

        public JORManager()
        {
            server = new HIO2Server();
        }

        private void SetCurrentClient(HIO2ServerClient client)
        {
            if (client == currentClient)
                return;

            currentClient = client;
            jhiClient = currentClient != null ? new JHIClient(currentClient) : null;
            jorServer = currentClient != null ? new JORServer(jhiClient) : null;
        }

        public void Update()
        {
            server.Update();
            SetCurrentClient(server.Client);
            if (jhiClient != null && currentClient.IsConnected())
                jhiClient.Update();
            if (jorServer != null)
                jorServer.Update();
        }
    }
}
