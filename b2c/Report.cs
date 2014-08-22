using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Mono.Data.Sqlite;


namespace b2c {
	public class Report : IDisposable {
		private static SqliteConnection connection;
		private static string srcPath;
		private static string xmlPath;
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
			using(var cmd = new SqliteCommand("SELECT distinct(classname),sourcefile FROM methods WHERE sourcefile != '';", connection)){
				using(var record = cmd.ExecuteReader()) {
					while(record.HasRows && record.Read()) {
						//all classes, packages can be infered.
						Class c = new Class(Convert.ToString(record["sourcefile"]), Convert.ToString(record["classname"]));
						string[] path = c.name.Split('.');
						string packageName = c.name.Substring(0, c.name.IndexOf(path[path.Length-1])-1);
						if(!packages.ContainsKey(packageName)) {
							packages.Add(packageName, new Package(packageName));
						}

						packages[packageName].AddClass(c);
						linesCovered += c.coveredLines;
						linesValid += c.validLines;
					}
				}
				tx.Commit();
			}
		}

		public void Write() {
			File.WriteAllText(xmlPath, ToString());
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
				"timestamp=\"{8}\">\n",
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
			return "\t<sources><source>"+Path.GetFullPath(srcPath)+"</source></sources>\n";
		}

		private string Packages() {
			StringBuilder str = new StringBuilder();
			str.Append("\t<packages>\n");
			foreach(KeyValuePair<string, Package> entry in packages) {
				str.Append(entry.Value.ToString());
			}
			str.Append("\t</packages>\n");
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
				if(validLines == 0)
					return 1;
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
				str.AppendFormat("\t\t<package " +
				                 "name=\"{0}\" " +
				                 "line-rate=\"{1}\" " +
				                 "branch-rate=\"1.0\" " +
				                 "complexity=\"1.0\">\n", 
				                 System.Security.SecurityElement.Escape(name), 
				                 LineRate());
				str.Append("\t\t\t<classes>\n");
				foreach(KeyValuePair<string, Class> c in classes) {
					str.Append(c.Value.ToString());
				}
				str.Append("\t\t\t</classes>\n");
				str.Append("\t\t</package>\n");
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
				using(var cmd = new SqliteCommand("SELECT * FROM methods WHERE classname = :CLASSNAME ", connection)){
					cmd.Transaction = tx;
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
				//double check the relative path part
				System.Uri source = new Uri("file://"+src);
				System.Uri basepath = new Uri("file://"+srcPath);
				StringBuilder str = new StringBuilder();
				str.AppendFormat("\t\t\t\t<class " +
				                 "name=\"{0}\" " +
				                 "filename=\"{1}\" " +
				                 "line-rate=\"{2}\" " +
				                 "branch-rate=\"1.0\" " +
				                 "complexity=\"1.0\">\n",
				                 System.Security.SecurityElement.Escape(name), 
				                 basepath.MakeRelativeUri(source), //print it relative to one of the source files
				                 LineRate());
				str.Append("\t\t\t\t\t<methods>\n");
				foreach(Method c in methods) {
					str.Append(c.ToString());
				}
				str.Append("\t\t\t\t\t</methods>\n");
				str.Append("\t\t\t\t</class>\n");
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
				using(var cmd = new SqliteCommand("SELECT * FROM lines WHERE fullname = :METHODNAME ", connection)){
					cmd.Transaction = tx;
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
				str.AppendFormat("\t\t\t\t\t\t<method " +
				                 "name=\"{0}\" " +
				                 "signature=\"{1}\" " +
				                 "line-rate=\"{2}\" " +
				                 "branch-rate=\"1.0\" " +
				                 "complexity=\"1.0\">\n",
				                 System.Security.SecurityElement.Escape(name), 
				                 System.Security.SecurityElement.Escape(fullname), //print it relative to one of the source files
				                 LineRate());
				str.Append("\t\t\t\t\t\t\t<lines>\n");
				foreach(Line c in lines) {
					str.Append(c.ToString());
				}
				str.Append("\t\t\t\t\t\t\t</lines>\n");
				str.Append("\t\t\t\t\t\t</method>\n");
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
				str.AppendFormat("\t\t\t\t\t\t\t\t<line " +
				                 "number=\"{0}\" " +
				                 "hits=\"{1}\" " +
				                 "branch=\"false\" />\n",
				                 number, 
				                 hits);
				return str.ToString();
			}
		}
	}
}