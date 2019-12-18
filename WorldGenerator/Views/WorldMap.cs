using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WorldGenerator.Models;

namespace WorldGenerator.Views
{
    public partial class WorldMap : Form
    {
        private int columns_max_index = 127;
        private int rows_max_index = 127;
        private DataGridView gridview;
        private Dictionary<KeyValuePair<int, int>, Terrain> TerrainDictionary;
        //private int image_height = 10;
        //private int image_width = 10;

        public WorldMap(int size, int image_height, int image_width, Dictionary<KeyValuePair<int, int>, Terrain> terrainDictionary)
        {
            try
            {
                columns_max_index = size - 1;
                rows_max_index = size - 1;
                this.TerrainDictionary = terrainDictionary;

                InitializeComponent();
                this.Controls.Clear();
                this.AutoSize = true;
                this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
                gridview = new DataGridView();
                gridview.Dock = DockStyle.Fill;
                gridview.RowTemplate.Height = image_height;
                gridview.CellClick += new DataGridViewCellEventHandler(gridview_CellClick);
                this.Controls.Add(gridview);

                //Image image = Image.FromFile("C:/Users/Robin/source/repos/WorldGenerator/WorldGenerator/Models/water/water.png");
                Image image = global::WorldGenerator.Properties.Resources.water;

                for (int column = 0; column < columns_max_index; column++)
                {
                    DataGridViewImageColumn imageCol = new DataGridViewImageColumn();
                    imageCol.ValuesAreIcons = false;
                    imageCol.HeaderText = column.ToString();
                    imageCol.Name = "img";
                    imageCol.Width = image_width;

                    gridview.Columns.Add(imageCol);
                }

                //I guess there's always 1 row, so you only need to add rows - 1
                for (int row = 0; row < (rows_max_index - 1); row++)
                {
                    gridview.Rows.Add();
                    gridview.Rows[row].HeaderCell.Value = row.ToString();
                }

                //Then fill all the cells with default water (for now)
                //for (int i = 0; i < columns_max_index; i++)
                //{
                //    for (int j = 0; j < rows_max_index; j++)
                //    {
                //        FillCell(j, i, image);
                //    }
                //}
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public bool FillCell(int row, int column, Terrain terrain)
        {
            try
            {
                gridview.Rows[row].Cells[column].Value = terrain.Image;

                return true;
            }
            catch (Exception e)
            {
                throw e;
                //return false;
            }
        }

        private void WorldMap_Load(object sender, EventArgs e)
        {

        }

        private void gridview_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int row = e.RowIndex;
            int column = e.ColumnIndex;

            if(TerrainDictionary != null)
            {
                KeyValuePair<int, int> coords = new KeyValuePair<int, int>(row, column);
                Terrain terrain = TerrainDictionary[coords];
                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.Append("Terrain type: " + terrain.TerrainType.ToString() + "\n");
                stringBuilder.Append("Terrain subtype: " + terrain.TerrainSubType.ToString() + "\n");
                stringBuilder.Append("Terrain picture: " + terrain.Picture + "\n");
                stringBuilder.Append("Has river: " + terrain.HasRiver + "\n");
                if (terrain.HasRiver)
                {
                    stringBuilder.Append("Rivername: " + terrain.RiverName + "\n");
                    stringBuilder.Append("River enters: " + terrain.RiverEntrance.ToString() + "\n");
                    stringBuilder.Append("River exits: " + terrain.RiverExit.ToString() + "\n");
                }
                stringBuilder.Append("Height: " + terrain.Height + "\n");
                stringBuilder.Append("Spring temp: " + terrain.Temperature[0].ToString() + "\n");
                stringBuilder.Append("Summer temp: " + terrain.Temperature[1].ToString() + "\n");
                stringBuilder.Append("Fall temp: " + terrain.Temperature[2].ToString() + "\n");
                stringBuilder.Append("Winter temp: " + terrain.Temperature[3].ToString() + "\n");
                stringBuilder.Append("Spring rainfall: " + terrain.Rainfall[0].ToString() + "\n");
                stringBuilder.Append("Summer rainfall: " + terrain.Rainfall[1].ToString() + "\n");
                stringBuilder.Append("Fall rainfall: " + terrain.Rainfall[2].ToString() + "\n");
                stringBuilder.Append("Winter rainfall: " + terrain.Rainfall[3].ToString() + "\n");
                stringBuilder.Append("Spring wind: " + terrain.Wind[0].VectorDirection.ToString() + " " + terrain.Wind[0].Velocity.ToString() + "\n");
                stringBuilder.Append("Summer wind: " + terrain.Wind[1].VectorDirection.ToString() + " " + terrain.Wind[1].Velocity.ToString() + "\n");
                stringBuilder.Append("Fall wind: " + terrain.Wind[2].VectorDirection.ToString() + " " + terrain.Wind[2].Velocity.ToString() + "\n");
                stringBuilder.Append("Winter wind: " + terrain.Wind[3].VectorDirection.ToString() + " " + terrain.Wind[3].Velocity.ToString() + "\n");

                MessageBox.Show(stringBuilder.ToString());
            }
        }
    }
}
