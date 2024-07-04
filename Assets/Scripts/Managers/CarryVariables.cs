using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using Photon.Pun;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class CarryVariables : MonoBehaviour
{
    public static CarryVariables instance;
    [Foldout("Prefabs", true)]
    public Player playerPrefab;
    public Card robotPrefab;
    public Card actionPrefab;
    public Popup cardPopup;
    public Popup textPopup;

    [Foldout("Right click", true)]
    [SerializeField] Transform rightClickBackground;
    [SerializeField] CanvasGroup cg;
    [SerializeField] CardLayout rightClickCard;

    [Foldout("Misc", true)]
    PhotonView pv;
    [SerializeField] Transform permanentCanvas;
    [ReadOnly] public Dictionary<string, MethodInfo> dictionary = new();
    public Sprite faceDownSprite;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Application.targetFrameRate = 60;
            pv = GetComponent<PhotonView>();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    public IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            yield return (IEnumerator)dictionary[methodName].Invoke(this, parameters);
    }

    void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(CarryVariables).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            rightClickBackground.gameObject.SetActive(false);
    }

    public void RightClickDisplay(float alpha, Sprite sprite, CardData dataFile, Color color)
    {
        rightClickBackground.gameObject.SetActive(true);
        cg.alpha = alpha;
        rightClickCard.FillInCards(dataFile, sprite, color);
    }
}
