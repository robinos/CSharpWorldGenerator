using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace WorldGenerator.Models
{
    public enum TerrainType
    {
        empty = 0, ocean = 1, water = 2, plains = 3, hill = 4, mountain = 5, light = 6, darkness = 7
    }

    public enum TerrainSubType
    {
        empty = 0, sea = 1, lake = 2, plains = 3, forest = 4, desert = 5, tundra = 6, verdantforest = 7, snowforest = 8, snowdesert = 9, ice = 10, hill = 11, dryhill = 12, snowhill = 13, mountain = 14, earthmountain = 15, snowmountain = 16,
        light = 17, airplains = 18, brightlight = 19, glowforest = 20, glowplains = 21, waterplains = 22, darkness = 23, deepdarkness = 24, firehill = 25, fireplains = 26, toxic = 27, wasteland = 28 
    }

    /// <summary>
    /// A definition for a world map terrain tile
    /// </summary>
    public class Terrain
    {
        //private int[,] _HeightMapArrays;
        public int Row { get; private set; }
        public int Column { get; private set; }
        public float Height { get; set; }
        public TerrainType TerrainType { get; set; }
        public TerrainSubType TerrainSubType { get; set; }
        public Image Image { get; set; }
        public float[] Temperature { get; set; }
        public float[] Pressure { get; set; }
        public Vector[] Wind { get; set; }
        public float[] Rainfall { get; set; }
        public Terrain WestNeighbour { get; set; }
        public Terrain EastNeighbour { get; set; }
        public Terrain NorthNeighbour { get; set; }
        public Terrain SouthNeighbour { get; set; }
        public Terrain NorthwestNeighbour { get; set; }
        public Terrain NortheastNeighbour { get; set; }
        public Terrain SouthwestNeighbour { get; set; }
        public Terrain SoutheastNeighbour { get; set; }
        public int NearbyLandValue { get; set; }
        public bool HasRiver { get; set; }
        public string RiverName { get; set; }
        public Vector.Direction RiverEntrance { get; set; }
        public Vector.Direction RiverExit { get; set; }
        public string Picture { get; set; }

        public Terrain(int row, int column, float height, TerrainType terrainType, TerrainSubType terrainSubtype, int NumberOfSeasons, Image image)
        {
            Row = row;
            Column = column;
            Height = height;
            TerrainType = terrainType;
            TerrainSubType = terrainSubtype;
            if (image != null) Image = image;
            Temperature = new float[NumberOfSeasons];
            Pressure = new float[NumberOfSeasons];
            Wind = new Vector[NumberOfSeasons];
            Rainfall = new float[NumberOfSeasons];
            NearbyLandValue = 0;
            WestNeighbour = null;
            EastNeighbour = null;
            NorthNeighbour = null;
            SouthNeighbour = null;
            NorthwestNeighbour = null;
            NortheastNeighbour = null;
            SouthwestNeighbour = null;
            SoutheastNeighbour = null;
            HasRiver = false;
            RiverName = "";
            RiverEntrance = Vector.Direction.none;
            RiverExit = Vector.Direction.none;
        }

        public void ChangeSubtypeAndImage(TerrainSubType terrainSubtype, Image image)
        {
            TerrainSubType = (TerrainSubType)terrainSubtype;
            if (image != null) Image = image;
        }

        public void ChangeTypeAndImage(TerrainType terrainType, Image image)
        {
            TerrainType = (TerrainType)terrainType;
            if (image != null) Image = image;
        }

        public int CalculateCellFromCoordinates(int maxIndex)
        {
            int cell;

            if (Row == 0) cell = Column;
            else if (Column == 0)
            {
                cell = Row * maxIndex;
            }
            else //neither row nor column are 0
            {
                cell = (Row + (Row * maxIndex)) + Column;
            }

            return cell;
        }
    }
}
