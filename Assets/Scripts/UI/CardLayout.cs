using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MyBox;
using TMPro;

public class CardLayout : MonoBehaviour, IPointerClickHandler
{
    CardData dataFile;
    [ReadOnly] public CanvasGroup cg;
    [SerializeField] Image background;
    TMP_Text titleText;
    TMP_Text description;
    TMP_Text coinText;
    TMP_Text crownText;

    private void Awake()
    {
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
        titleText = cg.transform.Find("Title").GetComponent<TMP_Text>();
        description = cg.transform.Find("Description").GetComponent<TMP_Text>();

        try
        {
            coinText = cg.transform.Find("Coin").GetComponent<TMP_Text>();
            crownText = cg.transform.Find("Crown").GetComponent<TMP_Text>();
        }
        catch
        {

        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            RightClickInfo();
        }
    }

    public void FillInCards(CardData dataFile, Color color)
    {
        this.dataFile = dataFile;

        try
        {
            background.color = color;
        }
        catch
        {
            Debug.LogError($"{this.name} has no background");
        }
        titleText.text = dataFile.cardName;

        if (dataFile.startingBatteries < 0)
        {
            description.text = KeywordTooltip.instance.EditText(dataFile.textBox);
        }
        else
        {
            description.text = KeywordTooltip.instance.EditText($"{dataFile.startingBatteries} Battery\n\n{dataFile.textBox}");
        }

        if (coinText != null)
        {
            if (dataFile.coinCost >= 0)
            {
                coinText.gameObject.SetActive(true);
                coinText.text = $"{dataFile.coinCost} Coin";
                coinText.text = KeywordTooltip.instance.EditText(coinText.text);
            }
            else
            {
                coinText.gameObject.SetActive(false);
            }
        }

        if (crownText != null)
        {
            if (dataFile.scoringCrowns >= 0)
            {
                crownText.gameObject.SetActive(true);
                crownText.text = $"{dataFile.scoringCrowns} Pos Crown";
                crownText.text = KeywordTooltip.instance.EditText(crownText.text);
            }
            else
            {
                crownText.gameObject.SetActive(false);
            }
        }
    }

    void RightClickInfo()
    {
        CarryVariables.instance.RightClickDisplay(cg.alpha, this.dataFile, background.color);
    }
}
