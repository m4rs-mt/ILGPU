using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SimpleStructures")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("SimpleStructures")]
[assembly: AssemblyCopyright("Copyright ©  2017")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]


// Sine the CPUAccelerator of the ILGPU runtime uses dynamic assembly generation
// in order to avoid boxing of parameters, the dynamically created assembly cannot
// access custom internal types of this assembly. Consequently, we have to make these
// custom types visible to the CPUAccelerator. For this reason, we add an
// InternalsVisibleTo attribute targeting the CPUAccelerator assembly.
[assembly: InternalsVisibleTo("CPUAccelerator")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("f75d6fd6-fde8-4ee7-8462-92b2799ce31d")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
