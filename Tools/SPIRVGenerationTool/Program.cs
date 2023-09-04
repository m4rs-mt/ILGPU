using SPIRVGenerationTool.Generators;
using System.Text.Json;
using SPIRVGenerationTool.Generators.Builder;
using SPIRVGenerationTool.Generators.Type;
using SPIRVGenerationTool.Grammar;

var pathToGrammar = args[0];
var outputDirectory = args[1];

var file = File.ReadAllText(pathToGrammar);
var grammar = JsonSerializer.Deserialize<SPIRVGrammar>(file,
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

if (grammar is null)
    throw new Exception("Grammar could not be parsed");

Preprocessor.Process(grammar);

var generators = new List<GeneratorBase>
{
    new BinaryBuilderGenerator(Path.Combine(outputDirectory, "BinarySPIRVBuilder.cs")),
    new InterfaceGenerator(Path.Combine(outputDirectory, "ISPIRVBuilder.cs")),
    new TypeGenerator(Path.Combine(outputDirectory, "Types", "SPIRVTypes.cs"))
};

foreach (var generator in generators)
{
    generator.GenerateCode(grammar);
    generator.WriteToFile();
}
