﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class MLPrefabMapAssembler : PrefabMapAssembler {

    [SerializeField] private GameObject floorPrefab;

    // Maps.
    private List<char[,]> maps;
    // Char that denotes a void tile.
    private char voidChar;
    // Height of each level.
    private float levelHeight;
    // Comulative mask.
    private char[,] comulativeMask;
    // Mask composed by room chars only.
    private String roomMask;

    void Start() {
        SetReady(true);
    }

    public override void AssembleMap(List<char[,]> ms, char wChar, char rChar, char vChar, float squareSize, float h) {
        wallChar = wChar;
        roomChar = rChar;
        voidChar = vChar;
        levelHeight = h;
        width = ms[0].GetLength(0);
        height = ms[0].GetLength(1);
        maps = ms;

        InitializeComulativeMask();

        InitializeRoomMask();

        // Process all the tiles.
        ProcessTiles();

        for (int i = 0; i < maps.Count; i++) {
            UpdateComulativeMask(i);
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    if (maps[i][x, y] != wallChar && maps[i][x, y] != voidChar) {
                        string currentMask = GetComulativeNeighbourhoodMask(x, y);
                        foreach (ProcessedTilePrefab p in processedTilePrefabs) {
                            if (p.mask == currentMask) {
                                AddPrefab(p.prefab, x, y, squareSize, p.rotation, levelHeight * i);
                                AddWallRecursevely(i, x, y, squareSize, currentMask);
                                break;
                            }
                        }
                        AddPrefab(floorPrefab, x, y, squareSize, 0, levelHeight * i);
                    }
                }
            }
        }
    }

    // Initializes the room mask.
    private void InitializeRoomMask() {
        char[] c = new char[4];
        c[0] = roomChar;
        c[1] = roomChar;
        c[2] = roomChar;
        c[3] = roomChar;
        roomMask = new string(c);
    }

    private void InitializeComulativeMask() {
        comulativeMask = new char[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                comulativeMask[x, y] = wallChar;
    }

    private void UpdateComulativeMask(int currentLevel) {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (maps[currentLevel][x, y] != wallChar)
                    comulativeMask[x, y] = roomChar;
    }

    public override void AssembleMap(char[,] m, char wChar, char rChar, float squareSize, float prefabHeight) { }

    // Adds a wall recursevely.
    private void AddWallRecursevely(int currentLevel, int x, int y, float squareSize, String currentMask) {
        currentMask = GetMaskConjunction(currentMask, GetNeighbourhoodMask(currentLevel, x, y));
        if (currentMask != roomMask) {
            ProcessedTilePrefab currentPrefab = GetPrefabFromMask(currentMask);
            AddPrefab(currentPrefab.prefab, x, y, squareSize, currentPrefab.rotation, levelHeight * currentLevel);
            if (currentLevel < maps.Count - 1)
                AddWallRecursevely(currentLevel + 1, x, y, squareSize, currentMask);
        }
    }

    // Gets the coumlative neighbours of a cell as a mask.
    private string GetComulativeNeighbourhoodMask(int gridX, int gridY) {
        char[] mask = new char[4];
        mask[0] = comulativeMask[gridX, gridY + 1];
        mask[1] = comulativeMask[gridX + 1, gridY];
        mask[2] = comulativeMask[gridX, gridY - 1];
        mask[3] = comulativeMask[gridX - 1, gridY];
        return new string(mask);
    }

    // Gets the neighbours of a cell as a mask.
    private string GetNeighbourhoodMask(int level, int gridX, int gridY) {
        char[] mask = new char[4];
        mask[0] = GetTileChar(level, gridX, gridY + 1);
        mask[1] = GetTileChar(level, gridX + 1, gridY);
        mask[2] = GetTileChar(level, gridX, gridY - 1);
        mask[3] = GetTileChar(level, gridX - 1, gridY);
        return new string(mask);
    }

    // Returns the conjunction of two masks.
    private String GetMaskConjunction(string mask1, string mask2) {
        char[] mask = new char[4];
        mask[0] = (mask1[0] == roomChar || mask2[0] == roomChar) ? roomChar : wallChar;
        mask[1] = (mask1[1] == roomChar || mask2[1] == roomChar) ? roomChar : wallChar;
        mask[2] = (mask1[2] == roomChar || mask2[2] == roomChar) ? roomChar : wallChar;
        mask[3] = (mask1[3] == roomChar || mask2[3] == roomChar) ? roomChar : wallChar;
        return new string(mask);
    }

    // Returns the char of a tile.
    private char GetTileChar(int level, int x, int y) {
        if (IsInMapRange(x, y))
            return maps[level][x, y] == wallChar ? wallChar : roomChar;
        else
            return wallChar;
    }

    // Tells if a mask is contained in another one.
    private bool IsMaskContained(string containerMask, string containedMask) {
        for (int i = 0; i < containerMask.Length; i++)
            if (containerMask[i] == wallChar)
                if (containedMask[i] == roomChar)
                    return false;
        return true;
    }

    // Returns a prefab given a mask.
    private ProcessedTilePrefab GetPrefabFromMask(String mask) {
        foreach (ProcessedTilePrefab p in processedTilePrefabs)
            if (p.mask == mask)
                return p;
        return null;
    }

}
