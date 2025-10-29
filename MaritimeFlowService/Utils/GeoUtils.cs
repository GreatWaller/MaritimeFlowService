using MaritimeFlowService.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaritimeFlowService.Utils
{
    internal static class GeoUtils
    {
        public static double DistanceMeters((double Lat, double Lon) a, (double Lat, double Lon) b)
        {
            double R = 6371000;
            double dLat = ToRad(b.Lat - a.Lat);
            double dLon = ToRad(b.Lon - a.Lon);
            double lat1 = ToRad(a.Lat); double lat2 = ToRad(b.Lat);
            double hav = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            return 2 * R * Math.Atan2(Math.Sqrt(hav), Math.Sqrt(1 - hav));
        }

        public static bool PointInPolygon((double Lat, double Lon) pt, List<Coordinate> poly)
        {
            int n = poly.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = poly[i]; var pj = poly[j];
                if (((pi.Lon > pt.Lon) != (pj.Lon > pt.Lon)) && (pt.Lat < (pj.Lat - pi.Lat) * (pt.Lon - pi.Lon) / (pj.Lon - pi.Lon) + pi.Lat))
                    inside = !inside;
            }
            return inside;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
