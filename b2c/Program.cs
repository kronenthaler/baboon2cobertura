using System;
using System.Linq;
using System.Collections.Generic;

namespace b2c {
	class MainClass {
		public static void Main(string[] args) {
			if(args.Length < 3) {
				Usage();
			}

			Report r = new Report(args[0], args[1], args[2]);
			Console.WriteLine(r.ToString());
		}

		static void Usage() {
			Console.WriteLine("b2c COVDB OUTPUT SRC_PATH");
			Environment.Exit(1);
		}
	}
}