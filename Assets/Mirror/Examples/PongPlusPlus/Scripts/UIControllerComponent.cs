using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIControllerComponent : MonoBehaviour
{
    internal static UIControllerComponent Instance { get; private set; }

    [SerializeField]
    private Text ScoreText;

    [SerializeField]
    private GameObject ServePanel;

    [SerializeField]
    private CanvasGroup ServePanelGroup;

    private bool ServePanelFadeActive = false;
    private float ServePanelFadeTimer;

    private int Team1Score;

    internal void PlayerScoreUpdated(int newScore)
    {
        PlayerScore = newScore;
        ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
    }

    private int Team2Score;
    private int PlayerScore;

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

    private void Update()
    {
        if (ServePanelFadeActive)
        {
            ServePanelFadeTimer -= Time.deltaTime;
            if (ServePanelFadeTimer <= 0)
            {
                ServePanel.SetActive(false);
                ServePanelFadeActive = false;
                return;
            }
            ServePanelGroup.alpha -= Time.deltaTime;
        }
    }

    internal void ActivateServePanel()
    {
        ServePanel.SetActive(true);
        ServePanelGroup.alpha = 1f;
    }

    internal void DeactivateServePanel()
    {
        ServePanelFadeTimer = 2f;
        ServePanelFadeActive = true;
    }
}
