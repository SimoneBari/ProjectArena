using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AssemblyLogging;
using JsonObjects.Logging.Game;
using Newtonsoft.Json;
using UnityEngine;

namespace AssemblyTester
{
    public class GraphMapTester : MonoBehaviour
    {
        [SerializeField] private GameObject botPrefab;
        [SerializeField] private string mapPath;
        [SerializeField] private string bot1ParamsPath;
        [SerializeField] private string bot2ParamsPath;
        [SerializeField] private bool[] activeGunsBot1;
        [SerializeField] private bool[] activeGunsBot2;
        [SerializeField] private MapManager mapManager;
        [SerializeField] private SpawnPointManager spawnPointManager;
        [SerializeField] private int numExperiments = 1;
        [SerializeField] private string experimentName = "experiment";
        private const int GAME_LENGTH = 600;

        // Size of a maps tile.
        private const float TILE_SIZE = 1;

        private GraphTesterGameManager manager;
        private GameResultsAnalyzer analyzer;

        private int experimentNumber;

        private readonly List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

        private void Awake()
        {
            #if !UNITY_EDITOR
                Time.captureFramerate = 30;
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            #endif
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-bot1file=")) bot1ParamsPath = arg.Substring(10);
                if (arg.StartsWith("-bot2file=")) bot2ParamsPath = arg.Substring(10);
                if (arg.StartsWith("-numExperiments=")) numExperiments = int.Parse(arg.Substring(16));
                if (arg.StartsWith("-experimentName=")) experimentName = arg.Substring(16);
            }

            analyzer = new GameResultsAnalyzer();
            analyzer.Setup();

            StartNewExperimentGameEvent.Instance.AddListener(NewExperimentStarted);
            ExperimentEndedGameEvent.Instance.AddListener(ExperimentEnded);

            StartNewExperiment();

            // Register start and end experiment events, so that I can finalize the analyzer and
            // reset it
        }

        private void StartNewExperiment()
        {
            manager = gameObject.AddComponent<GraphTesterGameManager>();
            var bot1Params = LoadBotCharacteristics(bot1ParamsPath);
            var bot2Params = LoadBotCharacteristics(bot2ParamsPath);
            manager.SetParameters(
                botPrefab,
                bot1Params,
                activeGunsBot1,
                bot2Params,
                activeGunsBot2,
                mapPath,
                mapManager,
                spawnPointManager,
                GAME_LENGTH
            );
        }

        private void ExperimentEnded()
        {
            Debug.Log("Experiment num " + +experimentNumber + " ended!");

            manager.StopGame();
            Destroy(manager);

            // TODO provide correct length 
            results.Add(analyzer.CompileResults(GAME_LENGTH));

            experimentNumber++;

            analyzer.Reset();

            if (experimentNumber >= numExperiments)
            {
                ExportResults(JsonConvert.SerializeObject(results), experimentName);
                Application.Quit();
            }
            else
            {
                mapManager.ResetMap();
                spawnPointManager.Reset();

                StartCoroutine(WaitAndStart());
            }
        }

        private static void ExportResults(string compileResults, string experimentName)
        {
            var exportPath = Application.persistentDataPath + "/Export/" + experimentName;
            if (!Directory.Exists(exportPath)) 
            {
                Directory.CreateDirectory(exportPath);
            }

            var filePath = exportPath + "/" + "result.json";
            try
            {
                using var writer = new StreamWriter(filePath);
                writer.Write(compileResults);
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't export results!, error " + e.Message);
            }
        }

        private IEnumerator WaitAndStart()
        {
            yield return new WaitForSeconds(1);
            mapManager.ManageMap(true);
            StartNewExperiment();
        }

        private void NewExperimentStarted()
        {
            Debug.Log("Experiment num " + experimentNumber + " started!");
        }

        private static BotCharacteristics LoadBotCharacteristics(string botFilename)
        {
            var importPath = Application.persistentDataPath + "/Import";
            if (!Directory.Exists(importPath))
            {
                Directory.CreateDirectory(importPath);
            }

            var filePath = importPath + "/" + botFilename;
            try
            {
                using var reader = new StreamReader(filePath);
                var botParams = reader.ReadToEnd();
                return JsonUtility.FromJson<BotCharacteristics>(botParams);
            }
            catch (Exception)
            {
                var rtn = BotCharacteristics.Default;
                try
                {
                    var json = JsonUtility.ToJson(rtn, true);
                    using var writer = new StreamWriter(filePath);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }

                return rtn;
            }
        }
    }
}