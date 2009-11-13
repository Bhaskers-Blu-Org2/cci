﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Cci.Contracts;
using Microsoft.Cci.ILToCodeModel;

namespace PeToPe {
  class Program {
    static void Main(string[] args) {
      if (args == null || args.Length == 0) {
        Console.WriteLine("usage: PeToPe [path]fileName.ext [decompile]");
        return;
      }
      var host = new PeReader.DefaultHost();

      //Read the Metadata Model from the PE file
      var module = host.LoadUnitFrom(args[0]) as IModule;
      if (module == null || module == Dummy.Module || module == Dummy.Assembly) {
        Console.WriteLine(args[0]+" is not a PE file containing a CLR module or assembly.");
        return;
      }

      //Get a PDB reader if there is a PDB file.
      PdbReader/*?*/ pdbReader = null;
      string pdbFile = Path.ChangeExtension(module.Location, "pdb");
      if (File.Exists(pdbFile)) {
        Stream pdbStream = File.OpenRead(pdbFile);
        pdbReader = new PdbReader(pdbStream, host);
      }

      //Construct a Code Model from the Metadata model via decompilation
      module = Decompiler.GetCodeModelFromMetadataModel(host, module, pdbReader);
      
      //Create a mutator for the CodeModel and run it over the module, producing a copy that could be different if the mutator
      //were a subclass that made changes during the copy process.
      SourceToILConverterProvider sourceToILProvider =
        delegate(IMetadataHost host2, ISourceLocationProvider/*?*/ sourceLocationProvider, IContractProvider/*?*/ contractProvider2) {
          return new CodeModelToILConverter(host2, sourceLocationProvider, contractProvider2);
        };
      var mutator = new CodeMutator(host, sourceToILProvider, pdbReader);
      module = mutator.Visit(module);

      //Replace anonymous delegates and iterators with code that uses helper classes.
      CodeModelNormalizer cmn = new CodeModelNormalizer(host, true, sourceToILProvider, pdbReader, null);
      module = cmn.Visit(module);

      //Write out the normalized Code Model, traversing it as the Metadata Model it also is.
      //This lazily uses CodeModelToILConverter, via the delegate that the mutator stored in the method bodies, to compile method bodies to IL.
      Stream peStream = File.Create(module.Location + ".pe");
      if (pdbReader == null) {
        PeWriter.WritePeToStream(module, host, peStream);
      } else {
        using (var pdbWriter = new PdbWriter(module.Location + ".pdb", pdbReader)) {
          PeWriter.WritePeToStream(module, host, peStream, pdbReader, pdbReader, pdbWriter);
        }
      }
    }
  }

}
