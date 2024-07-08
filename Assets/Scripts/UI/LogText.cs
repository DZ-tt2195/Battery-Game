using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyBox;

public class LogText : MonoBehaviour
{
    public TMP_Text textBox;
    public Image undoBar;

    private void Awake()
    {
        textBox = GetComponent<TMP_Text>();
        undoBar = this.transform.GetChild(0).GetComponent<Image>();
        undoBar.gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        undoBar.SetAlpha(Manager.instance.opacity);
    }
}
