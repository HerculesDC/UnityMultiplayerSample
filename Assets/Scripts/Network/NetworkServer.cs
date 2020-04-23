using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Collections.Generic;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    private List<NetworkObjects.NetworkPlayer> m_Players;

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        m_Players = new List<NetworkObjects.NetworkPlayer>(16);
        //InvokeRepeating("PrintReport", 0, 3);
    }
    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Players.Clear(); //Will probably require a more refined approach
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c){
        m_Connections.Add(c);
        
        m_Players.Add(new NetworkObjects.NetworkPlayer());
        m_Players[m_Players.Count - 1].id = c.InternalId.ToString();

        //Yeah, I know I did something unreadable here. 
        //But it felt more practical to assign these things simultaneously
        PlayerUpdateMsg msg = new PlayerUpdateMsg();
        msg.player.id = m_Players[m_Players.Count - 1].id;

        System.Random r = new System.Random();
        msg.player.r = m_Players[m_Players.Count - 1].r = (float)r.NextDouble();
        msg.player.g = m_Players[m_Players.Count - 1].g = (float)r.NextDouble();
        msg.player.b = m_Players[m_Players.Count - 1].b = (float)r.NextDouble();
        msg.player.x = m_Players[m_Players.Count - 1].x = r.Next(0, 16);
        msg.player.y = m_Players[m_Players.Count - 1].y = r.Next(0, 16);
        msg.player.z = m_Players[m_Players.Count - 1].z = r.Next(0, 16);

        SendToClient(JsonUtility.ToJson(msg), c);
        Debug.Log("Accepted a connection! ID: "+c.InternalId.ToString()+
            ".\nTotal players connected: "+m_Connections.Length.ToString());
        //Create Cube message:
        SendUpdateMessage();
        PrintReport();
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            Debug.Log(recMsg);
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            //Debug.Log("Player update message received!");
            ProcessPlayerMessage(puMsg);
            PrintReport();
            break;
            case Commands.SERVER_UPDATE: //Doesn't make much sense here, but still
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    void OnDisconnect(int i){
        Debug.Log("Client disconnected from server");
        ProcessDisconnect(i);
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }
        
        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }
    void ProcessPlayerMessage(PlayerUpdateMsg msg) {
        for (int i = 0; i < m_Players.Count; ++i) {
            if (msg.player.id == m_Players[i].id) {
                m_Players[i].x = msg.player.x;
                m_Players[i].y = msg.player.y;
                m_Players[i].z = msg.player.z;
            }
        }
        SendUpdateMessage();
    }
    void ProcessDisconnect(int connectionIndex) {
        //NOT WORKING, FOR WHATEVER REASON
        /*
        for(int i = 0; i < m_Players.Count; ++i) {
            if (m_Players[i].id == m_Connections[i].InternalId.ToString()) {
                m_Players[i] = default(NetworkObjects.NetworkPlayer);
            }
        }
        SendUpdateMessage();
        */
    }
    void SendUpdateMessage() {
        ServerUpdateMsg suMsg = new ServerUpdateMsg();
        foreach (NetworkObjects.NetworkPlayer player in m_Players)
        {
            suMsg.players.Add(player);
        }
        string message = JsonUtility.ToJson(suMsg);
        foreach (var player in m_Connections)
        {
            //Debug.Log(sumsg.players);
            SendToClient(message, player);
        }
    }
    void PrintReport() {
        Debug.Log("Connections: " + m_Connections.Length.ToString());
        foreach (var player in m_Players) {
            if (player != default(NetworkObjects.NetworkPlayer)) {
                Vector3 temp = new Vector3(player.x, player.y, player.z);
                Debug.Log("Player: " + player.id.ToString() +
                    ". Position: " + temp.ToString());
            }
        }
    }
}