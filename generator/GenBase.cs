// GtkSharp.Generation.GenBase.cs - The Generatable base class.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// (c) 2001-2002 Mike Kestner

namespace GtkSharp.Generation {

	using System;
	using System.IO;
	using System.Xml;

	public abstract class GenBase {
		
		private XmlElement ns;
		private XmlElement elem;
		private bool do_generate;

		protected GenBase (XmlElement ns, XmlElement elem)
		{
			this.ns = ns;
			this.elem = elem;
		}

		public string CName {
			get {
				return elem.GetAttribute ("cname");
			}
		}

		public XmlElement Elem {
			get {
				return elem;
			}
		}

		public string LibraryName {
			get {
				return ns.GetAttribute ("library");
			}
		}

		public string Name {
			get {
				return elem.GetAttribute ("name");
			}
		}

		public string NS {
			get {
				return ns.GetAttribute ("name");
			}
		}

		public string QualifiedName {
			get {
				return NS + "." + Name;
			}
		}

		public bool DoGenerate {
			get { return do_generate; }
			set { do_generate = value; }
		}

		protected StreamWriter CreateWriter () 
		{
			char sep = Path.DirectorySeparatorChar;
			string dir = ".." + sep + NS.ToLower() + sep + "generated";
			if (!Directory.Exists(dir)) {
				Console.WriteLine ("creating " + dir);
				Directory.CreateDirectory(dir);
			}
			String filename = dir + sep + Name + ".cs";
			// Console.WriteLine ("creating " + filename);
			
			FileStream stream = new FileStream (filename, FileMode.Create, FileAccess.Write);
			StreamWriter sw = new StreamWriter (stream);
			
			sw.WriteLine ("// This file was generated by the Gtk# code generator.");
			sw.WriteLine ("// Any changes made will be lost if regenerated.");
			sw.WriteLine ();
			sw.WriteLine ("namespace " + NS + " {");
			sw.WriteLine ();

			return sw;
		}
				
		protected void CloseWriter (StreamWriter sw)
		{
			sw.WriteLine ();
			sw.WriteLine ("}");
			sw.Flush();
			sw.Close();
		}
		
		public void AppendCustom (StreamWriter sw)
		{
			char sep = Path.DirectorySeparatorChar;
			string custom = ".." + sep + NS.ToLower() + sep + Name + ".custom";
			if (File.Exists(custom)) {
				sw.WriteLine ("#line 1 \"" + Name + ".custom\"");
				sw.WriteLine ("#region Customized extensions");
				FileStream custstream = new FileStream(custom, FileMode.Open, FileAccess.Read);
				StreamReader sr = new StreamReader(custstream);
				sw.WriteLine (sr.ReadToEnd ());
				sw.WriteLine ("#endregion");
				sr.Close ();
			}
		}
	}
}

