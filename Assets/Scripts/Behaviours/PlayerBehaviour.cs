using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour
{
    public string myID;
    [SerializeField] private GameObject _3DText;

    void Awake() {
    }
    // Start is called before the first frame update
    void Start()
    {
        if (myID != "") {
            _3DText.GetComponent<TextMesh>().text = myID;
            _3DText.GetComponent<TextMesh>().color = this.gameObject.GetComponent<MeshRenderer>().material.color;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (myID != "")
        {
            if (_3DText.GetComponent<TextMesh>().text != myID) {
                _3DText.GetComponent<TextMesh>().text = myID;
            }
        }
        else Debug.Log("Awaiting ID...");
    }
}
