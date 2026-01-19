// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using ILGPUC.CompilerService.Caching;
using ILGPUC.CompilerService.Jobs;

var builder = WebApplication.CreateBuilder(args);

// Use the parameterless constructor explicitly. CompilerManager has a
// primary ctor `(IEnumerable<ICompiler>)` which ASP.NET Core DI would
// otherwise pick — and DI happily resolves IEnumerable<T> to an empty
// collection when no T services are registered, leaving the manager with
// zero compilers. The parameterless ctor instead chains through
// CompilerOptions and constructs all five GPU compiler bindings.
builder.Services.AddSingleton<ICompilerManager>(_ => new CompilerManager());
builder.Services.AddSingleton<IJobQueue, JobQueue>();
builder.Services.AddSingleton<ICache, CompilationCache>();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();

if (builder.Environment.IsDevelopment())
    builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.Run();

// The synthesized Program class is internal in .NET 10 / C# 13.
// Integration tests use WebApplicationFactory<StatusController> as the
// assembly marker instead.
partial class Program { }
