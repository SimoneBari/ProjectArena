﻿using System.Collections;
using Entity;
using UI;
using UnityEngine;

namespace Managers.Mode
{
    /// <summary>
    ///     TutorialGameManager is an implementation of GameManager. The tutorial game mode consists in
    ///     finding and destroying a single target.
    /// </summary>
    public class TutorialGameManager : GameManager
    {
        [Header("Contenders")] [SerializeField]
        private GameObject player;

        [SerializeField] private int totalHealthPlayer = 100;
        [SerializeField] private bool[] activeGunsPlayer;
        [SerializeField] private GameObject target;

        [Header("Tutorial variables")] [SerializeField]
        protected TutorialGameUIManager tutorialGameUIManagerScript;

        private float completionTime;

        private Player playerScript;
        private bool tutorialCompleted;

        private void Start()
        {
            /* #if UNITY_EDITOR
            UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        #endif */

            playerScript = player.GetComponent<Player>();

            tutorialGameUIManagerScript.Fade(0.7f, 1f, true, 0.5f);
        }

        private void Update()
        {
            if (!IsReady() && mapManagerScript.IsReady() && spawnPointManagerScript.IsReady()
                && tutorialGameUIManagerScript.IsReady())
            {
                // Generate the map.
                mapManagerScript.ManageMap(true);

                if (!generateOnly)
                {
                    // Set the spawn points.
                    spawnPointManagerScript.SetSpawnPoints(mapManagerScript.GetSpawnPoints());

                    // Spawn the player.
                    Spawn(player);

                    // Setup the contenders.
                    playerScript.SetupEntity(totalHealthPlayer, activeGunsPlayer, this, 1);

                    playerScript.LockCursor();
                    startTime = Time.time;
                }

                SetReady(true);
            }
            else if (IsReady() && !generateOnly)
            {
                ManageGame();
            }
        }

        protected override void ManageGame()
        {
            UpdateGamePhase();

            switch (gamePhase)
            {
                case 0:
                    // Update the countdown.
                    tutorialGameUIManagerScript.SetCountdown((int) (startTime + readyDuration - Time.time));
                    break;
                case 1:
                    // Pause or unpause if needed.
                    if (Input.GetKeyDown(KeyCode.Escape)) Pause();
                    break;
                case 2:
                    // Do nothing.
                    break;
            }
        }

        protected override void UpdateGamePhase()
        {
            var passedTime = (int) (Time.time - startTime);

            if (gamePhase == -1)
            {
                // Disable the player movement and interactions, activate the ready UI and set the 
                // phase.
                playerScript.SetInGame(false);
                tutorialGameUIManagerScript.ActivateReadyUI();
                gamePhase = 0;
            }
            else if (gamePhase == 0 && passedTime >= readyDuration)
            {
                // Enable the player movement and interactions, activate the fight UI and spawn the 
                // target.
                tutorialGameUIManagerScript.Fade(0.7f, 0f, false, 0.25f);
                spawnPointManagerScript.UpdateLastUsed();
                StartCoroutine(SpawnTarget());
                playerScript.SetInGame(true);
                tutorialGameUIManagerScript.ActivateFightUI();
                gamePhase = 1;
            }
            else if (gamePhase == 1 && tutorialCompleted)
            {
                // Disable the player movement and interactions, activate the score UI, set the winner 
                // and set the phase.
                playerScript.SetInGame(false);
                tutorialGameUIManagerScript.Fade(0.7f, 0, true, 0.5f);
                tutorialGameUIManagerScript.ActivateScoreUI();
                completionTime = Time.time;
                gamePhase = 2;
            }
            else if (gamePhase == 2 && Time.time >= completionTime + scoreDuration)
            {
                Quit();
                gamePhase = 3;
            }
        }

        // Spawns a target.
        private IEnumerator SpawnTarget()
        {
            yield return new WaitForSeconds(2f);

            var newTarget = Instantiate(target);
            newTarget.name = target.name;
            newTarget.transform.position = spawnPointManagerScript.GetSpawnPosition();
            newTarget.GetComponent<Entity.Entity>().SetupEntity(0, null, this, 0);
        }

        // Pauses and unpauses the game.
        public override void Pause()
        {
            if (!isPaused)
            {
                tutorialGameUIManagerScript.Fade(0f, 0.7f, false, 0.25f);
                player.GetComponent<PlayerUIManager>().SetPlayerUIVisible(false);
                playerScript.ShowGun(false);
                tutorialGameUIManagerScript.ActivatePauseUI(true);
                playerScript.EnableInput(false);
            }
            else
            {
                tutorialGameUIManagerScript.Fade(0f, 0.7f, true, 0.25f);
                player.GetComponent<PlayerUIManager>().SetPlayerUIVisible(true);
                playerScript.ShowGun(true);
                tutorialGameUIManagerScript.ActivatePauseUI(false);
                playerScript.EnableInput(true);
            }

            isPaused = !isPaused;

            StartCoroutine(FreezeTime(0.25f, isPaused));
        }

        public override void SetUIColor(Color c)
        {
            tutorialGameUIManagerScript.SetColorAll(c);
        }

        public override void AddScore(int i, int j)
        {
            tutorialCompleted = true;
        }

        public override void ManageEntityDeath(GameObject g, Entity.Entity e)
        {
        }
    }
}