using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace ILGPUwebCompiler
{
    public partial class Service
    {
        [JSImport("references", "main.js")]
        internal static partial byte[] GetReference(int n);
        [JSImport("totalFiles", "main.js")]
        internal static partial int GetAmmountOfReferences();

        [JSImport("fillOptimizationLevelDropDown", "main.js")]
        internal static partial void FillOptimizationLevelDropDown(string ol, int value);

        [JSImport("setOutput", "main.js")]
        internal static partial void SetOutput(string output);
    }
}
