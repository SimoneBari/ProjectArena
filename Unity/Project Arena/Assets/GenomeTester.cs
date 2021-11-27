using System.Collections;
using System.Collections.Generic;
using AssemblyGraph;
using AssemblyMaps;
using UnityEngine;
using Random = System.Random;

public class GenomeTester : MonoBehaviour
{
    [SerializeField] private int startingSeed;
    [SerializeField] private int numRows;
    [SerializeField] private int numColumns;
    [SerializeField] private int maxHeight;
    [SerializeField] private int maxWidth;
    [SerializeField] private int thickness;
    [SerializeField] private int rowSeparation;
    [SerializeField] private int colSeparation;

    void Update()
    {
        var random = new Random(startingSeed++);
        var genome = GenomeGenerator.Generate(numRows, numColumns, maxHeight, maxWidth, thickness, rowSeparation,
            colSeparation, random);
        var translator = new GenomeTranslatorV1(random);
        translator.TranslateGenome(genome, out var map, out var areas);
        var finalMap = MapUtils.GetStringFromCharMap(map);
    }
}