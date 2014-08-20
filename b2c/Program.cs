using System;
using System.Linq;
using System.Collections.Generic;

namespace b2c {
	class MainClass {
		public static void Main(string[] args) {
			if(args.Length < 3) {
				Usage();
			}


		}

		static void Usage() {
			Console.WriteLine("b2c COVDB OUTPUT SRC_PATH");
			Environment.Exit(1);
		}
	}
}