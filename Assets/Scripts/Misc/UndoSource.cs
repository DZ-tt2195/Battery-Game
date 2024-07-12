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
    [SerializeField] protected List<string> executeInstructions = new();
    [ReadOnly] public PhotonView pv;

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!methodDictionary.ContainsKey(methodName))
            AddToMethodDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(methodDictionary[methodName].Name, affects, parameters);
        else
            methodDictionary[methodName].Invoke(this, parameters);
    }

    public IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!methodDictionary.ContainsKey(methodName))
            AddToMethodDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(methodDictionary[methodName].Name, affects, parameters);
        else
            yield return (IEnumerator)methodDictionary[methodName].Invoke(this, parameters);
    }

    public void UndoCommand(NextStep step)
    {
        if (!methodDictionary.ContainsKey(step.instruction))
            AddToMethodDictionary(step.instruction);

        try
        {
            StartCoroutine((IEnumerator)methodDictionary[step.instruction].Invoke(this, new object[2] { this, true }));
        }
        catch
        {
            methodDictionary[step.instruction].Invoke(this, new object[2] { this, true });
        }
    }

    protected virtual void AddToMethodDictionary(string methodName)
    {
    }
}
