﻿using System.Collections.Generic;
using UnityEngine;

namespace MSP2050.Scripts
{
	class RasterizerAltered
	{
		private static float intConversion = 100000000000000.0f;

		public static void RasterizePolygons(List<PolygonSubEntity> polygons, ref float[,] raster, Rect rasterBounds)
		{
			int voffset = (int)rasterBounds.yMin;
			int vsquaresize = (int)(rasterBounds.yMax - rasterBounds.yMin) / 1024;
			int hoffset = (int)rasterBounds.xMin;
			int hsquaresize = (int)(rasterBounds.xMax - rasterBounds.xMin) / 1024;

			foreach (PolygonSubEntity polygon in polygons)
			{
				//Convert to int poly
				List<ClipperLib.IntPoint> intpoly = VectorToIntPoint(polygon.GetPoints());
				float weight = polygon.Entity.EntityTypes[0].investmentCost;
				//if(weight == 0)
				//    Debug.Log(polygon.Entity.name);

				//Determine squares within bounding box
				long left, right, top, bot;
				GetBounds(intpoly, out left, out right, out top, out bot);
				int xmin = ((int) left - hoffset) / hsquaresize;
				int xmax = ((int) right - hoffset) / hsquaresize;
				int ymin = ((int) bot - voffset) / vsquaresize;
				int ymax = ((int) top - voffset) / vsquaresize;

				//Foreach overlapping square: calculate intersecting area
				for (int x = xmin; x < xmax; x++)
				for (int y = ymin; y < ymax; y++)
				{
					//Clipper clipper = new Clipper();

					////Construct polygon paths (of poly and grid square)
					//clipper.AddPaths(new List<List<IntPoint>>() {intpoly}, PolyType.ptSubject, true);
					//clipper.AddPaths(new List<List<IntPoint>>() {GetSquarePoly(x * hsquaresize, (x + 1) * hsquaresize, y * vsquaresize, (y + 1) * vsquaresize)}, PolyType.ptClip, true);

					////Calculate intersection
					//List<List<IntPoint>> intersection = new List<List<IntPoint>>();
					//clipper.Execute(ClipType.ctIntersection, intersection);

					////Calculate part of square that is covered by the intersection and add it to the result
					//if (intersection.Count > 0 && intersection[0].Count > 0)
					//raster[x, y] *= Mathf.Lerp(1f, weight, GetPolygonArea(intersection) / sqrt2);
					raster[x, y] *= weight;
				}
			}
		}

		public static void RasterizeLines(List<LineStringSubEntity> lines, ref float[,] raster, Rect rasterBounds)
		{
			int voffset = (int) rasterBounds.yMin;
			int vsquaresize = (int) (rasterBounds.yMax - rasterBounds.yMin) / 1024;
			int hoffset = (int) rasterBounds.xMin;
			int hsquaresize = (int) (rasterBounds.xMax - rasterBounds.xMin) / 1024;

			foreach (LineStringSubEntity line in lines)
			{
				//Convert to int poly
				List<ClipperLib.IntPoint> intline = VectorToIntPoint(line.GetPoints());
				float weight = line.Entity.EntityTypes[0].investmentCost;
				//if (weight == 0)
				//    Debug.Log(line.Entity.name);

				//Determine squares within bounding box
				long left, right, top, bot;
				GetBounds(intline, out left, out right, out top, out bot);
				int xmin = ((int) left - hoffset) / hsquaresize;
				int xmax = ((int) right - hoffset) / hsquaresize;
				int ymin = ((int) bot - voffset) / vsquaresize;
				int ymax = ((int) top - voffset) / vsquaresize;

				//Foreach overlapping square: calculate intersecting area
				for (int x = xmin; x < xmax; x++)
				for (int y = ymin; y < ymax; y++)
				{
					//Clipper clipper = new Clipper();

					////Construct polygon paths (of poly and grid square)
					//clipper.AddPaths(new List<List<IntPoint>>() {intline}, PolyType.ptSubject, false);
					//clipper.AddPaths(new List<List<IntPoint>>() {GetSquarePoly(x * hsquaresize, (x + 1) * hsquaresize, y * vsquaresize, (y + 1) * vsquaresize)}, PolyType.ptClip, true);

					////Calculate intersection
					//List<List<IntPoint>> intersection = new List<List<IntPoint>>();
					//clipper.Execute(ClipType.ctIntersection, intersection);

					////Calculate part of square that is covered by the intersection and add it to the result
					//if (intersection.Count > 0 && intersection[0].Count > 0)
					raster[x, y] *= weight;
				}
			}
		}

		/// <summary>
		/// Gets the bounds of a polygon. Returned rect is non-rotated.
		/// </summary>
		public static void GetBounds(List<ClipperLib.IntPoint> poly, out long left, out long right, out long top, out long bot)
		{
			left = 0; right = 0; top = 0; bot = 0;
			foreach (ClipperLib.IntPoint v in poly)
			{
				if (v.X > right)
					right = v.X;
				else if (v.X < left)
					left = v.X;
				if (v.Y > top)
					top = v.Y;
				else if (v.Y < bot)
					bot = v.Y;
			}
		}

		private static List<ClipperLib.IntPoint> VectorToIntPoint(List<Vector3> points)
		{
			List<ClipperLib.IntPoint> verts = new List<ClipperLib.IntPoint>();

			for (int i = 0; i < points.Count; i++)
			{
				verts.Add(new ClipperLib.IntPoint(points[i].x * intConversion, points[i].y * intConversion));
			}

			return verts;
		}

		private static List<ClipperLib.IntPoint> GetSquarePoly(long xmin, long xmax, long ymin, long ymax)
		{
			return new List<ClipperLib.IntPoint>()
			{
				new ClipperLib.IntPoint(ymin, xmin),
				new ClipperLib.IntPoint(ymin, xmax),
				new ClipperLib.IntPoint(ymax, xmax),
				new ClipperLib.IntPoint(ymax, xmin)
			};
		}

		private static float GetPolygonArea(List<List<ClipperLib.IntPoint>> polygons)
		{
			float area = 0;
			foreach (List<ClipperLib.IntPoint> polygon in polygons)
			{
				for (int i = 0; i < polygon.Count; ++i)
				{
					int j = (i + 1) % polygon.Count;
					area += polygon[i].Y * polygon[j].X - polygon[i].X * polygon[j].Y;
				}
			}
			return Mathf.Abs(area * 0.5f);
		}
	}
}