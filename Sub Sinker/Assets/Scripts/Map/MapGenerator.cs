﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;

public class MapGenerator : NetworkBehaviour
{
    public static MapGenerator instance;

    [Range(0, 100)]
    public int randomFillPercent;
    public int minRoomSize = 50;
    public int minChunkSize = 50;
    public int passageSize = 2;
    public bool debugLines = false;
    int[,] map;

    List<Coord> spawnableCoords;

    public static bool generated = false;

    void Start()
    {
        if (instance != null)
        {
            Debug.LogError("More than one MapGenerator in scene.");
        }
        else
        {
            instance = this;
        }

        spawnableCoords = new List<Coord>();
        GenerateMap(ServerManager.instance.tempSeed);
    }

    // while this does properly generate maps, the x and y vars are reversed
    // since 2d arrays are [y, x] aka [rows, columns]
    // therefore width is used as height and vice versa
    public void GenerateMap(string seed)
    {
        generated = false;

        map = new int[ServerManager.instance.mapWidth, ServerManager.instance.mapHeight];
        RandomFillMap(seed);

        for (int i = 0; i < 5; i++)
        {
            SmoothMap();
        }

        ProcessMap();

        int borderSize = 1;
        int[,] borderedMap = new int[ServerManager.instance.mapWidth + borderSize * 2, ServerManager.instance.mapHeight + borderSize * 2];

        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < ServerManager.instance.mapWidth + borderSize && y >= borderSize && y < ServerManager.instance.mapHeight + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1);

        List<List<Coord>> roomRegions = GetRegions(0);
        foreach (List<Coord> roomRegion in roomRegions)
        {
            foreach (Coord tile in roomRegion)
            {
                spawnableCoords.Add(tile);
	        }
        }

        // generating is done
        generated = true;


        // lazy
        GameObject waterBG = GameObject.Find("WaterBackground");
        GameObject darkBG = GameObject.Find("DarkBackground");

        // set size to cover map
        // note: REVERSED because the function uses width as height and vice versa
        waterBG.transform.localScale = new Vector3(ServerManager.instance.mapHeight * 3f / 10f, 1,
            ServerManager.instance.mapWidth * 3f / 10f);
        darkBG.transform.localScale = new Vector3(ServerManager.instance.mapHeight * 3f / 10f + 15, 1,
            ServerManager.instance.mapWidth * 3f / 10f + 15);
        ServerManager.instance.mapGenPrefab.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.mainTextureScale = new Vector2(ServerManager.instance.mapWidth / 1.5f, ServerManager.instance.mapHeight / 1.5f); // Change Tiling settings of the map
    }

    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);

        foreach (List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < minChunkSize)
            {
                foreach (Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(0);
        List<Room> survivingRooms = new List<Room>();

        foreach (List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < minRoomSize)
            {
                foreach (Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = 1;
                }
            }
            else
            {
                survivingRooms.Add(new Room(roomRegion, map));
            }
        }
        survivingRooms.Sort();
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessibleFromMainRoom = true;

        ConnectClosestRooms(survivingRooms);
    }

    void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false)
    {

        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (forceAccessibilityFromMainRoom)
        {
            foreach (Room room in allRooms)
            {
                if (room.isAccessibleFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0)
                {
                    continue;
                }
            }

            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB))
                {
                    continue;
                }

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }
            if (possibleConnectionFound && !forceAccessibilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosestRooms(allRooms, true);
        }

        if (!forceAccessibilityFromMainRoom)
        {
            ConnectClosestRooms(allRooms, true);
        }
    }

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        if(debugLines) Debug.DrawLine(CoordToWorldPoint(tileA)*3, CoordToWorldPoint(tileB)*3, Color.green, 100);

        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord c in line)
        {
            DrawCircle(c, passageSize);
        }
    }

    void DrawCircle(Coord c, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if (IsInMapRange(drawX, drawY))
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
            {
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 CoordToWorldPoint(Coord tile)
    {
        Vector3 vector = new Vector3(-ServerManager.instance.mapWidth / 2 + .5f + tile.tileX, 2, -ServerManager.instance.mapHeight / 2 + .5f + tile.tileY);
        return Quaternion.Euler(270, 0, 0) * vector;
    }

    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[ServerManager.instance.mapWidth, ServerManager.instance.mapHeight];

        for (int x = 0; x < ServerManager.instance.mapWidth; x++)
        {
            for (int y = 0; y < ServerManager.instance.mapHeight; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[ServerManager.instance.mapWidth, ServerManager.instance.mapHeight];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < ServerManager.instance.mapWidth && y >= 0 && y < ServerManager.instance.mapHeight;
    }


    void RandomFillMap(string seed)
    {
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < ServerManager.instance.mapWidth; x++)
        {
            for (int y = 0; y < ServerManager.instance.mapHeight; y++)
            {
                if (x == 0 || x == ServerManager.instance.mapWidth - 1 || y == 0 || y == ServerManager.instance.mapHeight - 1)
                {
                    map[x, y] = 1; // Makes sure it doesn't have a blank spot at the edge
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0; // Random fill using a random number generator
                }
            }
        }
    }

    void SmoothMap()
    {
        for (int x = 0; x < ServerManager.instance.mapWidth; x++)
        {
            for (int y = 0; y < ServerManager.instance.mapHeight; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if (neighbourWallTiles > 4)
                    map[x, y] = 1;
                else if (neighbourWallTiles < 4)
                    map[x, y] = 0;

            }
        }
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }

        return wallCount;
    }

    struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }


    class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room()
        {
        }

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Coord>();
            foreach (Coord tile in tiles)
            {
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                {
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                    {
                        if (x == tile.tileX || y == tile.tileY)
                        {
                            if (map[x, y] == 1)
                            {
                                edgeTiles.Add(tile);
                            }
                        }
                    }
                }
            }
        }

        public void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms)
                {
                    connectedRoom.SetAccessibleFromMainRoom();
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.isAccessibleFromMainRoom)
            {
                roomB.SetAccessibleFromMainRoom();
            }
            else if (roomB.isAccessibleFromMainRoom)
            {
                roomA.SetAccessibleFromMainRoom();
            }
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    public Vector3 GetSpawnPos()
    {
        int tileset_width = map.GetLength(0);
        int tileset_height = map.GetLength(1);

        // exclude edges
        int map_x = UnityEngine.Random.Range(1, tileset_width - 1);
        int map_y = UnityEngine.Random.Range(1, tileset_height - 1);

        // sometimes this check makes it go out of bounds
        while (map[map_x - 1, map_y + 1] == 1 || map[map_x, map_y + 1] == 1 || map[map_x + 1, map_y + 1] == 1 ||
            map[map_x - 1, map_y] == 1 || map[map_x, map_y] == 1 || map[map_x + 1, map_y] == 1 ||
            map[map_x - 1, map_y - 1] == 1 || map[map_x, map_y - 1] == 1 || map[map_x + 1, map_y - 1] == 1) // BREAK when all conditions are not 1, i.e. open space
        {
            map_x = UnityEngine.Random.Range(1, tileset_width - 1);
            map_y = UnityEngine.Random.Range(1, tileset_height - 1);
        }
        float x = 3*(map_x - ServerManager.instance.mapWidth / 2); // 3x scale
        float y = 3*(map_y - ServerManager.instance.mapHeight / 2);

        return new Vector2(x, y);
    }

    public Vector3 GetGroundSpawnPos()
    {
        int tileset_width = map.GetLength(0);
        int tileset_height = map.GetLength(1);

        // exclude edges
        int map_x = UnityEngine.Random.Range(1, tileset_width - 1);
        int map_y = UnityEngine.Random.Range(1, tileset_height - 1);

            // sometimes this check makes it go out of bounds
            while (map[map_x - 1, map_y + 1] == 1 || map[map_x, map_y + 1] == 1 || map[map_x + 1, map_y + 1] == 1 ||
                map[map_x - 1, map_y] == 1 || map[map_x, map_y] == 1 || map[map_x + 1, map_y] == 1 ||
                map[map_x - 1, map_y - 1] == 0 || map[map_x, map_y - 1] == 0 || map[map_x + 1, map_y - 1] == 0) // BREAK when on "floor"
            {
                map_x = UnityEngine.Random.Range(1, tileset_width - 1);
                map_y = UnityEngine.Random.Range(1, tileset_height - 1);
            }
            float x = 3 * (map_x - ServerManager.instance.mapWidth / 2); // 3x scale
            float y = 3 * (map_y - ServerManager.instance.mapHeight / 2);

            return new Vector2(x, y);
    }
}