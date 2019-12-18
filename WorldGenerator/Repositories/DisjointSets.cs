using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldGenerator.Repositories
{
	/// <summary>
	/// DisjointSets class
	/// 
	/// CONSTRUCTION: with int representing initial number of sets
	/// 
	/// ******************PUBLIC OPERATIONS*********************
	/// void union( root1, root2 ) --> Merge two sets
	/// int find( x )              --> Return set containing x
	/// ******************ERRORS********************************
	/// Error checking of parameters is performed
	/// 
	/// Disjoint set class, using union by rank
	/// and path compression.
	/// Elements in the set are numbered starting at 0.
	/// 
	/// Original author:
	/// @author Mark Allen Weiss
	/// 
	/// 
	/// C# conversion:
	/// Changed array .length to the c# .Length
	/// Added additional comments.
	/// 
	/// @author: Robin Osborne
	/// @version: 0.1.0
	/// @date: 2015-01-30
	/// </summary>	
	public class DisjointSets
	{
        private int[] s;

        /// <summary>
        /// Constructor for the disjoint sets object.
        /// </summary>
        /// <param name="numElements">the initial number of disjoint sets</param>
        public DisjointSets(int numElements)
		{
			s = new int[numElements];
			for (int i = 0; i < s.Length; i++)
				s[i] = -1;
		}

		/// <summary>
		/// Union does a union operation on two disjoint sets using the height heuristic.
		/// root1 and root2 are distinct and represent set names.
		/// Throws an ArgumentException if root1 or roo2 are not distinct roots.
		/// </summary>
		/// <param name="root1">a distinct set name</param>
		/// <param name="root2">another distinct set name</param> 
		public void Union(int root1, int root2)
		{
			AssertIsRoot(root1);
			AssertIsRoot(root2);
			if (root1 == root2)
				throw new ArgumentException("Union: root1 == root2 " + root1);

			if (s[root2] < s[root1])  // root2 is deeper
				s[root1] = root2;        // Make root2 new root
			else
			{
				if (s[root1] == s[root2])
					s[root1]--;          // Update height if same
				s[root2] = root1;        // Make root1 new root
			}
		}

		/// <summary>
		/// Perform a find with path compression.
		/// Find recursively calls itself to find x.
		/// Throws an ArgumentException if x is not valid (through AssertIsItem).
		/// </summary>
		/// <param name="x">the element being searched for</param>
		/// <returns>the set containing x</returns> 
		public int Find(int x)
		{
			AssertIsItem(x);
			if (s[x] < 0)
				return x;
			else
				return s[x] = Find(s[x]);
		}

		/// <summary>
		/// AssertIsRoot first calls AssertIsItem to make sure the inparameter 'root'
		/// exists as a disjoint set and then tests that it is in fact a root. 
		/// Throws an ArgumentException if 'root' is not an item (through AssertIsItem)
		/// ánd an ArgumentExcept if it is an item but not a root.
		/// </summary>
		/// <param name="root">an integer to test for being an item and root</param>
		private void AssertIsRoot(int root)
		{
			AssertIsItem(root);
			if (s[root] >= 0)
				throw new ArgumentException("Union: " + root + " not a root");
		}

		/// <summary>
		/// AssertIsItem tests whether the item exists within the disjoint set and is
		/// therefore a valid item within it.
		/// Throws an ArgumentException if 'x' is not a valid item.
		/// </summary>
		/// <param name="x">an integer to test for being an item</param>
		private void AssertIsItem(int x)
		{
			if (x < 0 || x >= s.Length)
				throw new ArgumentException("Disjoint sets: " + x + " not an item");
		}
	}
}
