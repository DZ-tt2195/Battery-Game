using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class ConnectToLobby : MonoBehaviourPunCallbacks
{
    [SerializeField] Button localPlay;

    public void Start()
    {
        localPlay.onClick.AddListener(LocalPlay);
        Application.targetFrameRate = 60;
    }

    void LocalPlay()
    {
        Debug.Log("press local play");
        SceneManager.LoadScene("2. Game");
    }

    public void Join(string region)
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = region;
        PhotonNetwork.ConnectUsingSettings();
        PlayerPrefs.SetString("Online Username", "");
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("press online play");
        SceneManager.LoadScene("1. Lobby");
    }
}
