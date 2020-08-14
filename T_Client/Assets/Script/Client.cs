using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;

public class Client : MonoBehaviour
{
    public static Client Instance {private set;get;}

    private const int MAX_USER = 100;
    private const int PORT = 26000;
    private const int WEB_PORT = 26001;
    private const int BYTE_SIZE = 1024;
    private const string SERVER_IP ="127.0.0.1"; 


    private byte reliableChannel;
    private int connectionId;
    private int hostId;
    private byte error;

    private bool isStarted;

    #region Monobehaviour
    private void Start() 
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }
    private void Update() 
    {
        UpdateMessagePump();
    }
    #endregion

    public void Init()
    {
        NetworkTransport.Init();
        ConnectionConfig cc = new ConnectionConfig();
        reliableChannel = cc.AddChannel(QosType.Reliable);

        HostTopology topo = new HostTopology(cc,MAX_USER);
        
        //Client Only Server
        hostId = NetworkTransport.AddHost(topo,0);


#if UNITY_WEBGL && !UNITY_EDITOR
        // web Client
        connectionId = NetworkTransport.Connect(hostId,SERVER_IP,WEB_PORT,0, out error);   
        Debug.Log("Connecting from WebGL");
#else
        // Standalone Client
        connectionId = NetworkTransport.Connect(hostId,SERVER_IP,PORT,0, out error);
        Debug.Log("Connecting from Standalone");   
#endif        
        
        Debug.Log(string.Format("Attempting to connect on Server {0}....",SERVER_IP));
        isStarted = true;
    }

    public void Shutdown()
    {
        isStarted = false;
        NetworkTransport.Shutdown();
    }

    public void  UpdateMessagePump()
    {
        if(!isStarted)
            return;

        int recHostId;      // Is this webgl or standalone
        int connectionId;   // which user is sending this 
        int channelId;      // which line is he using

        byte[] recBuffer = new byte[BYTE_SIZE];
        int dataSize;

        NetworkEventType type = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, BYTE_SIZE,out dataSize,out error);

        switch(type)
        {
            case NetworkEventType.Nothing:
                    break;

            case NetworkEventType.ConnectEvent:
                    Debug.Log("We have Connected to the server");
                    break;

            case NetworkEventType.DisconnectEvent:
                    Debug.Log("We have been disconnected");
                    break;

            case NetworkEventType.DataEvent:
                    BinaryFormatter formatter = new BinaryFormatter();
                    MemoryStream ms = new MemoryStream(recBuffer);
                    NetMessage msg = (NetMessage)formatter.Deserialize(ms);

                    OnData(connectionId,channelId,recHostId,msg);
                    break;

            default:
            case NetworkEventType.BroadcastEvent:
                    Debug.Log("Unexpected Network Event Type");
                    break;     
        }
    }

 #region OnDATA
    private void OnData(int cnnId,int channelId,int recHostId,NetMessage msg)
    {
        switch(msg.OP)
        {
            case NetOP.None:
                Debug.Log("Unexpected Net OP");
                break;

            case NetOP.OnCreateAccount:
                OnCreateAccount((Net_OnCreateAccount)msg);
                break;

            case NetOP.OnLoginRequest:
                OnLoginRequest((Net_OnLoginRequest)msg);
                break;    
        }
    }

    private void OnCreateAccount(Net_OnCreateAccount oca)
    {
        LobbyScene.Instance.EnableInputs();
        LobbyScene.Instance.ChangeAuthenticationMessage(oca.Information);
    }

    private void OnLoginRequest(Net_OnLoginRequest olr)
    {
        LobbyScene.Instance.ChangeAuthenticationMessage(olr.Information);
        if(olr.Success != 1)
        {
            //Unable to login
            LobbyScene.Instance.EnableInputs();
        }
        else
        {
            //Successful Login
        }
    }
#endregion

#region Send
    public void SendServer(NetMessage msg)
    {
        // This is where we store our data
        byte[] buffer = new byte[BYTE_SIZE];

        // This is where we crush our data to a byte[]
        BinaryFormatter formatter = new BinaryFormatter();
        MemoryStream ms = new MemoryStream(buffer);
        formatter.Serialize(ms,msg);

        NetworkTransport.Send(hostId,connectionId,reliableChannel,buffer,BYTE_SIZE,out error);
    }

    public void SendCreateAccount(string username, string password,string email)
    {
        Net_CreateAccount ca = new Net_CreateAccount();
        ca.Username = username;
        ca.Password = Utility.Sha256FromString(password);
        ca.Email = email;

        SendServer(ca);
    }
    public void SendLoginRequest(string usernameOrEmail, string password)
    {
        Net_LoginRequest lr = new Net_LoginRequest();
        lr.UsernameOrEmail = usernameOrEmail;
        lr.Password = Utility.Sha256FromString(password);

        SendServer(lr);
    }
#endregion
}
