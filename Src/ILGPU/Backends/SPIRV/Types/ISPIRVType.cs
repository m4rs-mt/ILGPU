namespace ILGPU.Backends.SPIRV.Types
{
    public interface ISPIRVType
    {
        SPIRVWord[] ToWords();

        string ToRepr();
    }
}
