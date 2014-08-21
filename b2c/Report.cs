using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Mono.Data.Sqlite;

namespace b2c {
	public class Report : IDisposable {
		private static SqliteConnection connection;
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
						Class c = new Class(Convert.ToString(record["sourcefile"]), Convert.ToString(record["classname"]));
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
			StringBuilder str = new StringBuilder();
			str.Append("<packages>");
			foreach(KeyValuePair<string, Package> entry in packages) {
				str.Append(entry.Value.ToString());
			}
			str.Append("/<packages>");
			return str.ToString();
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

			public override string ToString() {
				StringBuilder str = new StringBuilder();
				str.AppendFormat("<package " +
				                 "name=\"{0}\" " +
				                 "line-rate=\"{1}\" " +
				                 "branch-rate=\"1.0\" " +
								 "complexity=\"1.0\">", name, LineRate());
				str.Append("<classes>");
				foreach(KeyValuePair<string, Class> c in classes) {
					str.Append(c.Value.ToString());
				}
				str.Append("</classes>");
				str.Append("</package>");
				return str.ToString();
			}
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
							Method m = new Method(Convert.ToString(record["name"]), Convert.ToString(record["fullname"]));
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

			public override string ToString() {
				StringBuilder str = new StringBuilder();
				str.AppendFormat("<class " +
				                 "name=\"{0}\" " +
				                 "filename=\"{1}\" " +
				                 "line-rate=\"{2}\" " +
				                 "branch-rate=\"1.0\" " +
				                 "complexity=\"1.0\">",
				                 name, 
				                 src, //print it relative to one of the source files
				                 LineRate());
				str.Append("<methods>");
				foreach(Method c in methods) {
					str.Append(c.ToString());
				}
				str.Append("</methods>");
				str.Append("</class>");
				return str.ToString();
			}
		}

		class Method : Element {
			public IList<Line> lines;
			public string name;
			public string fullname;

			public Method(string n, string f){
				lines = new List<Line>();
				name = n;
				fullname = f;

				Init();
			}

			private void Init(){
				using(var tx = connection.BeginTransaction()) 
					using(var cmd = new SqliteCommand(connection)){
					cmd.Transaction = tx;
					cmd.CommandText = @"SELECT * FROM lines WHERE fullname = :METHODNAME ";
					cmd.Parameters.Add(new SqliteParameter(":METHODNAME", fullname));

					using(var record = cmd.ExecuteReader()) {
						while(record.HasRows && record.Read()) {
							Line l = new Line(Convert.ToInt32(record["line"]),Convert.ToInt32(record["hits"]));
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

			public override string ToString() {
				StringBuilder str = new StringBuilder();
				str.AppendFormat("<method " +
				                 "name=\"{0}\" " +
				                 "signature=\"{1}\" " +
				                 "line-rate=\"{2}\" " +
				                 "branch-rate=\"1.0\" " +
				                 "complexity=\"1.0\">",
				                 name, 
				                 fullname, //print it relative to one of the source files
				                 LineRate());
				str.Append("<lines>");
				foreach(Line c in lines) {
					str.Append(c.ToString());
				}
				str.Append("</lines>");
				str.Append("</method>");
				return str.ToString();
			}
		}

		class Line {
			public int number;
			public int hits;

			public Line(int n, int h){
				number = n;
				hits = h;
			}

			public override string ToString() {
				StringBuilder str = new StringBuilder();
				str.AppendFormat("<line " +
				                 "number=\"{0}\" " +
				                 "hits=\"{1}\" " +
				                 "branch=\"false\" />",
				                 number, 
				                 hits);
				return str.ToString();
			}
		}
	}
}