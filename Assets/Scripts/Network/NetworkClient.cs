using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public string m_internalID;
    [SerializeField] private GameObject m_model;

    public List<NetworkObjects.NetworkPlayer> m_Players;
    public List<GameObject> m_cubes;
    
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);

        m_Players = new List<NetworkObjects.NetworkPlayer>();
        m_cubes = new List<GameObject>();
        m_internalID = "";
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
    }

    void OnData(DataStreamReader stream){
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
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            ProcessPlayerUpdateMessage(puMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);

            UpdatePlayers(suMsg);
            //Debug.Log("Server update message received!");
            Debug.Log("Players: " + suMsg.players.Count.ToString());
            break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }
        
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }

        foreach (NetworkObjects.NetworkPlayer player in m_Players) {
            if (player.id == m_internalID) {
                if (Int32.TryParse(player.id, out int index)) {
                    if (player.x != m_cubes[index].transform.position.x ||
                    player.z != m_cubes[index].transform.position.z) {
                        KeepAlive();
                    }
                }
                
            }
        }
    }
    void UpdatePlayers(ServerUpdateMsg s) {
        Debug.Log("Server players: " + s.players.Count.ToString() + ",Client Players: " + m_Players.Count.ToString());
        if (s.players.Count > m_Players.Count)
        { //spawn new
            foreach (NetworkObjects.NetworkPlayer player in s.players)
            {
                if (!m_Players.Contains(player))
                {
                    m_Players.Add(player);
                    SpawnPlayer(player);
                }
            }
        }
        else if (s.players.Count < m_Players.Count) {
            foreach (NetworkObjects.NetworkPlayer player in m_Players) {
                if (!s.players.Contains(player)) {
                    if (Int32.TryParse(player.id, out int index)){
                        Destroy(m_cubes[index]);
                        m_cubes.Remove(m_cubes[index]);
                        m_Players.Remove(player);
                    }
                }
            }
        }
        else {
            foreach (NetworkObjects.NetworkPlayer clientPlayer in m_Players) {
                foreach (NetworkObjects.NetworkPlayer serverPlayer in m_Players) {
                    if (serverPlayer.id == m_internalID) {
                        continue;
                    }
                    else if (serverPlayer.id == clientPlayer.id) {
                        clientPlayer.x = serverPlayer.x;
                        clientPlayer.y = serverPlayer.y;
                        clientPlayer.z = serverPlayer.z;
                        //this should be a separate function, but meh...
                        if (Int32.TryParse(clientPlayer.id, out int index)) {
                            m_cubes[index].transform.position = new Vector3(clientPlayer.x, clientPlayer.y, clientPlayer.z);
                        }
                    }
                }
            }
        }
    }
    void SpawnPlayer(NetworkObjects.NetworkPlayer newPlayer) {

        Vector3 pos = new Vector3(newPlayer.x, newPlayer.y, newPlayer.z);
        Color c = new Color(newPlayer.r, newPlayer.g, newPlayer.b);
        string s = newPlayer.id;
        GameObject temp = GameObject.Instantiate(m_model, pos, Quaternion.identity, null);
        temp.GetComponent<PlayerBehaviour>().myID = s;
        temp.GetComponent<MeshRenderer>().material.color = c;
        m_cubes.Add(temp); //Uses the NetworkObject part of NetworkPlayer
    }
    void ProcessPlayerUpdateMessage(PlayerUpdateMsg msg) { //doesn't check if synchronous connections happen
        if (m_internalID == "") {
            m_internalID = msg.player.id;
            //SpawnPlayer(msg.player);
        }
        else if (m_internalID == msg.player.id) {
            //update player status
        }
    }
    void KeepAlive() {
        if (Int32.TryParse(m_internalID, out int i)) {
            PlayerUpdateMsg pum = new PlayerUpdateMsg();
            pum.player.id = m_internalID;
            //it's not THIS gameObject: gotta find the correct cube
            pum.player.x = m_cubes[i].transform.position.x;
            pum.player.y = m_cubes[i].transform.position.y;
            pum.player.z = m_cubes[i].transform.position.z;
            SendToServer(JsonUtility.ToJson(pum));
        }
    }
}