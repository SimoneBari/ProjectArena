using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Logging;
using Managers;
using Managers.Map;
using Managers.Mode;
using Maps.Genomes;
using Maps.MapAssembler;
using Maps.MapGenerator;
using Newtonsoft.Json;
using UnityEngine;

namespace Tester
{
    /// <summary>
    /// Deals with instantiating an experiment to test a specific map generated from a given genome.
    /// </summary>
    public class GenomeMapTester : MonoBehaviour
    {
        private const int DEFAULT_GAME_LENGTH = 240;

        private const int BOT1_ID = 1;
        private const int BOT2_ID = 2;

        [SerializeField] private GameObject botPrefab;
        [SerializeField] private string genomeName;
        [SerializeField] private string bot1ParamsFilenamePrefix;
        [SerializeField] private float bot1SkillLevel = 0.5f;
        [SerializeField] private string bot2ParamsFilenamePrefix;
        [SerializeField] private float bot2SkillLevel = 0.5f;
        [SerializeField] private SLMapManager mapManager;
        [SerializeField] private GenomeV2MapGenerator genomeMapGenerator;
        [SerializeField] private MapAssembler mapAssembler;
        [SerializeField] private SpawnPointManager spawnPointManager;
        [SerializeField] private int numExperiments = 1;
        [SerializeField] private string folderName;
        [SerializeField] private string experimentName = "experiment";
        [SerializeField] private int gameLength = DEFAULT_GAME_LENGTH;
        [SerializeField] private bool saveMap ;
        [SerializeField] private bool logPositions;
        [SerializeField] private bool logKillDistances;
        [SerializeField] private bool logDeathPositions;

        private readonly List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
        private GameResultsAnalyzer analyzer;
        private PositionAnalyzer positionAnalyzer;
        private KillDistanceAnalyzer killDistanceAnalyzer;
        private DeathPositionAnalyzer deathPositionAnalyzer;
        private string botsPath;

        private int experimentNumber;
        private string importPath;

        private GenomeTesterGameManager manager;
        private string genomesPath;

        
        private void Awake()
        {
            if (Application.isBatchMode)
            {
                Time.captureFramerate = 24;
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            }

            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-bot1file=")) bot1ParamsFilenamePrefix = arg.Substring(10);
                if (arg.StartsWith("-bot1skill=")) bot1SkillLevel = float.Parse(arg.Substring(11));
                if (arg.StartsWith("-bot2file=")) bot2ParamsFilenamePrefix = arg.Substring(10);
                if (arg.StartsWith("-bot2skill=")) bot2SkillLevel = float.Parse(arg.Substring(11));
                if (arg.StartsWith("-numExperiments=")) numExperiments = int.Parse(arg.Substring(16));
                if (arg.StartsWith("-folderName=")) folderName = arg.Substring(12);
                if (arg.StartsWith("-experimentName=")) experimentName = arg.Substring(16);
                if (arg.StartsWith("-genomeName=")) genomeName = arg.Substring(12);
                if (arg.StartsWith("-gameLength=")) gameLength = int.Parse(arg.Substring(12));
                if (arg.StartsWith("-saveMap")) saveMap = true;
                if (arg.StartsWith("-logPositions")) logPositions = true;
                if (arg.StartsWith("-logKillDistances")) logKillDistances = true;
                if (arg.StartsWith("-logDeathPositions")) logDeathPositions = true;
            }

            #if !UNITY_EDITOR
            var basePath = Directory.GetCurrentDirectory() + "/Data";
            #else
            var basePath = Application.persistentDataPath;
            #endif
            
            importPath = basePath + "/Import/";
            genomesPath = importPath + "Genomes/";
            botsPath = importPath + "Bots/";

            if (!Directory.Exists(genomesPath)) Directory.CreateDirectory(genomesPath);
            if (!Directory.Exists(botsPath)) Directory.CreateDirectory(botsPath);

            analyzer = new GameResultsAnalyzer(BOT1_ID, BOT2_ID);
            analyzer.Setup();
            if (logPositions)
            {
                positionAnalyzer = new PositionAnalyzer(BOT1_ID, BOT2_ID);
                positionAnalyzer.Setup();
            }

            if (logKillDistances)
            {
                killDistanceAnalyzer = new KillDistanceAnalyzer(BOT1_ID, BOT2_ID);
                killDistanceAnalyzer.Setup();
            }

            if (logDeathPositions)
            {
                deathPositionAnalyzer = new DeathPositionAnalyzer(BOT1_ID, BOT2_ID);
                deathPositionAnalyzer.Setup();
            }            

            StartNewExperimentGameEvent.Instance.AddListener(NewExperimentStarted);
            ExperimentEndedGameEvent.Instance.AddListener(ExperimentEnded);

            if (saveMap)
            {
                SaveMapTextGameEvent.Instance.AddListener(SaveMap);
            }

            StartNewExperiment();

            // Register start and end experiment events, so that I can finalize the analyzer and
            // reset it
        }
        
        private void SaveMap(string map)
        {
            ExportResults(map, folderName + "map_" + experimentName, ".txt");
            SaveMapTextGameEvent.Instance.RemoveListener(SaveMap);
        }

        private void StartNewExperiment()
        {
            analyzer.Reset();
            
            var bot1JsonParams = ReadFromFile(botsPath + bot1ParamsFilenamePrefix + "params.json",
                new JSonBotCharacteristics());
            var bot1Params = new BotCharacteristics(bot1SkillLevel, bot1JsonParams);
            
            var bot2JsonParams = ReadFromFile(botsPath + bot2ParamsFilenamePrefix + "params.json",
                new JSonBotCharacteristics());
            var bot2Params = new BotCharacteristics(bot2SkillLevel, bot2JsonParams);

            var defaultGuns = new[] {true, true, true, true};
            var activeGunsBot1 = ReadFromFile(botsPath + bot1ParamsFilenamePrefix + "guns.json", defaultGuns);
            var activeGunsBot2 = ReadFromFile(botsPath + bot2ParamsFilenamePrefix + "guns.json", defaultGuns);

            var genome = ReadFromFile(genomesPath + folderName + genomeName, GraphGenomeV2.Default);
            if (!genome.IsGenomeValid())
            {
                Debug.LogError("Genome read from file is invalid!");
                genome = GraphGenomeV2.Default;
            }
            genomeMapGenerator.SetGenome(genome);
            mapAssembler.SetMapScale(genome.mapScale);
            
            manager = gameObject.AddComponent<GenomeTesterGameManager>();
            manager.SetParameters(
                botPrefab,
                BOT1_ID,
                bot1Params,
                activeGunsBot1,
                BOT2_ID,
                bot2Params,
                activeGunsBot2,
                mapManager,
                spawnPointManager,
                gameLength,
                respawnDuration: 3.0f
            );
        }

        private void ExperimentEnded()
        {
            Debug.Log("Experiment num " + +experimentNumber + " ended!");

            manager.StopGame();
            Destroy(manager);
            
            // TODO provide correct length 
            results.Add(analyzer.CompileResults(gameLength));

            experimentNumber++;

            if (experimentNumber >= numExperiments)
            {
                if (logKillDistances)
                {
                    var (positions1, positions2) = killDistanceAnalyzer.CompileResultsAsCSV();
                    ExportResults(positions1, folderName + "kill_distances_" + experimentName + "_bot1", ".csv");
                    ExportResults(positions2, folderName + "kill_distances_" + experimentName + "_bot2", ".csv");
                }
                if (logDeathPositions)
                {
                    var (positions1, positions2) = deathPositionAnalyzer.CompileResultsAsCSV();
                    ExportResults(positions1, folderName + "death_positions_" + experimentName + "_bot1", ".csv");
                    ExportResults(positions2, folderName + "death_positions_" + experimentName + "_bot2", ".csv");
                }

                if (logPositions)
                {
                    var (positions1, positions2) = positionAnalyzer.CompileResultsAsCSV();
                    ExportResults(positions1, folderName + "position_" + experimentName + "_bot1", ".csv");
                    ExportResults(positions2, folderName + "position_" + experimentName + "_bot2", ".csv");
                }

                ExportResults(JsonConvert.SerializeObject(results), folderName + "final_results_" + experimentName);
                Application.Quit();
            }
            else
            {
                mapManager.ResetMap();
                spawnPointManager.Reset();

                StartCoroutine(WaitAndStart());
            }
        }

        private static void ExportResults(string compileResults, string experimentName, string extension = ".json")
        {

            #if UNITY_EDITOR
            var exportPath = Application.persistentDataPath + "Export/" + experimentName;
            #else
            var exportPath = Directory.GetCurrentDirectory() + "/Data/Export/" + experimentName;
            #endif
            
            var filePath = exportPath + extension;
            try
            {
                Debug.Log("Writing to file " + filePath);
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
            yield return new WaitForSeconds(0.2f);
            StartNewExperiment();
        }

        private void NewExperimentStarted()
        {
            Debug.Log("Experiment num " + experimentNumber + " started!");
        }

        private static T ReadFromFile<T>(string filePath, T defaultValue)
        {
            try
            {
                Debug.Log("Reading from " + filePath);
                using var reader = new StreamReader(filePath);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                Debug.LogError("Failed to load " + filePath);
                try
                {
                    var json = JsonConvert.SerializeObject(defaultValue, Formatting.Indented);
                    using var writer = new StreamWriter(filePath);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            return defaultValue;
        }

        private static Tuple<BotCharacteristics, bool[]> LoadBotCharacteristics(string botsPath, string botFilename)
        {
            var paramsFile = botsPath + botFilename + "params.json";
            var botParams = BotCharacteristics.Default;
            try
            {
                using var reader = new StreamReader(paramsFile);
                botParams = JsonConvert.DeserializeObject<BotCharacteristics>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(botParams, Formatting.Indented);
                    using var writer = new StreamWriter(paramsFile);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            var gunsFile = botsPath + botFilename + "guns.json";
            var guns = new[] {true, true, true, true};
            try
            {
                using var reader = new StreamReader(gunsFile);
                guns = JsonConvert.DeserializeObject<bool[]>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(guns);
                    using var writer = new StreamWriter(gunsFile);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            return new Tuple<BotCharacteristics, bool[]>(botParams, guns);
        }
    }
}
