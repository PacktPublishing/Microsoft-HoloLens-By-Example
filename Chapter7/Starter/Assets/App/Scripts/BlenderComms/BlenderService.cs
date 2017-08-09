using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
#if WINDOWS_UWP
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices.WindowsRuntime; 
using Windows.Storage.Streams;
#endif 
using HoloToolkit.Unity;

public class BlenderService :  Singleton<BlenderService> {

    #region delegates and events 

    public delegate void DataReceived(byte[] data);
    public event DataReceived OnDataReceived = delegate { };

    public delegate void ConnectionStateChanged(ConnectionState state);
    public event ConnectionStateChanged OnConnectionStateChanged = delegate { }; 

    #endregion 

    #region types 

    public enum ConnectionState
    {
        Disconnected, 
        Connecting,  
        Connected, 
        Failed        
    }

    #endregion 

    #region properties and variables 

    public string ServiceIPAddress { get; set; }
    
    public int ServicePort { get; set; }

    private ConnectionState _state = ConnectionState.Disconnected;

    public ConnectionState State {
        get
        {
            return _state; 
        }
        private set
        {
            _state = value;

#if DEBUG
            Debug.LogFormat("State changed to {0}", _state);   
#endif 

            OnConnectionStateChanged(_state); 
        }
    }

    ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();

    const int receivingBufferSize = 512000;

    byte[] receivingBuffer = new byte[receivingBufferSize];

#if WINDOWS_UWP
    StreamSocket socket;
    DataWriter writer;
    DataReader reader;

    CancellationTokenSource tokenSource;
    Task receivingTask;

#else
    Socket clientSocket;
    private Thread receivingThread;
#endif

#endregion

    void Start () {

    }

    void Update () {
		if(State == ConnectionState.Connected)
        {
            while(queue.Count > 0)
            {
                var receivedData = queue.Dequeue();
                OnDataReceived(receivedData); 
            }
        }
	}

    private void OnDestroy()
    {
        Disconnect(); 
    }

    #region connection methods 

#if WINDOWS_UWP

    public async void Connect()
    {
        if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
        {
            throw new InvalidOperationException("Connection is active");
        }

        State = ConnectionState.Connecting;

        try{
            socket = new StreamSocket();
            socket.Control.KeepAlive = true;
            socket.Control.QualityOfService = SocketQualityOfService.Normal;
            socket.Control.OutboundBufferSizeInBytes = receivingBufferSize; 
            
            Debug.LogFormat("Trying to connect to service {0} {1}", ServiceIPAddress, ServicePort.ToString()); 

            await socket.ConnectAsync(new HostName(ServiceIPAddress), ServicePort.ToString());
            
            State = ConnectionState.Connected;
        }
        catch(Exception e)
        {
            Debug.LogFormat("Connecting failed to service {0} {1}, {2}", ServiceIPAddress, ServicePort.ToString(), e.ToString()); 
            
            State = ConnectionState.Failed;
            throw e;
        }

        Debug.LogFormat("Connected to service {0} {1}", ServiceIPAddress, ServicePort.ToString()); 

        // start receiving 
        writer = new DataWriter(socket.OutputStream);

        if (tokenSource == null)
        {
            tokenSource = new CancellationTokenSource();
        }

        receivingTask = Task.Factory.StartNew(SocketReceiveHandler, tokenSource.Token);               
    }

#else

    public void Connect()
    {        
        if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
        {
            throw new InvalidOperationException("Connection is active");
        }

        State = ConnectionState.Connecting;

        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(ServiceIPAddress, ServicePort);

            State = ConnectionState.Connected;
        }
        catch (Exception e)
        {
            State = ConnectionState.Failed;
            throw e;
        }

        StartReceiving();
    }

#endif

#if WINDOWS_UWP

    public void Disconnect()
    {
        Debug.Log("entering - Disconnect");

        if (socket == null)
        {
            return;
        }

        State = ConnectionState.Disconnected;

        if (writer != null)
        {
            writer.DetachStream();
            writer.Dispose();
            writer = null; 
        }

        if(reader != null)
        {
            reader.Dispose(); 
            reader = null; 
        }

        socket.Dispose();
        socket = null;

        Debug.Log("exiting - Disconnect");
    }

#else

    public void Disconnect()
    {
        Debug.Log("entering - Disconnect");

        if (clientSocket == null)
        {
            return;
        }

        State = ConnectionState.Disconnected;

        try
        {
            clientSocket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException e) {
            Debug.LogWarningFormat("ClientSocket Connection Exception {0}", e.ToString());
        }

        clientSocket.Close();

        StopReceiving();

        Debug.Log("exiting - Disconnect");
    }

#endif

    #endregion

    #region receiving

#if WINDOWS_UWP

    async void SocketReceiveHandler()
    {
        reader = new DataReader(socket.InputStream);
        
        byte[] previousReceivingBuffer = null;
        int remainingBytes = 0; 

        while (State == ConnectionState.Connected)
        {            
            if (State != ConnectionState.Connected)
            {
                return; 
            }

            Debug.Log("SocketReceiveHandler"); 
            
            // Set the DataReader to only wait for available data (so that we don't have to know the data size)
            reader.InputStreamOptions = Windows.Storage.Streams.InputStreamOptions.Partial;
            
            // The encoding and byte order need to match the settings of the writer we previously used.
            reader.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;

            try
            {
                var count = await reader.LoadAsync(receivingBufferSize);

                Debug.LogFormat("Received data from Blender Service {0}", count); 

                var receivingBuffer = new byte[count];
                reader.ReadBytes(receivingBuffer);
                
                if(remainingBytes > 0 && previousReceivingBuffer != null){
                    remainingBytes -= (int)count;

                    //Array.Resize<byte>(ref previousReceivingBuffer, previousReceivingBuffer.Length + receivingBuffer.Length);
                    //Array.Copy(receivingBuffer, 0, previousReceivingBuffer, previousReceivingBuffer.Length, receivingBuffer.Length); 

                    byte[] tmp = new byte[previousReceivingBuffer.Length + receivingBuffer.Length];
                    int idx = 0; 
                    for(var i=0; i<previousReceivingBuffer.Length; i++)
                    {
                        tmp[idx] = previousReceivingBuffer[i];
                        idx += 1;  
                    }
                    for (var i = 0; i < receivingBuffer.Length; i++)
                    {
                        tmp[idx] = receivingBuffer[i];
                        idx += 1;
                    }

                    previousReceivingBuffer = tmp; 

                    if (remainingBytes <= 0)
                    {
                        remainingBytes = 0;
                        receivingBuffer = previousReceivingBuffer;
                        previousReceivingBuffer = null; 
                    }
                }
                else
                {
                    int packetSize = BitConverter.ToInt32(receivingBuffer, 1);

                    remainingBytes = packetSize - (int)count;

                    if (remainingBytes > 0)
                    {
                        previousReceivingBuffer = receivingBuffer;
                    } 

                    Debug.LogFormat("received size {0}, packet size {1}, bytes remaining {2}", count, packetSize, remainingBytes); 
                }
                    
                if(remainingBytes <= 0){
                    queue.Enqueue(receivingBuffer);
                }
            }
            catch (Exception e) 
            { 
                Debug.LogFormat("Exception in SocketReceiveHandler {0}", e.ToString()); 
            }
        }
    }

#else

    void StartReceiving()
    {
        Debug.Log("StartReceiving");

        receivingThread = new Thread(new ThreadStart(SocketReceiveHandler));
        receivingThread.Start();
    }

    void StopReceiving()
    {
        if (receivingThread == null)
        {
            return;
        }

        receivingThread.Interrupt();
        receivingThread.Join(100);
    }

    void SocketReceiveHandler()
    {
        Debug.Log("entering - SocketReceiveHandler");

        byte[] previousReceivingBuffer = null;
        int remainingBytes = 0;

        while (State == ConnectionState.Connected)
        {
            try
            {
                int bytesReceived = clientSocket.Receive(receivingBuffer);

                Debug.LogFormat("Received {0} bytes", bytesReceived);

                if (bytesReceived <= 0 || State != ConnectionState.Connected)
                {
                    Debug.Log("SocketReceiveHandler - invalid state, returning");
                    return;
                }

                if (remainingBytes > 0 && previousReceivingBuffer != null)
                {
                    remainingBytes -= (int)bytesReceived;

                    byte[] tmp = new byte[previousReceivingBuffer.Length + receivingBuffer.Length];
                    int idx = 0; 
                    for(var i=0; i<previousReceivingBuffer.Length; i++)
                    {
                        tmp[idx] = previousReceivingBuffer[i];
                        idx += 1;  
                    }
                    for (var i = 0; i < receivingBuffer.Length; i++)
                    {
                        tmp[idx] = receivingBuffer[i];
                        idx += 1;
                    }

                    if (remainingBytes <= 0)
                    {
                        remainingBytes = 0;
                        receivingBuffer = previousReceivingBuffer;
                        previousReceivingBuffer = null;
                    }
                }
                else
                {
                    int packetSize = BitConverter.ToInt32(receivingBuffer, 1);

                    remainingBytes = packetSize - (int)bytesReceived;
                    
                    if(remainingBytes > 0){
                        previousReceivingBuffer = receivingBuffer;
                    } 

                    Debug.LogFormat("received size {0}, packet size {1}", bytesReceived, packetSize);
                }

                if (remainingBytes <= 0)
                {
                    queue.Enqueue(receivingBuffer);
                }
            }
            catch (SocketException e) {
                //Debug.LogWarningFormat("SocketException when receiving data {0}", e.ToString());
            }
        }

        Debug.Log("exiting - SocketReceiveHandler");
    }

#endif

    #endregion

    #region Sending 

    public void SendData(string data)
    {
        SendData(Encoding.ASCII.GetBytes(data));
    }

#if WINDOWS_UWP
    
    public async void SendData(byte[] data)
    {
        if(State != ConnectionState.Connected)
        {
            return; 
        }

        Debug.LogFormat("Sending packet of type {0}, size {1}", data[0], data.Length); 
        writer.WriteBytes(data);
        
        try
        {
            await writer.StoreAsync();
            //Debug.WriteLine(string.Format("Successfully sent {0} bytes", data.Length));
        }
        catch (Exception e) 
        {
            Debug.LogWarningFormat("Failed to send data, exception {0}", e.ToString());  
        }
    }

#else

    public void SendData(byte[] data)
    {
        if (State != ConnectionState.Connected)
        {
            return; 
        }

        SocketAsyncEventArgs socketAsyncData = new SocketAsyncEventArgs();
        socketAsyncData.SetBuffer(data, 0, data.Length);
        clientSocket.SendAsync(socketAsyncData);
    }

#endif

#endregion
}
