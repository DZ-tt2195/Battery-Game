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
        if (!methodDictionary.ContainsKey(methodName))
            AddToMethodDictionary(methodName);

        MethodInfo info = methodDictionary[methodName];
        if (info.ReturnType == typeof(IEnumerator))
        {
            if (PhotonNetwork.IsConnected)
                pv.RPC(info.Name, affects, parameters);
            else
                StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        }
        else if (info.ReturnType == typeof(void))
        {
            if (PhotonNetwork.IsConnected)
                pv.RPC(info.Name, affects, parameters);
            else
                info.Invoke(this, parameters);
        }
    }

    protected virtual void AddToMethodDictionary(string methodName)
    {
    }
}
