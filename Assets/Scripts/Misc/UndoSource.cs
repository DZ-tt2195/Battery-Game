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

    public virtual void ExecuteCommand(Player player)
    {
    }

    public virtual IEnumerator UndoCommand(UndoStep step)
    {
        yield return null;
    }

    protected virtual void AddToMethodDictionary(string methodName)
    {
    }
}
