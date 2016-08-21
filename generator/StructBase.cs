// GtkSharp.Generation.StructBase.cs - The Structure/Boxed Base Class.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2001-2003 Mike Kestner
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the GNU General Public
// License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.


namespace GtkSharp.Generation {

	using System;
	using System.Collections;
	using System.IO;
	using System.Text.RegularExpressions;
	using System.Xml;

	public abstract class StructBase : ClassBase, IManualMarshaler {
	
		internal new ArrayList fields = new ArrayList ();
		bool need_read_native = false;

		protected StructBase (XmlElement ns, XmlElement elem) : base (ns, elem)
		{
			foreach (XmlNode node in elem.ChildNodes) {

				if (!(node is XmlElement)) continue;
				XmlElement member = (XmlElement) node;

				switch (node.Name) {
				case "field":
					fields.Add (new StructField (member, this));
					break;

				case "callback":
					Statistics.IgnoreCount++;
					break;

				default:
					if (!IsNodeNameHandled (node.Name))
						Console.WriteLine ("Unexpected node " + node.Name + " in " + CName);
					break;
				}
			}
		}

		public override string DefaultValue {
			get {
				return QualifiedName + ".Zero";
			}
		}

		public override string MarshalType {
			get {
				return "IntPtr";
			}
		}

		public override string AssignToName {
			get { throw new NotImplementedException (); }
		}

		public override string CallByName ()
		{
			return "this_as_native";
		}

		public override string CallByName (string var)
		{
			return var + "_as_native";
		}

		public override string FromNative (string var)
		{
			if (DisableNew)
				return var + " == IntPtr.Zero ? " + QualifiedName + ".Zero : (" + QualifiedName + ") System.Runtime.InteropServices.Marshal.PtrToStructure (" + var + ", typeof (" + QualifiedName + "))";
			else
				return QualifiedName + ".New (" + var + ")";
		}
		
		public string AllocNative (string var)
		{
			return "GLib.Marshaller.StructureToPtrAlloc (" + var + ")";
		}

		public string ReleaseNative (string var)
		{
			return "Marshal.FreeHGlobal (" +var + ")";
		}

		private bool DisableNew {
			get {
				return Elem.HasAttribute ("disable_new");
			}
		}

		protected new void GenFields (GenerationInfo gen_info)
		{
			int bitfields = 0;
			bool need_field = true;

			foreach (StructField field in fields) {
				if (field.IsBitfield) {
					if (need_field) {
						StreamWriter sw = gen_info.Writer;

						sw.WriteLine ("\t\tprivate uint _bitfield{0};\n", bitfields++);
						need_field = false;
					}
				} else
					need_field = true;
				field.Generate (gen_info, "\t\t");
			}
		}

		public override bool Validate ()
		{
			foreach (StructField field in fields) {
				if (!field.Validate ()) {
					Console.WriteLine ("in Struct " + QualifiedName);
					if (!field.IsPointer)
						return false;
				}
			}

			return base.Validate ();
		}

		public override void Generate (GenerationInfo gen_info)
		{
			bool need_close = false;
			if (gen_info.Writer == null) {
				gen_info.Writer = gen_info.OpenStream (Name);
				need_close = true;
			}

			StreamWriter sw = gen_info.Writer;
			
			sw.WriteLine ("namespace " + NS + " {");
			sw.WriteLine ();
			sw.WriteLine ("\tusing System;");
			sw.WriteLine ("\tusing System.Collections;");
			sw.WriteLine ("\tusing System.Runtime.InteropServices;");
			sw.WriteLine ();
			
			sw.WriteLine ("#region Autogenerated code");
			if (IsDeprecated)
				sw.WriteLine ("\t[Obsolete]");
			sw.WriteLine ("\t[StructLayout(LayoutKind.Sequential)]");
			GenerateAttribute (sw);
			string access = IsInternal ? "internal" : "public";
			sw.WriteLine ("\t" + access + " struct " + Name + " {");
			sw.WriteLine ();

			need_read_native = false;
			GenFields (gen_info);
			sw.WriteLine ();
			GenCtors (gen_info);
			GenMethods (gen_info, null, this);
			if (need_read_native)
				GenReadNative (sw);

			if (!need_close)
				return;

			sw.WriteLine ("#endregion");
			AppendCustom(sw, gen_info.CustomDir);
			
			sw.WriteLine ("\t}");
			sw.WriteLine ("}");

			sw.Close ();
			gen_info.Writer = null;
		}

		protected virtual void GenerateAttribute (StreamWriter writer)
		{
		}
		
		protected override void GenCtors (GenerationInfo gen_info)
		{
			StreamWriter sw = gen_info.Writer;

			sw.WriteLine ("\t\tpublic static {0} Zero = new {0} ();", QualifiedName);
			sw.WriteLine();
			if (!DisableNew) {
				sw.WriteLine ("\t\tpublic static " + QualifiedName + " New(IntPtr raw) {");
				sw.WriteLine ("\t\t\tif (raw == IntPtr.Zero)");
				sw.WriteLine ("\t\t\t\treturn {0}.Zero;", QualifiedName);
				sw.WriteLine ("\t\t\treturn ({0}) Marshal.PtrToStructure (raw, typeof ({0}));", QualifiedName);
				sw.WriteLine ("\t\t}");
				sw.WriteLine ();
			}

			foreach (Ctor ctor in Ctors)
				ctor.IsStatic = true;

			base.GenCtors (gen_info);
		}

		void GenReadNative (StreamWriter sw)
		{
			sw.WriteLine ("\t\tstatic void ReadNative (IntPtr native, ref {0} target)", QualifiedName);
			sw.WriteLine ("\t\t{");
			sw.WriteLine ("\t\t\ttarget = New (native);");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();
		}

		public override void Prepare (StreamWriter sw, string indent)
		{
			bool canUseSizeOf = true;
			foreach (StructField child in this.fields) {
				if (!SymbolTable.CanUseSizeOf (child.CType))
					canUseSizeOf = false;
			}
			if (canUseSizeOf) {
				sw.WriteLine (indent + "int marshalSize;");
				sw.WriteLine (indent + "unsafe { marshalSize = sizeof (" + QualifiedName + "); }");
				sw.WriteLine (indent + "IntPtr this_as_native = System.Runtime.InteropServices.Marshal.AllocHGlobal (marshalSize);");
			} else {
				sw.WriteLine (indent + "IntPtr this_as_native = System.Runtime.InteropServices.Marshal.AllocHGlobal (System.Runtime.InteropServices.Marshal.SizeOf (this));");
			}
			sw.WriteLine (indent + "System.Runtime.InteropServices.Marshal.StructureToPtr (this, this_as_native, false);");
		}

		public override void Finish (StreamWriter sw, string indent)
		{
			need_read_native = true;
			sw.WriteLine (indent + "ReadNative (this_as_native, ref this);");
			sw.WriteLine (indent + "System.Runtime.InteropServices.Marshal.FreeHGlobal (this_as_native);");
		}
	}
}

