using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldGenerator.Models
{
    public class Vector
    {
        public enum Direction
        {
            none = 0, southwest = 1, south = 2, southeast = 3, west = 4, still = 5, east = 6, northwest = 7, north = 8, northeast = 9
        }

        public Direction VectorDirection { get; set; }
        public float Velocity { get; set; }

        public Vector()
        {

        }
    }
}
