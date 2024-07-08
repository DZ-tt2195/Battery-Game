using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class UndoSource : MonoBehaviour
{
    protected Dictionary<string, string> undoDictionary = new();
    public Dictionary<string, MethodInfo> methodDictionary = new();
    [SerializeField] protected List<string> executeInstructions = new();

    public virtual void ExecuteCommand(Player player)
    {
    }

    public virtual IEnumerator UndoCommand(UndoStep step)
    {
        yield return null;
    }

    protected void AddToUndoDictionary(string firstName, string secondName)
    {
        if (!undoDictionary.ContainsKey(firstName))
            undoDictionary.Add(firstName, secondName);
    }

    protected virtual void AddToMethodDictionary(string methodName)
    {
    }
}
