using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace b2c {
	class MainClass {
		public static void Main(string[] args) {
			if(args.Length < 3) {
				Usage();
			}

			Console.WriteLine(Path.GetFullPath(args[0]));
			Report r = new Report(Path.GetFullPath(args[0]), args[1], args[2]);
			r.Write();
		}

		static void Usage() {
			Console.WriteLine("b2c COVDB OUTPUT SRC_PATH");
			Environment.Exit(1);
		}
	}
}