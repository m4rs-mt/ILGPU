namespace ILGPU.Backends.SPIRV.Types
{
    internal interface ISPIRVType
    {
        SPIRVWord[] ToWords();

        string ToRepr();
    }
}
