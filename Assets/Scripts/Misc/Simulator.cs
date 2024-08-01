using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    [SerializeField] int numCards;
    [SerializeField] int numCoins;
    [SerializeField] int targetCrown;
    int playedCards;

    void Start()
    {
        RunLoop();
    }

    void RunLoop()
    {
        //default card: $4, 3 Crowns, no abilities/batteries
        float turnCount = 0;
        while (playedCards*3 < targetCrown)
        {
            turnCount++;
            if (numCards == 0)
            {
                numCards += 2;
            }
            else if (numCoins == 0)
            {
                numCoins += 4;
            }
            else
            {
                numCards -= 1;
                numCoins -= 4;
                playedCards++;
            }
        }

        int currentTurn = (int)turnCount; 

        turnCount -= (playedCards * 3) - targetCrown;
        turnCount -= (float)(numCards / 2f);
        turnCount -= (float)(numCoins / 4f);

        Debug.Log($"Turns: {turnCount:F2} ({currentTurn-turnCount:F2} overshoot)" +
            $"\nCards played: {playedCards}" +
            $"\nCrown overshot: {(playedCards*3)-targetCrown}" +
            $"\nLeftover cards: {numCards}" +
            $"\nLeftover coins: {numCoins}");
    }
}
