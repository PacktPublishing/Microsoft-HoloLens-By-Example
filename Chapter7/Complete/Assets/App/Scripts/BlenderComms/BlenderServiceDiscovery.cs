#define LOGGING
using System; 
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using HoloToolkit.Unity;
using System.Threading;
#if WINDOWS_UWP
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime; 
#endif 

public class BlenderServiceDiscovery : Singleton<BlenderServiceDiscovery> {

    public delegate void ServiceDiscovered(string serviceName, string ipAddress, int port);
    public event ServiceDiscovered OnServiceDiscovered = delegate { }; 

    public int listenPort = 8881;
    public string serviceName = "blenderlive";

#if WINDOWS_UWP
    DatagramSocket socket; 
#else
    private Thread broadcastThread;
#endif 

    public bool IsListening { get; private set; }

    void Start () {
         
	}
	
	void Update () {
		
	}

    #region StartListening 

#if WINDOWS_UWP

    public async void StartListening()
    {
        if (IsListening) return;

        IsListening = true;
    
        socket = new DatagramSocket();
        socket.Control.MulticastOnly = false;
        socket.MessageReceived += SocketOnMessageReceived; 

        try
        {
            await socket.BindServiceNameAsync(listenPort.ToString());
            
            HostName remoteHost = new HostName("255.255.255.255");
            
            IOutputStream outputStream = await socket.GetOutputStreamAsync(remoteHost, listenPort.ToString());
            DataWriter writer = new DataWriter(outputStream);
            writer.WriteString("1");
            await writer.StoreAsync();

        }
        catch (Exception e) { }
    }

#else

    public void StartListening()
    {
        if (IsListening) return;

        IsListening = true;

        broadcastThread = new Thread(new ThreadStart(BroadcastListener));
        broadcastThread.Start();
    }

#endif

    #endregion

    #region StopListening 

#if WINDOWS_UWP

    public async void StopListening()
    {
        IsListening = false;

        if (socket != null)
        {
            await socket.CancelIOAsync();
            socket.Dispose();
        }

        socket = null; 
    }

#else

    public void StopListening()
    {
        if (!IsListening) return;

        IsListening = false;

        if (broadcastThread == null)
        {
            return;
        }

        broadcastThread.Interrupt();
        broadcastThread.Join(100);
    }

#endif 

#endregion

    private void OnDestroy()
    {
        StopListening(); 
    }

    #region Listener  

#if WINDOWS_UWP

    private async void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
    {
        var result = args.GetDataStream();
        var resultStream = result.AsStreamForRead(1024);

        if (!IsListening) return;

        using (var reader = new StreamReader(resultStream))
        {
            var text = await reader.ReadToEndAsync();
            
            Debug.LogFormat("Service discovered {0}", text);
            
            if (text.Contains(serviceName))
            {
                string[] split = text.Split(':');
                Debug.LogFormat("Broadcasting discovered service {0}", text);
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    Debug.LogFormat("Broadcasting {0} {1}", args.RemoteAddress.DisplayName, split[1]);
                    OnServiceDiscovered(text, args.RemoteAddress.DisplayName, int.Parse(split[1]));
                });
            }                
        }
    }

#else 

    void BroadcastListener()
    {
#if LOGGING
        Debug.Log("entering - BroadcastListener");
#endif
        
        UdpClient listener = new UdpClient(listenPort);
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            while (IsListening)
            {
                byte[] bytes = listener.Receive(ref groupEP);

                var message = Encoding.ASCII.GetString(bytes, 0, bytes.Length);

                if (message != null)
                {
                    var messageSplit = message.Split(':');

                    if (messageSplit.Length == 2)
                    {
                        if (messageSplit[0].Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            string ipAddress = groupEP.Address.ToString();
                            int port;

                            if (int.TryParse(messageSplit[1], out port))
                            {
#if LOGGING
                                Debug.LogFormat("{0}:{1}", ipAddress, port);
#endif

                                OnServiceDiscovered(message, ipAddress, port);
                            }
                        }
                    }
                }                    
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.ToString());
        }
        finally
        {
            listener.Close();
        }

#if LOGGING
        Debug.Log("exiting - BroadcastListener");
#endif
    }

#endif 

#endregion
}
