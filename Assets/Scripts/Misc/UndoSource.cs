using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Photon.Pun;
using MyBox;

public class UndoSource : MonoBehaviour
{
    public Dictionary<string, MethodInfo> methodDictionary = new();
    [SerializeField] protected List<string> executeInstructions = new();
    [ReadOnly] public PhotonView pv;

    public void UndoCommand(UndoStep step)
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
