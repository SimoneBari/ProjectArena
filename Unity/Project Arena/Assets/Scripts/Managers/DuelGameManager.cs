﻿using UnityEngine;

public class DuelGameManager : GameManager {

    [Header("Contenders")] [SerializeField] private GameObject player;
    [SerializeField] private GameObject opponent;
    [SerializeField] private string playerName = "Player 1";
    [SerializeField] private string opponentName = "Player 2";
    [SerializeField] private int totalHealthPlayer = 100;
    [SerializeField] private int totalHealthOpponent = 100;
    [SerializeField] private bool[] activeGunsPlayer;
    [SerializeField] private bool[] activeGunsOpponent;

    [Header("Duel variables")] [SerializeField] protected GameObject duelGameUIManager;

    private DuelGameUIManager duelGameUIManagerScript;

    private Player playerScript;
    private Opponent opponentScript;

    private int playerKillCount = 0;
    private int opponentKillCount = 0;

    private int playerID = 1;
    private int opponentID = 2;

    private void Start() {
        /* #if UNITY_EDITOR
            UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        #endif */

        mapManagerScript = mapManager.GetComponent<MapManager>();
        spawnPointManagerScript = spawnPointManager.GetComponent<SpawnPointManager>();
        duelGameUIManagerScript = duelGameUIManager.GetComponent<DuelGameUIManager>();

        playerScript = player.GetComponent<Player>();
        opponentScript = opponent.GetComponent<Opponent>();

        duelGameUIManagerScript.Fade(0.5f, 1f, true, 0.5f);
    }

    private void Update() {
        if (!IsReady() && mapManagerScript.IsReady() && spawnPointManagerScript.IsReady() && duelGameUIManagerScript.IsReady()) {
            // Generate the map.
            mapManagerScript.ManageMap(true);

            // Set the spawn points.
            if (!generateOnly)
                generateOnly = spawnPointManagerScript.SetSpawnPoints(mapManagerScript.GetSpawnPoints());

            if (!generateOnly) {
                // Spawn the player and the opponent.
                Spawn(player);
                Spawn(opponent);

                // Setup the contenders.
                playerScript.SetupEntity(totalHealthPlayer, activeGunsPlayer, this, playerID);
                opponentScript.SetupEntity(totalHealthOpponent, activeGunsOpponent, this, opponentID);

                playerScript.LockCursor();
                startTime = Time.time;
            }

            SetReady(true);
        } else if (IsReady() && !generateOnly) {
            ManageGame();
        }
    }

    // Updates the phase of the game.
    protected override void UpdateGamePhase() {
        int passedTime = (int)(Time.time - startTime);

        if (gamePhase == -1) {
            // Disable the contenders movement and interactions, activate the ready UI, set the name of the players and set the phase.
            playerScript.SetInGame(false);
            opponentScript.SetInGame(false);
            duelGameUIManagerScript.ActivateReadyUI();
            duelGameUIManagerScript.SetPlayersName(playerName, opponentName);
            duelGameUIManagerScript.SetReadyUI();
            gamePhase = 0;
        } else if (gamePhase == 0 && passedTime >= readyDuration) {
            // Enable the contenders movement and interactions, activate the figth UI, set the kills to zero and set the phase.
            duelGameUIManagerScript.Fade(0.5f, 0f, false, 0.25f);
            playerScript.SetInGame(true);
            opponentScript.SetInGame(true);
            duelGameUIManagerScript.SetKills(0, 0);
            duelGameUIManagerScript.ActivateFigthUI();
            gamePhase = 1;
        } else if (gamePhase == 1 && passedTime >= readyDuration + gameDuration) {
            // Disable the contenders movement and interactions, activate the score UI, set the winner and set the phase.
            playerScript.SetInGame(false);
            opponentScript.SetInGame(false);
            duelGameUIManagerScript.Fade(0.5f, 0, true, 0.5f);
            duelGameUIManagerScript.ActivateScoreUI();
            duelGameUIManagerScript.SetScoreUI(playerKillCount, opponentKillCount);
            gamePhase = 2;
        } else if (gamePhase == 2 && passedTime >= readyDuration + gameDuration + scoreDuration) {
            Quit();
        }
    }

    // Manages the gamed depending on the current time.
    protected override void ManageGame() {
        UpdateGamePhase();

        switch (gamePhase) {
            case 0:
                // Update the countdown.
                duelGameUIManagerScript.SetCountdown((int)(startTime + readyDuration - Time.time));
                break;
            case 1:
                // Update the time.
                duelGameUIManagerScript.SetTime((int)(startTime + readyDuration + gameDuration - Time.time));
                // Pause or unpause if needed.
                if (Input.GetKeyDown(KeyCode.Escape))
                    Pause();
                break;
            case 2:
                // Do nothing.
                break;
        }
    }

    // Adds a kill to the kill counters.
    public override void AddScore(int killerIdentifier, int killedID) {
        if (killerIdentifier == killedID) {
            if (killerIdentifier == playerID)
                playerKillCount--;
            else
                opponentKillCount--;
        } else {
            if (killerIdentifier == playerID)
                playerKillCount++;
            else
                opponentKillCount++;
        }

        duelGameUIManagerScript.SetKills(playerKillCount, opponentKillCount);
    }

    // Sets the color of the UI.
    public override void SetUIColor(Color c) {
        duelGameUIManagerScript.SetColorAll(c);
    }

    // Pauses and unpauses the game.
    public override void Pause() {
        if (!isPaused) {
            duelGameUIManagerScript.Fade(0f, 0.5f, false, 0.25f);
            duelGameUIManagerScript.ActivatePauseUI(true);
            playerScript.EnableInput(false);
        } else {
            duelGameUIManagerScript.Fade(0f, 0.5f, true, 0.25f);
            duelGameUIManagerScript.ActivatePauseUI(false);
            playerScript.EnableInput(true);
        }

        isPaused = !isPaused;
    }

    // Menages the death of an entity.
    public override void MenageEntityDeath(GameObject g, Entity e) {
        // Start the respawn process.
        StartCoroutine(WaitForRespawn(g, e));
    }

}