﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDigest.CMD {
    class Program {
        static void Main(string[] args) {
            Random r = new Random();

            TDigest digestA = new TDigest();
            TDigest digestAll = new TDigest();
            List<double> actual = new List<double>();
            for (int i = 0; i < 10000; i++) {
                var n = (r.Next() % 50) + (r.Next() % 50);
                digestA.Add(n);
                //digestAll.Add(n);
                actual.Add(n);
            }

            TDigest digestB = new TDigest();
            List<double> actualB = new List<double>();
            for (int i = 0; i < 10000; i++) {
                var n = (r.Next() % 100) + (r.Next() % 100);
                digestB.Add(n);
                digestAll.Add(n);
                actual.Add(n);
            }

            actual.Sort();

            var merged = TDigest.Merge(digestA, digestB);
            //Debug.Assert.AreEqual(actual.Count, merged.Count);

            //var avgError = GetAvgError(actual, merged);
            //Assert.IsTrue(avgError < .5);

            var trueAvg = actual.Average();
            var deltaAvg = Math.Abs(digestAll.Average - merged.Average);
        }
    }
}
