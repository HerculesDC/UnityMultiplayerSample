using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour
{
    private NetworkClient nc;
    public string myID;
    [SerializeField] private GameObject _3DText;
    [SerializeField] float speed;

    void Awake() {
        nc = GameObject.Find("NetworkMan Client").GetComponent<NetworkClient>();
    }
    // Start is called before the first frame update
    void Start()
    {
        if (nc == null) {
            nc = GameObject.Find("NetworkMan Client").GetComponent<NetworkClient>();
        }
        if (myID != "") {
            _3DText.GetComponent<TextMesh>().text = myID;
            _3DText.GetComponent<TextMesh>().color = this.gameObject.GetComponent<MeshRenderer>().material.color;
        }
    }

    // Update is called once per frame
    void Update() //think of a way to rig this
    {
        if (myID != "")
        {
            if (_3DText.GetComponent<TextMesh>().text != myID) {
                _3DText.GetComponent<TextMesh>().text = myID;
            }
        }
        //else Debug.Log("Awaiting ID...");
        if (myID == nc.m_internalID) {
            Vector3 v = Vector3.zero;
            if (Input.GetKey(KeyCode.UpArrow))   { v.z += speed * Time.deltaTime; }
            if (Input.GetKey(KeyCode.DownArrow)) { v.z -= speed * Time.deltaTime; }
            if (Input.GetKey(KeyCode.LeftArrow)) { v.x -= speed * Time.deltaTime; }
            if (Input.GetKey(KeyCode.RightArrow)){ v.x += speed * Time.deltaTime; }
            this.gameObject.transform.position += v;
        }
    }
}
