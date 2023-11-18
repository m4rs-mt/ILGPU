# ILGPU.Analyzers

A set of custom Roslyn analyzers for ILGPU to make the experience smoother and more beginner friendly.

## Content
### ILGPU.Analyzers
Includes the code for the actual analyzers.
**You must build this project to see the results (warnings) in the IDE.**

### ILGPU.Analyzers.Sample
A project that references the above project and provides samples for the types of analyses an analyzer may produce.
It's also helpful for development and debugging.

### ILGPU.Analyzers.Tests
Unit tests.

## How To?
### How to debug?
- Use the [launchSettings.json](Properties/launchSettings.json) profile to debug samples.
- Debug tests as usual.
