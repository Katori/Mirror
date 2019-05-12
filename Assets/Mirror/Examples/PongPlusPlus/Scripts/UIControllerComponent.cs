using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIControllerComponent : MonoBehaviour
{
    internal static UIControllerComponent Instance { get; private set; }

    [SerializeField]
    private Text ScoreText = default;

    [SerializeField]
    private GameObject ServePanel = default;

    private int Team1Score;
    private int Team2Score;
    private int PlayerScore;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    internal void ActivateServePanel()
    {
        ServePanel.SetActive(true);
    }

    internal void DeactivateServePanel()
    {
        ServePanel.SetActive(false);
    }

    internal void PlayerScoreUpdated(int newScore)
    {
        PlayerScore = newScore;
        ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
    }

    internal void UpdateTeam1Score(int newScore)
    {
        Team1Score = newScore;
        ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
    }

    internal void UpdateTeam2Score(int newScore)
    {
        Team2Score = newScore;
        ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
    }
}
