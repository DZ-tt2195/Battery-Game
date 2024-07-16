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
    public Button button;

    private void Awake()
    {
        textBox = GetComponent<TMP_Text>();
        undoBar = this.transform.GetComponentInChildren<Image>();
        undoBar.gameObject.SetActive(false);
        button = GetComponent<Button>();
    }

    private void FixedUpdate()
    {
        undoBar.SetAlpha(Manager.instance.opacity);
    }
}
