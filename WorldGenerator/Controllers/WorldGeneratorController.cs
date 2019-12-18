using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorldGenerator.Views;
using WorldGenerator.Repositories;
using WorldGenerator.Models;
using System.Windows.Forms;
using System.Drawing;
using System.Resources;

namespace WorldGenerator.Controllers
{
    public class WorldGeneratorController
    {
        private static WorldGeneratorController worldGeneratorController = null;
        private static WorldGeneratorRepository worldGeneratorRepository = null;
        private static WorldMap worldMap = null;
        private static int size = 128;

        public WorldGeneratorController()
        {
        }

        private static WorldGeneratorController CreateWorldGeneratorController()
        {
            if (worldGeneratorController == null) worldGeneratorController = new WorldGeneratorController();

            return worldGeneratorController;
        }

        private static WorldGeneratorRepository CreateWorldGeneratorRepository()
        {
            if (worldGeneratorRepository == null) worldGeneratorRepository = new WorldGeneratorRepository(size);

            return worldGeneratorRepository;
        }

        public static void Main()
        {
            worldGeneratorController = CreateWorldGeneratorController();
            worldGeneratorRepository = CreateWorldGeneratorRepository();
            Dictionary <KeyValuePair<int, int>, Terrain > terrainDictionary = worldGeneratorRepository.GenerateWorld();
            worldGeneratorController.ShowWorldMap(terrainDictionary);
        }

        private void ShowWorldMap(Dictionary<KeyValuePair<int,int>,Terrain> terrainDictionary)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                worldMap = new WorldMap(128, 10, 10, terrainDictionary);

                int maxindex = size - 1;
                string resxFile = "Resources.resx";

                using (ResXResourceSet resxSet = new ResXResourceSet(resxFile))
                {
                    for (int i = 0; i < maxindex; i++)
                    {
                        for (int j = 0; j < maxindex; j++)
                        {
                            KeyValuePair<int, int> keyValuePair = new KeyValuePair<int, int>(i, j);
                            Terrain terrain = terrainDictionary[keyValuePair];

                            Image image = (Image)resxSet.GetObject(terrain.Picture, true);
                            if (image != null) terrain.Image = image;

                            worldMap.FillCell(i, j, terrain);
                        }
                    }
                }

                Application.Run(worldMap);
            }
            catch(Exception e)
            {
                throw e;
            }
        }        
    }
}
