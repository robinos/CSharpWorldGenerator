using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorldGenerator.Models;
using System.Drawing;
using WorldGenerator.Repositories;
using System.Resources;
using System.IO;
using System.Reflection;

namespace WorldGenerator.Repositories
{
    public class WorldGeneratorRepository
    {
        private int _Size = 128;
        private int _MaxIndex = 127;      // Map will go from 0..MAX_INDEX (included). So number of cells 
                                          // will be MAX_INDEX+1. Must be 2^n to ensure midpoint at each iteration.
        private int _HighRange = 32;      // max high difference will be this value * random value
        private Random _Random = new Random();    // generic random object
        private float _ActualHighest = float.MaxValue;
        private float _ActualLowest = float.MinValue;
        private int _NumberOfSeasons = 4;
        private float _WaterLimit = 0;
        private float _DeepOceanLimit = 0;
        private float _MountainLimit = 0;
        private float _HillLimit = 0;
        private DisjointSets disjointSet;

        public WorldGeneratorRepository(int size)
        {
            _Size = size;
            _MaxIndex = size - 1;
            _HighRange = size / 8;
            disjointSet = new DisjointSets(_Size * _Size);
        }


        public Dictionary<KeyValuePair<int, int>, Terrain> GenerateWorld()
        {
            try
            {
                Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary = new Dictionary<KeyValuePair<int, int>, Terrain>(); 
                float[,] heightMapArrays = new float[_Size, _Size];                
                int octaveCount = _Size / _HighRange;

                float[][] noiseMap = GenerateNoiseArray(_Size, _Size, octaveCount);
                ConvertNoiseToHeightMap(heightMapArrays, noiseMap);
                ConvertHeightMapToInitialTerrainDictionary(heightMapArrays, terrainDictionary);
                ComputeTemperature(terrainDictionary, _NumberOfSeasons);
                ComputePressure(terrainDictionary, _NumberOfSeasons);
                ComputeWind(terrainDictionary, _NumberOfSeasons);
                ComputeRainfall(terrainDictionary, _NumberOfSeasons);
                ComputeRivers(terrainDictionary, _NumberOfSeasons);
                ComputeClimates(terrainDictionary, _NumberOfSeasons);

                //ComputeRainfall(terrainDictionary, _NumberOfSeasons);
                //ComputeRivers(terrainDictionary, _NumberOfSeasons);
                //ComputeClimates(terrainDictionary, _NumberOfSeasons);

                return terrainDictionary;
            }
            catch(Exception e)
            {
                throw e;
            }
        }


        private float[][] GenerateWhiteNoise(int width, int height)
        {
            float[][] noise = GetEmptyArray<float>(width, height);

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    noise[i][j] = (float)_Random.NextDouble() % 1;
                }
            }

            return noise;
        }


        private float Interpolate(float x0, float x1, float alpha)
        {
            return x0 * (1 - alpha) + alpha * x1;
        }


        private T[][] GetEmptyArray<T>(int width, int height)
        {
            T[][] image = new T[width][];

            for (int i = 0; i < width; i++)
            {
                image[i] = new T[height];
            }

            return image;
        }


        private float[][] GenerateSmoothNoise(float[][] baseNoise, int octave)
        {
            int width = baseNoise.Length;
            int height = baseNoise[0].Length;

            float[][] smoothNoise = GetEmptyArray<float>(width, height);

            int samplePeriod = 1 << octave; // calculates 2 ^ k
            float sampleFrequency = 1.0f / samplePeriod;

            for (int i = 0; i < width; i++)
            {
                //calculate the horizontal sampling indices
                int sample_i0 = (i / samplePeriod) * samplePeriod;
                int sample_i1 = (sample_i0 + samplePeriod) % width; //wrap around
                float horizontal_blend = (i - sample_i0) * sampleFrequency;

                for (int j = 0; j < height; j++)
                {
                    //calculate the vertical sampling indices
                    int sample_j0 = (j / samplePeriod) * samplePeriod;
                    int sample_j1 = (sample_j0 + samplePeriod) % height; //wrap around
                    float vertical_blend = (j - sample_j0) * sampleFrequency;

                    //blend the top two corners
                    float top = Interpolate(baseNoise[sample_i0][sample_j0],
                        baseNoise[sample_i1][sample_j0], horizontal_blend);

                    //blend the bottom two corners
                    float bottom = Interpolate(baseNoise[sample_i0][sample_j1],
                        baseNoise[sample_i1][sample_j1], horizontal_blend);

                    //final blend
                    smoothNoise[i][j] = Interpolate(top, bottom, vertical_blend);
                }
            }

            return smoothNoise;
        }


        private float[][] GenerateNoiseArray(float[][] baseNoise, int octaveCount)
        {
            int width = baseNoise.Length;
            int height = baseNoise[0].Length;

            float[][][] smoothNoise = new float[octaveCount][][]; //an array of 2D arrays containing

            //Value to tweak for continent formation
            float persistance = 0.95f;

            //generate smooth noise
            for (int i = 0; i < octaveCount; i++)
            {
                smoothNoise[i] = GenerateSmoothNoise(baseNoise, i);
            }

            float[][] noiseArray = GetEmptyArray<float>(width, height); //an array of floats initialised to 0

            float amplitude = 1.0f;
            float totalAmplitude = 0.0f;

            //blend noise together
            for (int octave = octaveCount - 1; octave >= 0; octave--)
            {
                amplitude *= persistance;
                totalAmplitude += amplitude;

                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        noiseArray[i][j] += smoothNoise[octave][i][j] * amplitude;
                    }
                }
            }

            //normalisation
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    noiseArray[i][j] /= totalAmplitude;
                }
            }

            return noiseArray;
        }


        private float[][] GenerateNoiseArray(int width, int height, int octaveCount)
        {
            float[][] baseNoise = GenerateWhiteNoise(width, height);

            return GenerateNoiseArray(baseNoise, octaveCount);
        }


        private void ConvertNoiseToHeightMap(float[,] heightMapArrays, float[][] noiseArray)
        {
            _ActualHighest = noiseArray[0][0] * 10f;
            _ActualLowest = noiseArray[0][0] * 10f;

            for (int column = 0; column <= _MaxIndex; column++)
            {
                for (int row = 0; row <= _MaxIndex; row++)
                {
                    float currentValue = noiseArray[row][column] * 10f;

                    if (currentValue < _ActualLowest)
                    {
                        _ActualLowest = currentValue;
                    }
                    if (currentValue > _ActualHighest)
                    {
                        _ActualHighest = currentValue;
                    }

                    heightMapArrays[row, column] = currentValue;
                }
            }

            float waterLimit = 0;
            float temp = _ActualHighest + _ActualLowest / 2.0f;

            if (_ActualHighest > Math.Abs(_ActualLowest))
            {
                if (temp > 0)
                {
                    waterLimit = temp / 2.0f;
                }
                else waterLimit = temp;
            }

            float deepOceanLimit = 0;
            if (waterLimit > 0) deepOceanLimit = waterLimit / 1.5f;
            else
            {
                deepOceanLimit = _ActualLowest + Math.Abs(waterLimit / 2.0f);
                if (deepOceanLimit > waterLimit) deepOceanLimit = _ActualLowest - 0.01f;
            }

            _WaterLimit = waterLimit;
            _DeepOceanLimit = deepOceanLimit;

            float mountainLimit = _ActualHighest - (_ActualHighest / 8.0f);
            float hillLimit = _ActualHighest - (_ActualHighest / 4.0f);
            if (hillLimit < waterLimit) hillLimit = waterLimit + 1f;
            if (hillLimit > mountainLimit) hillLimit = mountainLimit - 0.05f;

            _MountainLimit = mountainLimit;
            _HillLimit = hillLimit;

            for (int column = 0; column <= _MaxIndex; column++)
            {
                for (int row = 0; row <= _MaxIndex; row++)
                {
                    float height = heightMapArrays[row, column];
                    KeyValuePair<int, int> keyValuePair = new KeyValuePair<int, int>(row, column);

                    if (height <= _WaterLimit)
                    {
                        if (height <= _DeepOceanLimit)
                        {
                            float deeptemp = 0f;
                            if(_WaterLimit > 0)
                            {
                                deeptemp = 0f - _DeepOceanLimit;
                                deeptemp -= 2.5f;
                                if (deeptemp > (_HillLimit * -1.5f)) deeptemp = (_HillLimit * -1.5f);
                                height += deeptemp;
                            } 
                            else height -= 2.5f;

                            heightMapArrays[row, column] = height;
                            if (height < _ActualLowest) _ActualLowest = height;
                        }
                        else
                        {
                            float watertemp = 0f;
                            if (_WaterLimit > 0)
                            {
                                watertemp = 0f - _WaterLimit;
                                watertemp -= 1.5f;
                                height += watertemp;
                            }
                            else height -= 1.5f;
                            heightMapArrays[row, column] = height;
                            if (height < _ActualLowest) _ActualLowest = height;
                        }
                    }
                    else if (height >= _MountainLimit)
                    {
                        height += 2.5f;
                        heightMapArrays[row, column] = height;
                        if (height > _ActualHighest) _ActualHighest = height;
                    }
                    else if (height >= _HillLimit)
                    {
                        height += 1.5f;
                        heightMapArrays[row, column] = height;
                        if (height < _ActualHighest) _ActualHighest = height;
                    }
                    else
                    {
                        if(height > 0) height = height / 2;

                        heightMapArrays[row, column] = height;
                    }
                }
            }
        }


        private void ConvertHeightMapToInitialTerrainDictionary(float[,] heightMapArrays, Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary)
        {
            float waterLimit = 0;
            //float temp = _ActualHighest + _ActualLowest / 2.0f;

            //if (_ActualHighest > Math.Abs(_ActualLowest))
            //{
            //    if (temp > 0)
            //    {
            //        waterLimit = temp / 2.0f;
            //    }
            //    else waterLimit = temp;
            //}

            float deepOceanLimit = 0;
            //if (waterLimit > 0) deepOceanLimit = waterLimit / 1.5f;
            //else
            //{
                deepOceanLimit = _ActualLowest / 2f;
                //deepOceanLimit = _ActualLowest + Math.Abs(waterLimit / 2.0f);
                if (deepOceanLimit > waterLimit) deepOceanLimit = _ActualLowest + 0.5f;
            //}

            _WaterLimit = waterLimit;
            _DeepOceanLimit = deepOceanLimit;

            float mountainLimit = _ActualHighest - (_ActualHighest / 8.0f);
            float hillLimit = _ActualHighest - (_ActualHighest / 4.0f);
            if (hillLimit < waterLimit) hillLimit = waterLimit + 1f;
            if (hillLimit > mountainLimit) hillLimit = mountainLimit - 0.05f;

            _MountainLimit = mountainLimit;
            _HillLimit = hillLimit;

            int oceans = 0;
            int waters = 0;
            int mountains = 0;
            int hills = 0;
            int plains = 0;

            if (terrainDictionary == null) terrainDictionary = new Dictionary<KeyValuePair<int, int>, Terrain>();

            for (int column = 0; column <= _MaxIndex; column++)
            {
                for (int row = 0; row <= _MaxIndex; row++)
                {
                    float height = heightMapArrays[row, column];
                    KeyValuePair<int, int> keyValuePair = new KeyValuePair<int, int>(row, column);

                    if (height <= _WaterLimit)
                    {
                        if(height <= _DeepOceanLimit)
                        {
                            Image image = global::WorldGenerator.Properties.Resources.ocean;
                            Terrain terrain = new Terrain(row, column, height, TerrainType.ocean, TerrainSubType.sea, _NumberOfSeasons, image);
                            terrain.Picture = "ocean";
                            AddToDictionary(terrainDictionary, keyValuePair, terrain);
                            oceans++;
                        }
                        else
                        {
                            Image image = global::WorldGenerator.Properties.Resources.water;
                            Terrain terrain = new Terrain(row, column, height, TerrainType.water, TerrainSubType.sea, _NumberOfSeasons, image);
                            terrain.Picture = "water";
                            AddToDictionary(terrainDictionary, keyValuePair, terrain);
                            waters++;
                        }
                    }
                    else if(height >= _MountainLimit)
                    {
                        Image image = global::WorldGenerator.Properties.Resources.mountain;
                        Terrain terrain = new Terrain(row, column, height, TerrainType.mountain, TerrainSubType.mountain, _NumberOfSeasons, image);
                        terrain.Picture = "mountain";
                        AddToDictionary(terrainDictionary, keyValuePair, terrain);
                        mountains++;
                    }
                    else if (height >= _HillLimit)
                    {
                        Image image = global::WorldGenerator.Properties.Resources.hill;
                        Terrain terrain = new Terrain(row, column, height, TerrainType.hill, TerrainSubType.hill, _NumberOfSeasons, image);
                        terrain.Picture = "hill";
                        AddToDictionary(terrainDictionary, keyValuePair, terrain);
                        hills++;
                    }
                    else
                    {
                        Image image = global::WorldGenerator.Properties.Resources.plains;
                        Terrain terrain = new Terrain(row, column, height, TerrainType.plains, TerrainSubType.plains, _NumberOfSeasons, image);
                        terrain.Picture = "plains";
                        AddToDictionary(terrainDictionary, keyValuePair, terrain);
                        plains++;
                    }
                }
            }
        }


        private void ComputeTemperature(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            //float MAX_TEMP = 340.0f; //331K is about 57,8 celcius, so this allows near-earth variation
            //float MIN_TEMP = 180.0f; //184K is about -89.2 celcius
            //float tmax = 180.0f, tmin = 340.0f, tmean; //For calculating actual max, min, mean         
            float fTemp;

            //Go through the terrain
            for (int season = 0; season < NumberOfSeasons; season++)
            {
                foreach (Terrain terrain in terrainDictionary.Values)
                {
                    fTemp = CalculateTemperature(terrainDictionary, terrain, season);
                    //-= 274; //273.15 Kelvin till celsius                
                    terrain.Temperature[season] = fTemp;
                }

                foreach (Terrain terrain in terrainDictionary.Values)
                {
                    fTemp = ScaleTemperature(terrainDictionary, terrain, season);              
                    terrain.Temperature[season] = fTemp;
                }
            }

            int frog = 3;
            frog++;
        }


        private float CalculateTemperature(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, int season)
        {
            float fTemp = 14.0f; //280.0f;  //287.2K is about 14 celcius

            //Get equatorial zone and polar zone
            int Half = _MaxIndex / 2;
            int Tenth = _MaxIndex / 10;

            int EquatorTop = Half - Tenth;
            int EquatorBottom = Half + Tenth;

            int PolarNorth = Tenth;
            int PolarSouth = _MaxIndex - Tenth;

            TerrainSubType subtype = terrain.TerrainSubType;

            float row = terrain.Row;
            float column = terrain.Column;
            float height = terrain.Height;

            AttachNearbyLand(terrainDictionary, terrain);

            int landSum = GetNearbyLandValue(terrainDictionary, terrain);
            terrain.NearbyLandValue = landSum;

            //Water
            if (subtype == TerrainSubType.sea || subtype == TerrainSubType.lake) fTemp += 5f;
            else fTemp -= 5f;

            //Poles and equator
            if (row < PolarNorth || row > PolarSouth) fTemp -= 20f;
            else if (row > EquatorTop && row < EquatorBottom) fTemp += 20f;

            //Land ratio    
            if (landSum > 75) fTemp -= 10f; //very mountainous
            else if (landSum > 55) fTemp -= 5f; //lots o land
            else if (landSum > 25) { } //land
            else fTemp += 5f; //water dominates

            //Seasons
            if (row <= Half)
            { //northern hemisphere
                if (landSum >= 40)
                {
                    if (season == 1) fTemp += 20f; //summer
                    else if (season == 3) //winter
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            fTemp -= 10f;
                        }
                        else
                        {
                            fTemp -= 20f;
                        }
                    }
                }
                else if (landSum >= 20f)
                {
                    if (season == 1) fTemp += 10f; //summer
                    else if (season == 3) //winter
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            fTemp -= 5f;
                        }
                        else
                        {
                            fTemp -= 10f;
                        }
                    }
                }
                else
                {
                    if (season == 1) fTemp += 5f; //summer
                    else if (season == 3) //winter
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            
                        }
                        else
                        {
                            fTemp -= 5f;
                        }
                    }
                }
            }
            else
            { //southern hemisphere
                if (landSum >= 40f)
                {
                    if (season == 3) fTemp += 20f; //winter
                    else if (season == 1) //summer
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            fTemp -= 10f;
                        }
                        else
                        {
                            fTemp -= 20f;
                        }
                    }
                }
                else if (landSum >= 20f)
                {
                    if (season == 3) fTemp += 10f; //winter
                    else if (season == 1) //summer
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            fTemp -= 5f;
                        }
                        else
                        {
                            fTemp -= 10f;
                        }
                    }
                }
                else
                {
                    if (season == 3) fTemp += 5f; //winter
                    else if (season == 1) //summer
                    {
                        if (row > EquatorTop && row < EquatorBottom)
                        {
                            
                        }
                        else
                        {
                            fTemp -= 5f;
                        }
                    }
                }
            }

            //Elevations
            if (height >= _ActualHighest) fTemp -= 10f;
            else if (height >= _ActualHighest / 4) fTemp -= 5f;
            else if (height <= _ActualLowest / 4) fTemp += 5f;

            if (subtype == TerrainSubType.sea || subtype == TerrainSubType.lake && fTemp > 30f)
            {
                fTemp -= 5f;
            }

            return fTemp;
        }

        private void AttachNearbyLand(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                Terrain westNeighbour = GetWestNeighbour(terrainDictionary, terrain);
                terrain.WestNeighbour = westNeighbour;
                terrain.EastNeighbour = GetEastNeighbour(terrainDictionary, terrain);
                terrain.NorthNeighbour = GetNorthNeighbour(terrainDictionary, terrain);
                terrain.SouthNeighbour = GetSouthNeighbour(terrainDictionary, terrain);
                terrain.NorthwestNeighbour = GetNorthwestNeighbour(terrainDictionary, terrain);
                terrain.NortheastNeighbour = GetNortheastNeighbour(terrainDictionary, terrain);
                terrain.SouthwestNeighbour = GetSouthwestNeighbour(terrainDictionary, terrain);
                terrain.SoutheastNeighbour = GetSoutheastNeighbour(terrainDictionary, terrain);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private int GetNearbyLandValue(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int sum = 0;
                int count = 0;

                if (terrain != null)
                {
                    sum += GetLandValue(terrain);
                    count++;

                    if (terrain.WestNeighbour != null) 
                    {
                        Terrain west = terrain.WestNeighbour;
                        sum += GetLandValue(west); 
                        count++; 
                    }
                    if (terrain.EastNeighbour != null) { sum += GetLandValue(terrain.EastNeighbour); count++; }
                    if (terrain.NorthNeighbour != null) { sum += GetLandValue(terrain.NorthNeighbour); count++; }
                    if (terrain.SouthNeighbour != null) { sum += GetLandValue(terrain.SouthNeighbour); count++; }
                    if (terrain.NorthwestNeighbour != null) { sum += GetLandValue(terrain.NorthwestNeighbour); count++; }
                    if (terrain.NortheastNeighbour != null) { sum += GetLandValue(terrain.NortheastNeighbour); count++; }
                    if (terrain.SouthwestNeighbour != null) { sum += GetLandValue(terrain.SouthwestNeighbour); count++; }
                    if (terrain.SoutheastNeighbour != null) { sum += GetLandValue(terrain.SoutheastNeighbour); count++; }

                    //sum /= count;
                }

                return sum;
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private int GetLandValue(Terrain terrain)
        {
            try
            {
                if (terrain != null)
                {
                    if (terrain.TerrainSubType == TerrainSubType.sea || terrain.TerrainSubType == TerrainSubType.lake) return 0;
                    else if (terrain.TerrainSubType == TerrainSubType.mountain) return 2;
                    else return 1;
                }
                else return 0;
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private float ScaleTemperature(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, int season)
        {
            try
            {
                float sum = 0f;
                int count = 0;

                if (terrain != null)
                {
                    if (terrain.Temperature != null)
                    {
                        sum += terrain.Temperature[season];
                        count++;

                        if (terrain.WestNeighbour != null) {
                            if (terrain.WestNeighbour.Temperature != null)
                            {
                                sum += terrain.WestNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.EastNeighbour != null) {
                            if (terrain.EastNeighbour.Temperature != null)
                            {
                                sum += terrain.EastNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.NorthNeighbour != null) {
                            if (terrain.NorthNeighbour.Temperature != null)
                            {
                                sum += terrain.NorthNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.SouthNeighbour != null) {
                            if (terrain.SouthNeighbour.Temperature != null)
                            {
                                sum += terrain.SouthNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.NorthwestNeighbour != null) {
                            if (terrain.NorthwestNeighbour.Temperature != null)
                            {
                                sum += terrain.NorthwestNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.NortheastNeighbour != null) {
                            if (terrain.NortheastNeighbour.Temperature != null)
                            {
                                sum += terrain.NortheastNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.SouthwestNeighbour != null) {
                            if (terrain.SouthwestNeighbour.Temperature != null)
                            {
                                sum += terrain.SouthwestNeighbour.Temperature[season];
                                count++;
                            }
                        }
                        if (terrain.SoutheastNeighbour != null) {
                            if (terrain.SoutheastNeighbour.Temperature != null)
                            {
                                sum += terrain.SoutheastNeighbour.Temperature[season];
                                count++;
                            }
                        }

                        sum /= count;
                    }
                }

                return sum;
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private void ComputePressure(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            try
            {
                //Get equatorial zone and polar zone
                int Half = _MaxIndex / 2;
                int Tenth = _MaxIndex / 10;

                int EquatorTop = Half - Tenth;
                int EquatorBottom = Half + Tenth;

                int PolarNorth = Tenth;
                int PolarSouth = _MaxIndex - Tenth;

                //Get desert zone 1 & 2
                int DesertZone1 = EquatorTop - Tenth;
                int DesertZone2 = EquatorBottom + Tenth;

                //Go through the terrain
                for (int season = 0; season < NumberOfSeasons; season++)
                {
                    foreach (Terrain terrain in terrainDictionary.Values)
                    {
                        int row = terrain.Row;

                        float height = terrain.Height;

                        if (height <= _WaterLimit)
                        {
                            height += _ActualHighest;
                        }
                        else
                        {
                            if (_ActualLowest < 0) height += _ActualLowest;
                            else height -= _ActualLowest;
                        }

                        //if(terrain.NearbyLandValue > 40)
                        //{
                        //    height -= _ActualLowest;
                        //}
                        //else if (terrain.NearbyLandValue > 20)
                        //{
                        //    height -= (_ActualLowest / 2);
                        //}

                        //Adjust high zones over the desert zones
                        if (row >= DesertZone1 && row < EquatorTop)
                        {
                            //if (height <= _WaterLimit)
                            //{
                                height += _ActualHighest;
                            //}
                        }
                        else if (row > EquatorBottom && row <= DesertZone2)
                        {
                            //if (height <= _WaterLimit)
                            //{
                                height += _ActualHighest;
                            //}
                        }


                        //Adjust high zones over the polar areas (mostly)
                        if (row >= PolarSouth || row <= PolarNorth)
                        {
                            //if (height < 0)
                            //{
                                height += _ActualHighest;
                            //}
                        }

                        //Adjust low zones over the equator (mostly)
                        if (row >= EquatorTop && row <= EquatorBottom)
                        {
                            //if (height > 0)
                            //{
                                if (_ActualLowest < 0) height += _ActualLowest;
                                else height -= _ActualLowest;
                            //}
                        }

                        if (season == 1)
                        { //add to lows in summer
                            height += (_ActualLowest / 4f);
                        }
                        else if (season == 3)
                        { //add to highs in winter
                            height += (_ActualHighest / 4f);
                        }

                        terrain.Pressure[season] = height * _Random.Next(1, 4);
                    }
                }
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private void ComputeWind(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            Vector windVector;

            //Go through the terrain
            for (int season = 0; season < NumberOfSeasons; season++)
            {
                foreach (Terrain terrain in terrainDictionary.Values)
                {
                    windVector = CalculateWind(terrainDictionary, terrain, season);
                    if (windVector != null)
                    {
                        terrain.Wind[season] = windVector;
                    }
                }
            }
        }


        private Vector CalculateWind(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, int season)
        {
            float startVelocity = 0;
            float velocity = 0; //largest difference
            Vector.Direction direction = Vector.Direction.still;
            Vector vector = null;

            try
            {
                if (terrain != null)
                {
                    startVelocity = terrain.Pressure[season];
                    velocity = startVelocity;

                    Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                    if (northwestNeighbour != null)
                    {
                        float temp = northwestNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.northwest;
                        }
                    }
                    Terrain northNeighbour = terrain.NorthNeighbour;
                    if (northNeighbour != null)
                    {
                        float temp = northNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.north;
                        }
                    }
                    Terrain northeastNeighbour = terrain.NortheastNeighbour;
                    if (northeastNeighbour != null)
                    {
                        float temp = northeastNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.northeast;
                        }
                    }
                    Terrain westNeighbour = terrain.WestNeighbour;
                    if (westNeighbour != null)
                    {
                        float temp = westNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.west;
                        }
                    }
                    Terrain eastNeighbour = terrain.EastNeighbour;
                    if (eastNeighbour != null)
                    {
                        float temp = eastNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.east;
                        }
                    }
                    Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                    if (southwestNeighbour != null)
                    {
                        float temp = southwestNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.southwest;
                        }
                    }
                    Terrain southNeighbour = terrain.SouthNeighbour;
                    if (southNeighbour != null)
                    {
                        float temp = southNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.south;
                        }
                    }
                    Terrain southeastNeighbour = terrain.SoutheastNeighbour;
                    if (southeastNeighbour != null)
                    {
                        float temp = southeastNeighbour.Pressure[season];
                        if (temp <= velocity)
                        {
                            velocity = temp;
                            direction = Vector.Direction.southeast;
                        }
                    }

                    float result = Math.Abs(startVelocity - velocity);

                    vector = new Vector();
                    vector.Velocity = result; //*2f
                    vector.VectorDirection = direction;
                }

                return vector;
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private void ComputeRainfall(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            //Get equatorial zone and polar zone
            int Half = _MaxIndex / 2;
            int Tenth = _MaxIndex / 10;

            int EquatorTop = Half - Tenth;
            int EquatorBottom = Half + Tenth;

            int PolarNorth = Tenth;
            int PolarSouth = _MaxIndex - Tenth;

            //Get desert zone 1 & 2
            int DesertZone1 = EquatorTop - Tenth;
            int DesertZone2 = EquatorBottom + Tenth;

            //Go through the terrain
            for (int season = 0; season < NumberOfSeasons; season++)
            {
                foreach (Terrain terrain in terrainDictionary.Values)
                {
                    float rain = terrain.Pressure[season];
                    rain = rain * (-1);  //reverse sign                

                    terrain.Rainfall[season] = rain;
                }
            }

            //Go through the terrain again! for rainfall with wind
            for (int season = 0; season < NumberOfSeasons; season++)
            {
                foreach (Terrain terrain in terrainDictionary.Values)
                {
                    CalculateRainfallWithWind(terrainDictionary, terrain, season);
                    int row = terrain.Row;

                    if (terrain.TerrainSubType == TerrainSubType.sea || terrain.TerrainSubType == TerrainSubType.lake)
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 100;
                        terrain.Rainfall[season] = rain;
                    }
                    //Just to re-adjust mountains for river making
                    if (terrain.TerrainSubType == TerrainSubType.mountain)
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 10;
                        terrain.Rainfall[season] = rain;
                    }

                    if (terrain.NearbyLandValue <= 20)
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 50;
                        terrain.Rainfall[season] = rain;
                    }
                    else if (terrain.NearbyLandValue <= 40)
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 25;
                        terrain.Rainfall[season] = rain;
                    }

                    if (row >= EquatorTop && row <= EquatorBottom)
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 25;
                        terrain.Rainfall[season] = rain;
                    }
                    if (row < EquatorTop && row >= DesertZone1)
                    {
                        float rain = terrain.Rainfall[season];
                        rain -= 20;
                        terrain.Rainfall[season] = rain;
                    }
                    if (row > EquatorBottom && row <= DesertZone2)
                    {
                        float rain = terrain.Rainfall[season];
                        rain -= 20;
                        terrain.Rainfall[season] = rain;
                    }
                    if (row <= PolarNorth || row >= PolarSouth)
                    {
                        float rain = terrain.Rainfall[season];
                        rain -= 20;
                        terrain.Rainfall[season] = rain;
                    }
                    if (season == 1 || season == 3)
                    { //summer or winter
                        float rain = terrain.Rainfall[season];
                        rain -= 20;
                        terrain.Rainfall[season] = rain;
                    }
                    else
                    {
                        float rain = terrain.Rainfall[season];
                        rain += 20;
                        terrain.Rainfall[season] = rain;
                    }

                }
            }
        }


        private void CalculateRainfallWithWind(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, int season)
        {
            if (terrain != null)
            {
                Vector windVector = terrain.Wind[season];
                float velocity = windVector.Velocity;

                if (velocity <= 0)
                {
                    return;
                }

                Vector.Direction direction = windVector.VectorDirection;

                if (direction == Vector.Direction.northwest)
                {
                    Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                    if (northwestNeighbour != null)
                    {
                        if (northwestNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = northwestNeighbour.Rainfall[season];
                            rain += velocity;
                            northwestNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.north)
                {
                    Terrain northNeighbour = terrain.NorthNeighbour;
                    if (northNeighbour != null)
                    {
                        if (northNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = northNeighbour.Rainfall[season];
                            rain += velocity;
                            northNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.northeast)
                {
                    Terrain northeastNeighbour = terrain.NortheastNeighbour;
                    if (northeastNeighbour != null)
                    {
                        if (northeastNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            /*
                            if(rain > 0) {
                                rain += velocity*2;
                            }
                            else {
                                rain += velocity;
                            }*/
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = northeastNeighbour.Rainfall[season];
                            rain += velocity;
                            northeastNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.west)
                {
                    Terrain westNeighbour = terrain.WestNeighbour;
                    if (westNeighbour != null)
                    {
                        if (westNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = westNeighbour.Rainfall[season];
                            rain += velocity;
                            westNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.east)
                {
                    Terrain eastNeighbour = terrain.EastNeighbour;
                    if (eastNeighbour != null)
                    {
                        if (eastNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = eastNeighbour.Rainfall[season];
                            rain += velocity;
                            eastNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.southwest)
                {
                    Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                    if (southwestNeighbour != null)
                    {
                        if (southwestNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = southwestNeighbour.Rainfall[season];
                            rain += velocity;
                            southwestNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.south)
                {
                    Terrain southNeighbour = terrain.SouthNeighbour;
                    if (southNeighbour != null)
                    {
                        if (southNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = southNeighbour.Rainfall[season];
                            rain += velocity;
                            southNeighbour.Rainfall[season] = rain;
                        }
                    }
                }
                if (direction == Vector.Direction.southeast)
                {
                    Terrain southeastNeighbour = terrain.SoutheastNeighbour;
                    if (southeastNeighbour != null)
                    {
                        if (southeastNeighbour.TerrainSubType == TerrainSubType.mountain)
                        {
                            float rain = terrain.Rainfall[season];
                            rain += velocity;
                            terrain.Rainfall[season] = rain;
                        }
                        else
                        {
                            float rain = southeastNeighbour.Rainfall[season];
                            rain += velocity;
                            southeastNeighbour.Rainfall[season] = rain;
                        }
                    }
                }

            }
            //End if terrain not null
        }

        //This was previously only used to turn 1-tile water surrounded by land into river ends
        //let's maybe find a more interesting use for it
        private void CalculateLakes(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary)
        {
            //Go through the terrain
            foreach (Terrain terrain in terrainDictionary.Values)
            {
                if (terrain.TerrainSubType == TerrainSubType.sea)
                {
                    if(terrain.NorthwestNeighbour != null && terrain.NorthwestNeighbour.TerrainSubType != TerrainSubType.sea)
                    {

                    }
                }
            }
        }


        /// <summary>
        /// For rivers
        /// </summary>
        /// <param name="randomNumber"></param>
        /// <param name="terrainDictionary"></param>
        /// <param name="riverSet"></param>
        /// <param name="terrain"></param>
        /// <returns></returns>
        private Terrain ValidDirection(int randomNumber, Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            if (terrain != null)
            {
                float height = terrain.Height;

                Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                Terrain northNeighbour = terrain.NorthNeighbour;
                Terrain northeastNeighbour = terrain.NortheastNeighbour;
                Terrain westNeighbour = terrain.WestNeighbour;
                Terrain eastNeighbour = terrain.EastNeighbour;
                Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                Terrain southNeighbour = terrain.SouthNeighbour;
                Terrain southeastNeighbour = terrain.SoutheastNeighbour;

                //Tests each direction by random number
                //1 = SW
                if (randomNumber == 0 && southwestNeighbour != null && UnionPossible(terrain, southwestNeighbour)
                    && (height >= southwestNeighbour.Height)
                    && (!(southwestNeighbour.HasRiver)) 
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return southwestNeighbour;
                }
                //2 = S
                else if (randomNumber == 1 && southNeighbour != null && UnionPossible(terrain, southNeighbour)
                    && (height >= southNeighbour.Height)
                    && (!(southNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return southNeighbour;
                }
                //3 = SE
                else if (randomNumber == 2 && southeastNeighbour != null && UnionPossible(terrain, southeastNeighbour)
                    && (height >= southeastNeighbour.Height)
                    && (!(southeastNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return southeastNeighbour;
                }
                //4 = W
                else if (randomNumber == 3 && westNeighbour != null && UnionPossible(terrain, westNeighbour)
                    && (height >= westNeighbour.Height)
                    && (!(westNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return westNeighbour;
                }
                //6 = E
                else if (randomNumber == 4 && eastNeighbour != null && UnionPossible(terrain, eastNeighbour)
                    && (height >= eastNeighbour.Height)
                    && (!(eastNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return eastNeighbour;
                }
                //7 = NW
                else if (randomNumber == 5 && northwestNeighbour != null && UnionPossible(terrain, northwestNeighbour)
                    && (height >= northwestNeighbour.Height)
                    && (!(northwestNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return northwestNeighbour;
                }
                //8 = N
                else if (randomNumber == 6 && northNeighbour != null && UnionPossible(terrain, northNeighbour)
                    && (height >= northNeighbour.Height)
                    && (!(northNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return northNeighbour;
                }
                //9 = NE
                else if (randomNumber == 7 && northeastNeighbour != null && UnionPossible(terrain, northeastNeighbour)
                    && (height >= northeastNeighbour.Height)
                    && (!(northeastNeighbour.HasRiver))
                    && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    return northeastNeighbour;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// For rivers
        /// </summary>
        /// <param name="terrainDictionary"></param>
        /// <param name="terrain"></param>
        /// <returns></returns>
        private Terrain HasValidExit(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            //Test NE = 9 = random 7
            Terrain neighbour = ValidDirection(7, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test E = 6 = random 4
            neighbour = ValidDirection(4, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test NW = 7 = random 5
            neighbour = ValidDirection(5, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test N = 8 = random 6          
            neighbour = ValidDirection(6, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test SE = 3 = random 2             
            neighbour = ValidDirection(2, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test SW = 1 = random 0              
            neighbour = ValidDirection(0, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test W = 4 = random 3        
            neighbour = ValidDirection(3, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;
            //Test S = 2 = random 1             
            neighbour = ValidDirection(1, terrainDictionary, terrain);
            if (neighbour != null) return neighbour;

            return null;
        }


        private bool UnionPossible(Terrain terrain, Terrain otherTerrain)
        {
            int here = terrain.CalculateCellFromCoordinates(_MaxIndex);
            int next = otherTerrain.CalculateCellFromCoordinates(_MaxIndex);
            int hereRoot = 0;
            int nextRoot = 0;

            //find the root of here       
            try
            {
                hereRoot = disjointSet.Find(here);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }

            //find the root of next        
            try
            {
                nextRoot = disjointSet.Find(next);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }

            //If they do not have the same root, return true
            if (hereRoot != nextRoot)
            {
                return true;
            }

            return false;
        }


        private void CalculateUnion(Terrain terrain, Terrain otherTerrain)
        {
            //Variables
            int here = terrain.CalculateCellFromCoordinates(_MaxIndex);
            int next = otherTerrain.CalculateCellFromCoordinates(_MaxIndex);
            int hereRoot = 0;
            int nextRoot = 0;

            //find the root of here
            try
            {
                hereRoot = disjointSet.Find(here);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }

            //find the root of next
            try
            {
                nextRoot = disjointSet.Find(next);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }

            //attempt union
            try
            {
                disjointSet.Union(nextRoot, hereRoot);
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private void CalculatePath(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, string riverName, int NumberOfSeasons)
        {
            HashSet<Terrain> riverSet = new HashSet<Terrain>();
            riverSet.Add(terrain);
            int factor = 1;

            if (terrain.TerrainSubType == TerrainSubType.sea || terrain.TerrainSubType == TerrainSubType.lake ||
                  terrain.TerrainSubType == TerrainSubType.ice || terrain == null)
            {
                return;
            }

            if (riverName == null || riverName == "")
            {
                int randomNumber = _Random.Next(0, 10);

                StringBuilder name = new StringBuilder();

                name.Append("The ");

                switch (randomNumber)
                {
                    case 0:
                        name.Append("windy ");
                        break;

                    case 1:
                        name.Append("grey ");
                        break;

                    case 2:
                        name.Append("gloomy ");
                        break;

                    case 3:
                        name.Append("ringing ");
                        break;

                    case 4:
                        name.Append("whistling ");
                        break;

                    case 5:
                        name.Append("rolling ");
                        break;

                    case 6:
                        name.Append("endless ");
                        break;

                    case 7:
                        name.Append("cursed ");
                        break;

                    case 8:
                        name.Append("wild ");
                        break;

                    case 9:
                        name.Append("eternal ");
                        break;

                    case 10:
                        name.Append("wandering ");
                        break;

                    default:
                        break;
                }

                randomNumber = _Random.Next(0, 10);

                switch (randomNumber)
                {
                    case 0:
                    case 1:
                    case 3:
                        name.Append("waters");
                        break;

                    case 4:
                    case 5:
                        name.Append("rapids");
                        break;

                    case 6:
                    case 7:
                        name.Append("torrent");
                        break;

                    default:
                        name.Append("river");
                        break;
                }
                riverName = name.ToString();

                terrain.RiverName = riverName;
            }

            while (terrain.TerrainSubType != TerrainSubType.lake && terrain.TerrainSubType != TerrainSubType.sea
                && terrain.TerrainSubType != TerrainSubType.ice && terrain.RiverExit != Vector.Direction.still)
            {
                float rain = terrain.Rainfall[0] + 20;
                terrain.Rainfall[0] = rain;
                rain = terrain.Rainfall[1] + 20;
                terrain.Rainfall[1] = rain;
                rain = terrain.Rainfall[2] + 20;
                terrain.Rainfall[2] = rain;
                rain = terrain.Rainfall[3] + 20;
                terrain.Rainfall[3] = rain;

                //recalculate rainfall with river changes
                for (int season = 0; season < NumberOfSeasons; season++)
                {
                    CalculateRainfallWithWind(terrainDictionary, terrain, season);
                }

                int randomNumber = _Random.Next(0, 7);
                Terrain neighbour = ValidDirection(randomNumber, terrainDictionary, terrain);

                //if(neighbour == null) {
                //    neighbour = hasValidExit( terrainMap, riverSet, terrain );
                //}

                int min = 0;
                int max = 7;

                //Find a random direction that is valid (if one exists)
                while (neighbour == null && HasValidExit(terrainDictionary, terrain) != null)
                {
                    if (min > max)
                    {
                        for(int i=0;i<=7;i++)
                        {
                            neighbour = ValidDirection(i, terrainDictionary, terrain);
                            if (neighbour != null) break;
                        }

                        if (neighbour == null) break;
                    }
                    else
                    {
                        randomNumber = _Random.Next(min, max);
                        neighbour = ValidDirection(randomNumber, terrainDictionary, terrain);

                        if (randomNumber >= max / 2) max--;
                        else min++;
                    }
                }

                if (neighbour != null)
                {
                    if (randomNumber == 0)
                    { //1 = SW = leftbelow
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.southwest;
                        neighbour.RiverEntrance = Vector.Direction.northeast;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 1)
                    { //2 = S = below
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.south;
                        neighbour.RiverEntrance = Vector.Direction.north;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 2)
                    { //3 = SE = rightBelow
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.southeast;
                        neighbour.RiverEntrance = Vector.Direction.northwest;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 3)
                    { //4 = W = left
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.west;
                        neighbour.RiverEntrance = Vector.Direction.east;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 4)
                    { //6 = E = right
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.east;
                        neighbour.RiverEntrance = Vector.Direction.west;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 5)
                    { //7 = NW = leftAbove
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.northwest;
                        neighbour.RiverEntrance = Vector.Direction.southeast;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 6)
                    { //8 = N = above
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.north;
                        neighbour.RiverEntrance = Vector.Direction.south;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else if (randomNumber == 7)
                    { //9 = NE = rightAbove
                        CalculateUnion(terrain, neighbour);
                        terrain.RiverExit = Vector.Direction.northeast;
                        neighbour.RiverEntrance = Vector.Direction.southwest;
                        riverSet.Add(neighbour);
                        terrain = neighbour;
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;
                    }
                    else
                    {
                        //Change image
                        CreateEndImageForRiverType(terrain);
                        terrain.HasRiver = true;
                        terrain.RiverName = riverName;

                        //also, add rain around it
                        for (int season = 0; season < NumberOfSeasons; season++)
                        {
                            AdjustRainfall(terrainDictionary, neighbour, NumberOfSeasons, factor);
                        }

                        terrain.RiverExit = Vector.Direction.still;

                        //break;
                    }
                }
                else
                {
                    //Change image
                    CreateEndImageForRiverType(terrain);
                    terrain.HasRiver = true;
                    terrain.RiverName = riverName;

                    //also, add rain around it
                    for (int season = 0; season < NumberOfSeasons; season++)
                    {
                        AdjustRainfall(terrainDictionary, neighbour, NumberOfSeasons, factor);
                    }

                    terrain.RiverExit = Vector.Direction.still;

                    //break;
                }

                //in either case, add rain around it
                for (int season = 0; season < NumberOfSeasons; season++)
                {
                    AdjustRainfall(terrainDictionary, neighbour, NumberOfSeasons, factor);
                }
            }
            //End while      

            //terrain.RiverExit = Vector.Direction.still;

            //foreach (Terrain temp in riverSet)
            //{
            //    if (temp.RiverName == "")
            //    {
            //        temp.RiverName = riverName;
            //    }
            //}
        }


        private void CreateEndImageForRiverType(Terrain terrain)
        {
            //Image image = null;

            if(terrain.TerrainSubType == TerrainSubType.plains)
            {
                //image = global::WorldGenerator.Properties.Resources.plainsend;
                terrain.Picture = "plainsend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.hill)
            {
                //image = global::WorldGenerator.Properties.Resources.hillend;
                terrain.Picture = "hillend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.mountain)
            {
                //image = global::WorldGenerator.Properties.Resources.mountainend;
                terrain.Picture = "mountainend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.desert)
            {
                //image = global::WorldGenerator.Properties.Resources.desertend;
                terrain.Picture = "desertend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.dryhill)
            {
                //image = global::WorldGenerator.Properties.Resources.dryhillend;
                terrain.Picture = "dryhillend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.earthmountain)
            {
                //image = global::WorldGenerator.Properties.Resources.earthmountainend;
                terrain.Picture = "earthmountainend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.forest)
            {
                //image = global::WorldGenerator.Properties.Resources.forestend;
                terrain.Picture = "forestend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.snowdesert)
            {
                //image = global::WorldGenerator.Properties.Resources.snowdesertend;
                terrain.Picture = "snowdesertend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.snowforest)
            {
                //image = global::WorldGenerator.Properties.Resources.snowforestend;
                terrain.Picture = "snowforestend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.snowhill)
            {
                //image = global::WorldGenerator.Properties.Resources.snowhillend;
                terrain.Picture = "snowhillend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.snowmountain)
            {
                //image = global::WorldGenerator.Properties.Resources.snowmountainend;
                terrain.Picture = "snowmountainend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.tundra)
            {
                //image = global::WorldGenerator.Properties.Resources.tundraend;
                terrain.Picture = "tundraend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.verdantforest)
            {
                //image = global::WorldGenerator.Properties.Resources.verdantforestend;
                terrain.Picture = "verdantforestend";
            }
            else if (terrain.TerrainSubType == TerrainSubType.waterplains)
            {
                //image = global::WorldGenerator.Properties.Resources.verdantforestend;
                terrain.Picture = "verdantforestend";
            }

            //return image;
        }

        /// <summary>
        /// For rivers
        /// </summary>
        /// <param name="terrainDictionary"></param>
        /// <param name="terrain"></param>
        /// <param name="NumberOfSeasons"></param>
        /// <param name="factor"></param>
        private void AdjustRainfall(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain, int NumberOfSeasons, int factor)
        {
            if (terrain != null)
            {
                for (int season = 0; season < NumberOfSeasons; season++)
                {

                    float rain = terrain.Rainfall[season] + factor * 2;
                    terrain.Rainfall[season] = rain;
                    if (terrain.Temperature[season] < 15) terrain.Temperature[season] += 5;
                    else if (terrain.Temperature[season] > 25) terrain.Temperature[season] -= 5;
                    else terrain.Temperature[season] += 1;

                    Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                    if (southwestNeighbour != null)
                    {
                        rain = southwestNeighbour.Rainfall[season] + factor;
                        southwestNeighbour.Rainfall[season] = rain;
                        if (southwestNeighbour.Temperature[season] < 10) southwestNeighbour.Temperature[season] += 1;
                        else if (southwestNeighbour.Temperature[season] > 30) southwestNeighbour.Temperature[season] -= 1;
                    }
                    Terrain westNeighbour = terrain.WestNeighbour;
                    if (westNeighbour != null)
                    {
                        rain = westNeighbour.Rainfall[season] + factor;
                        westNeighbour.Rainfall[season] = rain;
                        if (westNeighbour.Temperature[season] < 10) westNeighbour.Temperature[season] += 1;
                        else if (westNeighbour.Temperature[season] > 30) westNeighbour.Temperature[season] -= 1;
                    }
                    Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                    if (northwestNeighbour != null)
                    {
                        rain = northwestNeighbour.Rainfall[season] + factor;
                        northwestNeighbour.Rainfall[season] = rain;
                        if (northwestNeighbour.Temperature[season] < 10) northwestNeighbour.Temperature[season] += 1;
                        else if (northwestNeighbour.Temperature[season] > 30) northwestNeighbour.Temperature[season] -= 1;
                    }
                    Terrain northNeighbour = terrain.NorthNeighbour;
                    if (northNeighbour != null)
                    {
                        rain = northNeighbour.Rainfall[season] + factor;
                        northNeighbour.Rainfall[season] = rain;
                        if (northNeighbour.Temperature[season] < 10) northNeighbour.Temperature[season] += 1;
                        else if (northNeighbour.Temperature[season] > 30) northNeighbour.Temperature[season] -= 1;
                    }
                    Terrain northeastNeighbour = terrain.NortheastNeighbour;
                    if (northeastNeighbour != null)
                    {
                        rain = northeastNeighbour.Rainfall[season] + factor;
                        northeastNeighbour.Rainfall[season] = rain;
                        if (northeastNeighbour.Temperature[season] < 10) northeastNeighbour.Temperature[season] += 1;
                        else if (northeastNeighbour.Temperature[season] > 30) northeastNeighbour.Temperature[season] -= 1;
                    }
                    Terrain eastNeighbour = terrain.EastNeighbour;
                    if (eastNeighbour != null)
                    {
                        rain = eastNeighbour.Rainfall[season] + factor;
                        eastNeighbour.Rainfall[season] = rain;
                        if (eastNeighbour.Temperature[season] < 10) eastNeighbour.Temperature[season] += 1;
                        else if (eastNeighbour.Temperature[season] > 30) eastNeighbour.Temperature[season] -= 1;
                    }
                    Terrain southeastNeighbour = terrain.SoutheastNeighbour;
                    if (southeastNeighbour != null)
                    {
                        rain = southeastNeighbour.Rainfall[season] + factor;
                        southeastNeighbour.Rainfall[season] = rain;
                        if (southeastNeighbour.Temperature[season] < 10) southeastNeighbour.Temperature[season] += 1;
                        else if (southeastNeighbour.Temperature[season] > 30) southeastNeighbour.Temperature[season] -= 1;
                    }
                    Terrain southNeighbour = terrain.SouthNeighbour;
                    if (southNeighbour != null)
                    {
                        rain = southNeighbour.Rainfall[season] + factor;
                        southNeighbour.Rainfall[season] = rain;
                        if (southNeighbour.Temperature[season] < 10) southNeighbour.Temperature[season] += 1;
                        else if (southNeighbour.Temperature[season] > 30) southNeighbour.Temperature[season] -= 1;
                    }
                }
            }
        }


        private void ComputeRivers(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            bool valid = false;

            foreach (Terrain terrain in terrainDictionary.Values)
            {
                float avg = (terrain.Rainfall[0] + terrain.Rainfall[1] + terrain.Rainfall[2] + terrain.Rainfall[3]) / 4;

                Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                Terrain northNeighbour = terrain.NorthNeighbour;
                Terrain northeastNeighbour = terrain.NortheastNeighbour;
                Terrain westNeighbour = terrain.WestNeighbour;
                Terrain eastNeighbour = terrain.EastNeighbour;
                Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                Terrain southNeighbour = terrain.SouthNeighbour;
                Terrain southeastNeighbour = terrain.SoutheastNeighbour;

                if ((!(terrain.HasRiver)) && (!(terrain.TerrainSubType == TerrainSubType.sea)) && (!(terrain.TerrainSubType == TerrainSubType.lake))
                    && (!(terrain.TerrainSubType == TerrainSubType.ice)))
                {
                    if (!northwestNeighbour.HasRiver) valid = true;
                    if (!northNeighbour.HasRiver) valid = true;
                    if (!northeastNeighbour.HasRiver) valid = true;
                    if (!westNeighbour.HasRiver) valid = true;
                    if (!eastNeighbour.HasRiver) valid = true;
                    if (!southwestNeighbour.HasRiver) valid = true;
                    if (!southNeighbour.HasRiver) valid = true;
                    if (!southeastNeighbour.HasRiver) valid = true;

                    if (valid)
                    {
                        if (terrain.TerrainSubType == TerrainSubType.mountain || terrain.TerrainSubType == TerrainSubType.snowmountain || terrain.TerrainSubType == TerrainSubType.earthmountain)
                        {
                            if (avg > 20)
                            {
                                terrain.RiverEntrance = Vector.Direction.still;
                                terrain.HasRiver = true;
                                CalculatePath(terrainDictionary, terrain, "", NumberOfSeasons);
                            }
                        }
                        //else if (terrain.TerrainSubType == TerrainSubType.hill || terrain.TerrainSubType == TerrainSubType.snowhill)
                        //{
                        //    if (avg > 75)
                        //    {
                        //        terrain.RiverEntrance = Vector.Direction.still;
                        //        terrain.HasRiver = true;
                        //        CalculatePath(terrainDictionary, terrain, "", NumberOfSeasons);
                        //    }
                        //}
                    }
                }
            }

            int factor = 5;

            foreach (Terrain terrain in terrainDictionary.Values)
            {
                float avg = (terrain.Rainfall[0] + terrain.Rainfall[1] + terrain.Rainfall[2] + terrain.Rainfall[3]) / 4;

                if(terrain.HasRiver == false && avg > 1000)
                {
                    CreateEndImageForRiverType(terrain);
                    terrain.TerrainSubType = TerrainSubType.lake;
                    AdjustRainfall(terrainDictionary, terrain, NumberOfSeasons, factor);

                    Terrain northwestNeighbour = terrain.NorthwestNeighbour;
                    Terrain northNeighbour = terrain.NorthNeighbour;
                    Terrain northeastNeighbour = terrain.NortheastNeighbour;
                    Terrain westNeighbour = terrain.WestNeighbour;
                    Terrain eastNeighbour = terrain.EastNeighbour;
                    Terrain southwestNeighbour = terrain.SouthwestNeighbour;
                    Terrain southNeighbour = terrain.SouthNeighbour;
                    Terrain southeastNeighbour = terrain.SoutheastNeighbour;

                    AdjustRainfall(terrainDictionary, northwestNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, northNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, northeastNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, westNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, eastNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, southwestNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, southNeighbour, NumberOfSeasons, factor);
                    AdjustRainfall(terrainDictionary, southeastNeighbour, NumberOfSeasons, factor);
                }
            }
        }


        private void ComputeClimates(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, int NumberOfSeasons)
        {
            try
            {
                //Get equatorial zone and polar zone
                int Half = _MaxIndex / 2;
                int Tenth = _MaxIndex / 10;

                int EquatorTop = Half - Tenth;
                int EquatorBottom = Half + Tenth;

                int PolarNorth = Tenth;
                int PolarSouth = _MaxIndex - Tenth;

                //Get desert zone 1 & 2
                int DesertZone1 = EquatorTop - Tenth;
                int DesertZone2 = EquatorBottom + Tenth;

                //Go through the terrain
                for (int season = 0; season < NumberOfSeasons; season++)
                {
                    foreach (Terrain terrain in terrainDictionary.Values)
                    {
                        int row = terrain.Row;

                        float avgRain = (terrain.Rainfall[0] + terrain.Rainfall[1] + terrain.Rainfall[2] + terrain.Rainfall[3]) / 4;
                        float springTemp = terrain.Temperature[0];
                        float summerTemp = terrain.Temperature[1];
                        float fallTemp = terrain.Temperature[2];
                        float winterTemp = terrain.Temperature[3];

                        //forest conversions
                        if (terrain.TerrainSubType == TerrainSubType.hill)
                        {
                            if (avgRain >= 50)
                            {
                                if (row <= Half)
                                { //northern hemisphere                    
                                    if (summerTemp >= 15 && winterTemp > 5) terrain.TerrainSubType = TerrainSubType.forest;
                                    else if (summerTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                }
                                else
                                { //southern hemisphere
                                    if (winterTemp >= 15 && summerTemp > 5) terrain.TerrainSubType = TerrainSubType.forest;
                                    else if (winterTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                }
                            }
                        }
                        else if (terrain.TerrainSubType == TerrainSubType.plains)
                        {
                            if (avgRain >= 100)
                            {
                                if (row <= Half)
                                { //northern hemisphere                    
                                    if (summerTemp >= 15 && winterTemp > 5) terrain.TerrainSubType = TerrainSubType.forest;
                                    else if (summerTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                }
                                else
                                { //southern hemisphere
                                    if (winterTemp >= 15 && summerTemp > 5) terrain.TerrainSubType = TerrainSubType.forest;
                                    else if (winterTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                }
                            }
                        }
                        else if (terrain.TerrainSubType == TerrainSubType.snowhill)
                        {
                            if (avgRain >= 50 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.snowforest;
                        }
                        else if (terrain.TerrainSubType == TerrainSubType.tundra)
                        {
                            if (avgRain >= 100 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.snowforest;
                        }

                        //Desert conversions
                        if (terrain.TerrainSubType == TerrainSubType.hill)
                        {
                            if (avgRain <= 30 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.dryhill;
                        }
                        else if (terrain.TerrainSubType == TerrainSubType.plains)
                        {
                            if (avgRain <= 30 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.desert;
                        }
                        else if (terrain.TerrainSubType == TerrainSubType.forest)
                        {
                            if (avgRain <= 30 && (summerTemp >= 5 || winterTemp >= 5))
                            {
                                if (terrain.Height >= (_ActualHighest / 4) && terrain.Height < _ActualHighest) terrain.TerrainSubType = TerrainSubType.dryhill;
                                else terrain.TerrainSubType = TerrainSubType.desert;
                            }
                        }

                        //Deserts reverting
                        if (terrain.TerrainSubType == TerrainSubType.desert)
                        {
                            if (avgRain > 30 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.plains;
                            else if (avgRain > 50 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.forest;
                        }
                        if (terrain.TerrainSubType == TerrainSubType.dryhill)
                        {
                            if (avgRain > 30 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.hill;
                            else if (avgRain > 50 && (summerTemp >= 5 || winterTemp >= 5)) terrain.TerrainSubType = TerrainSubType.forest;
                        }

                        //Temperature    
                        if (summerTemp > 0 || winterTemp > 0)
                        {
                            if (row <= Half)
                            { //northern hemisphere
                                if (summerTemp < 10)
                                { //summer temp < 10
                                    if (terrain.TerrainSubType == TerrainSubType.hill) terrain.TerrainSubType = TerrainSubType.snowhill;
                                    else if (terrain.TerrainSubType == TerrainSubType.plains) terrain.TerrainSubType = TerrainSubType.tundra;
                                    else if (terrain.TerrainSubType == TerrainSubType.desert) terrain.TerrainSubType = TerrainSubType.snowdesert;
                                    else if (terrain.TerrainSubType == TerrainSubType.forest && summerTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                    else if (terrain.TerrainSubType == TerrainSubType.forest) terrain.TerrainSubType = TerrainSubType.tundra;
                                }
                                else
                                { // >= 10
                                    if (terrain.TerrainSubType == TerrainSubType.snowhill) terrain.TerrainSubType = TerrainSubType.hill;
                                    else if (terrain.TerrainSubType == TerrainSubType.tundra) terrain.TerrainSubType = TerrainSubType.plains;
                                    else if (terrain.TerrainSubType == TerrainSubType.snowdesert) terrain.TerrainSubType = TerrainSubType.desert;
                                    else if (terrain.TerrainSubType == TerrainSubType.snowforest && summerTemp >= 15) terrain.TerrainSubType = TerrainSubType.forest;
                                }
                            }
                            else
                            { //southern hemisphere
                                if (winterTemp < 10)
                                { //winter temp < 10
                                    if (terrain.TerrainSubType == TerrainSubType.hill) terrain.TerrainSubType = TerrainSubType.snowhill;
                                    else if (terrain.TerrainSubType == TerrainSubType.plains) terrain.TerrainSubType = TerrainSubType.tundra;
                                    else if (terrain.TerrainSubType == TerrainSubType.desert) terrain.TerrainSubType = TerrainSubType.snowdesert;
                                    else if (terrain.TerrainSubType == TerrainSubType.forest && summerTemp >= 5) terrain.TerrainSubType = TerrainSubType.snowforest;
                                    else if (terrain.TerrainSubType == TerrainSubType.forest) terrain.TerrainSubType = TerrainSubType.tundra;
                                }
                                else
                                { // >= 10
                                    if (terrain.TerrainSubType == TerrainSubType.snowhill) terrain.TerrainSubType = TerrainSubType.hill;
                                    else if (terrain.TerrainSubType == TerrainSubType.tundra) terrain.TerrainSubType = TerrainSubType.plains;
                                    else if (terrain.TerrainSubType == TerrainSubType.snowdesert) terrain.TerrainSubType = TerrainSubType.desert;
                                    else if (terrain.TerrainSubType == TerrainSubType.snowforest && summerTemp >= 15) terrain.TerrainSubType = TerrainSubType.forest;
                                }
                            }
                        }
                        else if (summerTemp <= 0 && winterTemp <= 0)
                        { //summer and winter 0 or less than
                            if (terrain.TerrainSubType == TerrainSubType.mountain) terrain.TerrainSubType = TerrainSubType.snowmountain;
                            else if (terrain.TerrainSubType == TerrainSubType.hill) terrain.TerrainSubType = TerrainSubType.snowhill;
                            else if (terrain.TerrainSubType == TerrainSubType.plains || terrain.TerrainSubType == TerrainSubType.forest) terrain.TerrainSubType = TerrainSubType.snowdesert;
                            else if (terrain.TerrainSubType == TerrainSubType.lake || terrain.TerrainSubType == TerrainSubType.sea) terrain.TerrainSubType = TerrainSubType.ice;
                        }

                        //Polar landscape extra
                        if (row <= PolarNorth || row >= PolarSouth)
                        {
                            if (row <= Half)
                            { //northern hemisphere
                                if (summerTemp >= 5)
                                { 
                                    if (terrain.TerrainSubType == TerrainSubType.forest) terrain.TerrainSubType = TerrainSubType.snowforest;
                                    if (terrain.TerrainSubType == TerrainSubType.plains) terrain.TerrainSubType = TerrainSubType.tundra;
                                }
                            }
                            else
                            { //southern hemisphere
                                if (winterTemp >= 5)
                                { 
                                    if (terrain.TerrainSubType == TerrainSubType.forest) terrain.TerrainSubType = TerrainSubType.snowforest;
                                    if (terrain.TerrainSubType == TerrainSubType.plains) terrain.TerrainSubType = TerrainSubType.tundra;
                                }
                            }
                        }

                        //Snow Deserts
                        if (terrain.TerrainSubType == TerrainSubType.tundra || terrain.TerrainSubType == TerrainSubType.snowforest)
                        {
                            if (avgRain < 30 || (summerTemp <= 0 && winterTemp <= 0)) terrain.TerrainSubType = TerrainSubType.snowdesert;
                        }

                        //Adjust snowforest
                        if (terrain.TerrainSubType == TerrainSubType.snowforest)
                        {
                            if ((row <= Half && summerTemp < 5) || (row > Half && winterTemp < 5))
                            {
                                if (terrain.Height >= (_ActualHighest / 4) && terrain.Height < _ActualHighest) terrain.TerrainSubType = TerrainSubType.snowhill;
                                else terrain.TerrainSubType = TerrainSubType.tundra;
                            }
                        }

                        ChoosePicture(terrain);
                        //terrain.SetName("");
                    }
                }
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private void ChoosePicture(Terrain terrain)
        {
            if (terrain != null)
            {
                //string current = System.IO.Directory.GetCurrentDirectory();
                //System.IO.DirectoryInfo parent1 = System.IO.Directory.GetParent(current);
                //System.IO.DirectoryInfo parent2 = parent1.Parent;
                //string path = parent2.FullName;

                //string resxFile = path + @"\Properties\Resources.resx";
                string subtype = terrain.TerrainSubType.ToString();
                string imagename = subtype; //default

                if (terrain.TerrainSubType == TerrainSubType.lake)
                {
                    return;
                }
                else if (terrain.TerrainSubType == TerrainSubType.sea)
                {
                    if (terrain.HasRiver) { }
                }
                else if (terrain.TerrainSubType == TerrainSubType.ice)
                {
                    terrain.Image = global::WorldGenerator.Properties.Resources.ice;
                    terrain.Picture = "ice";
                }
                else
                {
                    if (terrain.HasRiver && (terrain.RiverEntrance == Vector.Direction.still || terrain.RiverExit == Vector.Direction.still))
                    {
                        imagename = subtype + "end";
                    }
                    //N entrance
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.north)
                    {
                        //south exit
                        if (terrain.RiverExit == Vector.Direction.south) imagename = "N" + subtype + "S";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "N" + subtype + "W";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "N" + subtype + "E";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "N" + subtype + "NW";
                        //northeast exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "N" + subtype + "NE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "N" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "N" + subtype + "SE";
                        //N exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.north) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //S entrance   
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.south)
                    {
                        //south exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "S";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "S" + subtype + "W";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "S" + subtype + "E";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "S" + subtype + "NW";
                        //northeast exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "S" + subtype + "NE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "S" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "S" + subtype + "SE";
                        //S exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //W entrance
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.west)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "W";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "W";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "W" + subtype + "E";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "W" + subtype + "NW";
                        //northeast exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "W" + subtype + "NE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "W" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "W" + subtype + "SE";
                        //west exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //E entrance       
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.east)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "E";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "E";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "W" + subtype + "E";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "E" + subtype + "NW";
                        //northeast exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "E" + subtype + "NE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "E" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "E" + subtype + "SE";
                        //east exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //NE entrance     
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.northeast)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "NE";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "NE";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "W" + subtype + "NE";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "E" + subtype + "NE";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "NE" + subtype + "NW";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "NE" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "NE" + subtype + "SE";
                        //northeast exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //SW entrance
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.southwest)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "SW";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "SW";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "W" + subtype + "SW";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "E" + subtype + "SW";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "NW" + subtype + "SW";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "NE" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "SE" + subtype + "SW";
                        //southwest exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    //NW entrance 
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.northwest)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "NW";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "NW";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "W" + subtype + "NW";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "E" + subtype + "NW";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "NW" + subtype + "SW";
                        //southeast exit
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = "NW" + subtype + "SE";
                        //northeast exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "NE" + subtype + "NW";
                        //northwest exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }  
                    //SE entrance
                    else if (terrain.HasRiver && terrain.RiverEntrance == Vector.Direction.southeast)
                    {
                        //N exit
                        if (terrain.RiverExit == Vector.Direction.north) imagename = "N" + subtype + "SE";
                        //south exit
                        else if (terrain.RiverExit == Vector.Direction.south) imagename = "S" + subtype + "SE";
                        //west exit
                        else if (terrain.RiverExit == Vector.Direction.west) imagename = "W" + subtype + "SE";
                        //east exit
                        else if (terrain.RiverExit == Vector.Direction.east) imagename = "E" + subtype + "SE";
                        //northwest exit
                        else if (terrain.RiverExit == Vector.Direction.northwest) imagename = "NW" + subtype + "SE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.northeast) imagename = "NE" + subtype + "SE";
                        //southwest exit
                        else if (terrain.RiverExit == Vector.Direction.southwest) imagename = "SE" + subtype + "SW";
                        //southeast exit (something went wrong)
                        else if (terrain.RiverExit == Vector.Direction.southeast) imagename = subtype + "end";
                        //something went really wrong
                        else imagename = subtype + "end";
                    }
                    else
                    {
                        imagename = subtype;
                    }

                    //string resxFile = "Resources.resx";
                    //string imagepath = path + @"\Content\" + subtype + @"\" + imagename + ".png";

                        //image = Image.FromFile(imagepath);
                        //if (image != null) terrain.Image = image;
                    terrain.Picture = imagename;

                    //using (ResXResourceSet resxSet = new ResXResourceSet(resxFile))
                    //{
                    //    Image image = (Image)resxSet.GetObject(imagename, true);
                    //    if (image != null) terrain.Image = image;
                    //}
                }

            }
        }


        private void AddToDictionary(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, KeyValuePair<int, int> keyValuePair, Terrain terrain)
        {
            if (terrainDictionary == null) terrainDictionary = new Dictionary<KeyValuePair<int, int>, Terrain>();

            if(!terrainDictionary.ContainsKey(keyValuePair))
            {
                terrainDictionary.Add(keyValuePair, terrain);
            } else
            {
                terrainDictionary[keyValuePair] = terrain;
            }
        }

        private Terrain GetWestNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain westTerrain = null;

                if (terrainDictionary != null)
                {
                    //If first column element, wrap around
                    if (column == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row, _MaxIndex);

                        if (terrainDictionary.ContainsKey(coordinates)) westTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row, column-1);

                        if (terrainDictionary.ContainsKey(coordinates)) westTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return westTerrain;
            }
            catch(Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private Terrain GetEastNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain eastTerrain = null;

                if (terrainDictionary != null)
                {
                    //If last column element, wrap around
                    if (column == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row, 0);

                        if (terrainDictionary.ContainsKey(coordinates)) eastTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row, column + 1);

                        if (terrainDictionary.ContainsKey(coordinates)) eastTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return eastTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private Terrain GetNorthNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain northTerrain = null;

                if (terrainDictionary != null)
                {
                    //If first row element, wrap around
                    if (row == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(_MaxIndex, column);

                        if (terrainDictionary.ContainsKey(coordinates)) northTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row - 1, column);

                        if (terrainDictionary.ContainsKey(coordinates)) northTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return northTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private Terrain GetSouthNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain southTerrain = null;

                if (terrainDictionary != null)
                {
                    //If last row element, wrap around
                    if (row == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(0, column);

                        if (terrainDictionary.ContainsKey(coordinates)) southTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row + 1, column);

                        if (terrainDictionary.ContainsKey(coordinates)) southTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return southTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

        private Terrain GetNorthwestNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain northwestTerrain = null;

                if (terrainDictionary != null)
                {

                    //If first column element, wrap around
                    if (column == 0 && row == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(_MaxIndex, _MaxIndex);

                        if (terrainDictionary.ContainsKey(coordinates)) northwestTerrain = terrainDictionary[coordinates];
                    }
                    //If first column element, wrap around
                    else if (column == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row - 1, _MaxIndex);

                        if (terrainDictionary.ContainsKey(coordinates)) northwestTerrain = terrainDictionary[coordinates];
                    }
                    //If first row element, wrap around
                    else if (row == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(_MaxIndex, column - 1);

                        if (terrainDictionary.ContainsKey(coordinates)) northwestTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row - 1, column - 1);

                        if (terrainDictionary.ContainsKey(coordinates)) northwestTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return northwestTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private Terrain GetNortheastNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain northeastTerrain = null;

                if (terrainDictionary != null)
                {
                    //If last column element, wrap around
                    if (row == 0 && column == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(_MaxIndex, 0);

                        if (terrainDictionary.ContainsKey(coordinates)) northeastTerrain = terrainDictionary[coordinates];
                    }
                    //If last column element, wrap around
                    else if (column == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row - 1, 0);

                        if (terrainDictionary.ContainsKey(coordinates)) northeastTerrain = terrainDictionary[coordinates];
                    }
                    //If first row element, wrap around
                    else if (row == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(_MaxIndex, column + 1);

                        if (terrainDictionary.ContainsKey(coordinates)) northeastTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row - 1, column + 1);

                        if (terrainDictionary.ContainsKey(coordinates)) northeastTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return northeastTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private Terrain GetSouthwestNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain southwestTerrain = null;

                if (terrainDictionary != null)
                {

                    //If first column element, wrap around
                    if (column == 0 && row == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(0, _MaxIndex);

                        if (terrainDictionary.ContainsKey(coordinates)) southwestTerrain = terrainDictionary[coordinates];
                    }
                    //If first column element, wrap around
                    else if (column == 0)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row + 1, _MaxIndex);

                        if (terrainDictionary.ContainsKey(coordinates)) southwestTerrain = terrainDictionary[coordinates];
                    }
                    //If last row element, wrap around
                    if (row == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(0, column - 1);

                        if (terrainDictionary.ContainsKey(coordinates)) southwestTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row + 1, column - 1);

                        if (terrainDictionary.ContainsKey(coordinates)) southwestTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return southwestTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }


        private Terrain GetSoutheastNeighbour(Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary, Terrain terrain)
        {
            try
            {
                int row = terrain.Row;
                int column = terrain.Column;

                Terrain southeastTerrain = null;

                if (terrainDictionary != null)
                {
                    //If last column element, wrap around
                    if (row == _MaxIndex && column == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(0, 0);

                        if (terrainDictionary.ContainsKey(coordinates)) southeastTerrain = terrainDictionary[coordinates];
                    }
                    //If last column element, wrap around
                    if (column == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row + 1, 0);

                        if (terrainDictionary.ContainsKey(coordinates)) southeastTerrain = terrainDictionary[coordinates];
                    }
                    //If first row element, wrap around
                    else if (row == _MaxIndex)
                    {
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(0, column + 1);

                        if (terrainDictionary.ContainsKey(coordinates)) southeastTerrain = terrainDictionary[coordinates];
                    }
                    else
                    { //otherwise just move left
                        KeyValuePair<int, int> coordinates = new KeyValuePair<int, int>(row + 1, column + 1);

                        if (terrainDictionary.ContainsKey(coordinates)) southeastTerrain = terrainDictionary[coordinates];
                    }
                    //End if
                }

                return southeastTerrain;
            }
            catch (Exception e)
            {
                var ex = e;
                throw ex;
            }
        }

    }
}
