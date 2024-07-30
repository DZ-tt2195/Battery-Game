using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Photon.Pun;
using MyBox;

[RequireComponent(typeof(PhotonView))]
public class UndoSource : MonoBehaviour
{
    public Dictionary<string, MethodInfo> methodDictionary = new();
    [ReadOnly] public PhotonView pv;

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        AddToMethodDictionary(methodName);
        MethodInfo info = methodDictionary[methodName];

        if (PhotonNetwork.IsConnected)
            pv.RPC(info.Name, affects, parameters);
        else if (info.ReturnType == typeof(IEnumerator))
            StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        else if (info.ReturnType == typeof(void))
            info.Invoke(this, parameters);
    }

    public void MultiFunction(string methodName, Photon.Realtime.Player specificPlayer, object[] parameters = null)
    {
        AddToMethodDictionary(methodName);
        MethodInfo info = methodDictionary[methodName];

        if (PhotonNetwork.IsConnected)
            pv.RPC(info.Name, specificPlayer, parameters);
        else if (info.ReturnType == typeof(IEnumerator))
            StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        else if (info.ReturnType == typeof(void))
            info.Invoke(this, parameters);
    }

    protected virtual void AddToMethodDictionary(string methodName)
    {
    }
}