using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Mono.Data.Sqlite;

namespace b2c {
	public class Report : IDisposable {
		private SqliteConnection connection;
		private string srcPath;
		private string xmlPath;
		private int linesValid;
		private int linesCovered;
		private IDictionary<string, Package> packages;

		public Report(string db, string xml, string src) {
			packages = new Dictionary<string, Package>();

			connection = new SqliteConnection(string.Format("URI=file:{0}", db));
			connection.Open();

			srcPath = src;
			xmlPath = xml;

			InitializeData();
		}

		private void InitializeData() {
			//initialize a data structure.
			using(var tx = connection.BeginTransaction()) 
			using(var cmd = new SqliteCommand(connection)){
				cmd.Transaction = tx;
				cmd.CommandText = @"SELECT distinct(classname) FROM methods";
				using(var record = cmd.ExecuteReader()) {
					while(record.HasRows && record.Read()) {
						//all classes, packages can be infered.
						Class c = new Class(Convert.ToString("sourcefile"), Convert.ToString("classname"));
						string[] path = c.name.Split('.');
						string packageName =c.name.Substring(0, c.name.IndexOf(path[path.Length-1]));
						if(!packages.ContainsKey(packageName)) {
							packages.Add(packageName, new Package(packageName));
						}

						packages[packageName].AddClass(c);
					}
				}
				tx.Commit();
			}
		}

		public void Write() {
			//open file an output 
		}

		public override string ToString() {
			string str = string.Empty;
			str += "<?xml version=\"1.0\"?>";
			str += "<!DOCTYPE coverage SYSTEM \"http://cobertura.sourceforge.net/xml/coverage-04.dtd\">";
			str += string.Format(
				"<coverage " +
				"line-rate=\"{0}\" " +
				"lines-covered=\"{1}\" "+
				"lines-valid=\"{2}\" "+
				"branch-rate=\"{3}\" " +
				"branches-covered=\"{4}\" "+
				"branches-valid=\"{5}\" "+ 
				"complexity=\"{6}\" "+
				"version=\"{7}\" " +
				"timestamp=\"{8}\">",
				linesCovered/(double)linesValid,
				linesCovered,
				linesValid,
				1,1,1,	//branching parameters.
				1.0, 	//complexity
				"1.10",	//version
				CurrentTimeMillis());
			str += Sources();
			str += Packages();
			str += "</coverage>";

			return str.ToString();
		}

		private string Sources() {
			return "";
		}

		private string Packages() {
			return "";
		}

		public void Dispose() {
			if(connection != null) {
				connection.Close();
			}
		}

		private static readonly DateTime Jan1st1970 = new DateTime
			(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static long CurrentTimeMillis() {
			return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
		}

		class Element {
			public int validLines;
			public int coveredLines;

			public double LineRate(){
				return coveredLines / (double)validLines;
			}
		}

		class Package : Element {
			public IDictionary<string, Class> classes;
			public string name;

			public Package(string n){
				classes = new Dictionary<string, Class>();
				name = n;
			}

			public void AddClass(Class c){
				if(!classes.ContainsKey(c.name)) {
					classes.Add(c.name, c);
					validLines += c.validLines;
					coveredLines += c.coveredLines;
				}
			}

			//tostring
		}

		class Class : Element {
			public IList<Method> methods;
			public string src;
			public string name;

			public Class(string s, string n){
				methods = new List<Method>();
				src = s;
				name = n;

				Init();
			}

			private void Init(){
				using(var tx = connection.BeginTransaction()) 
					using(var cmd = new SqliteCommand(connection)){
					cmd.Transaction = tx;
					cmd.CommandText = @"SELECT * FROM methods WHERE classname = :CLASSNAME ";
					cmd.Parameters.Add(new SqliteParameter(":CLASSNAME", name));

					using(var record = cmd.ExecuteReader()) {
						while(record.HasRows && record.Read()) {
							Method m = new Method(Convert.ToString("name"));
							AddMethod(m);
						}
					}
					tx.Commit();
				}
			}

			public void AddMethod(Method m){
				methods.Add(m);
				validLines += m.validLines;
				coveredLines += m.coveredLines;
			}

			//tostring
		}

		class Method : Element {
			public IList<Line> lines;
			public string name;

			public Method(string n){
				lines = new List<Line>();
				name = n;

				Init();
			}

			private void Init(){
				using(var tx = connection.BeginTransaction()) 
					using(var cmd = new SqliteCommand(connection)){
					cmd.Transaction = tx;
					cmd.CommandText = @"SELECT * FROM lines WHERE fullname = :METHODNAME ";
					cmd.Parameters.Add(new SqliteParameter(":METHODNAME", name));

					using(var record = cmd.ExecuteReader()) {
						while(record.HasRows && record.Read()) {
							Line l = new Line(Convert.ToInt32("line"),Convert.ToInt32("hits"));
							AddLine(l);
						}
					}
					tx.Commit();
				}
			}

			public void AddLine(Line l){
				lines.Add(l);
				coveredLines += l.hits > 0 ? 1 : 0;
				validLines++;
			}

			//tostring
		}

		class Line {
			public int number;
			public int hits;

			public Line(int n, int h){
				number = n;
				hits = h;
			}

			//tostring
		}
	}
}