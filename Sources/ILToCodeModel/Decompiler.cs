﻿//-----------------------------------------------------------------------------
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the Microsoft Public License.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using Microsoft.Cci.MutableCodeModel;
using System.IO;
using System.Diagnostics;

namespace Microsoft.Cci.ILToCodeModel {

  /// <summary>
  /// Provides methods that convert a given Metadata Model into an equivalent Code Model. 
  /// </summary>
  public static class Decompiler {

    /// <summary>
    /// Returns a mutable Code Model assembly that is equivalent to the given Metadata Model assembly,
    /// except that in the new assembly method bodies also implement ISourceMethodBody.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="assembly">The root of the Metadata Model to be converted to a Code Model.</param>
    /// <param name="pdbReader">An object that can map offsets in an IL stream to source locations and block scopes. May be null.</param>
    /// <param name="decompileIterators">True if iterator classes should be decompiled into iterator methods.</param>
    public static Assembly GetCodeModelFromMetadataModel(IMetadataHost host, IAssembly assembly, PdbReader/*?*/ pdbReader, bool decompileIterators = false) {
      return (Assembly)GetCodeModelFromMetadataModelHelper(host, assembly, pdbReader, pdbReader, decompileIterators);
    }

    /// <summary>
    /// Returns a mutable Code Model module that is equivalent to the given Metadata Model module,
    /// except that in the new module method bodies also implement ISourceMethodBody.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="module">The root of the Metadata Model to be converted to a Code Model.</param>
    /// <param name="pdbReader">An object that can map offsets in an IL stream to source locations and block scopes. May be null.</param>
    /// <param name="decompileIterators">True if iterator classes should be decompiled into iterator methods.</param>
    public static Module GetCodeModelFromMetadataModel(IMetadataHost host, IModule module, PdbReader/*?*/ pdbReader, bool decompileIterators = false) {
      return GetCodeModelFromMetadataModelHelper(host, module, pdbReader, pdbReader, decompileIterators);
    }

    /// <summary>
    /// Returns a (mutable) Code Model SourceMethod body that is equivalent to the given Metadata Model method body.
    /// It does *not* delete any helper types.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="methodBody">The Metadata Model method body that is to be decompiled.</param>
    /// <param name="pdbReader">An object that can map offsets in an IL stream to source locations and block scopes. May be null.</param>
    /// <param name="decompileIterators">True if iterator classes should be decompiled into iterator methods.</param>
    public static ISourceMethodBody GetCodeModelFromMetadataModel(IMetadataHost host, IMethodBody methodBody, PdbReader/*?*/ pdbReader, bool decompileIterators = false) {
      return new Microsoft.Cci.ILToCodeModel.SourceMethodBody(methodBody, host, pdbReader, pdbReader, decompileIterators);
    }

    /// <summary>
    /// Returns a mutable Code Model module that is equivalent to the given Metadata Model module,
    /// except that in the new module method bodies also implement ISourceMethodBody.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="module">The root of the Metadata Model to be converted to a Code Model.</param>
    /// <param name="sourceLocationProvider">An object that can map some kinds of ILocation objects to IPrimarySourceLocation objects. May be null.</param>
    /// <param name="localScopeProvider">An object that can provide information about the local scopes of a method. May be null.</param>
    /// <param name="decompileIterators">True if iterator classes should be decompiled into iterator methods.</param>
    private static Module GetCodeModelFromMetadataModelHelper(IMetadataHost host, IModule module,
      ISourceLocationProvider/*?*/ sourceLocationProvider, ILocalScopeProvider/*?*/ localScopeProvider, bool decompileIterators) {
      var result = new MetadataDeepCopier(host).Copy(module);
      var replacer = new ReplaceMetadataMethodBodiesWithDecompiledMethodBodies(host, module, sourceLocationProvider, localScopeProvider, decompileIterators);
      replacer.Traverse(result);
      var finder = new HelperTypeFinder(host, sourceLocationProvider);
      finder.Traverse(result);
      var remover = new RemoveUnnecessaryTypes(finder.helperTypes, finder.helperMethods, finder.helperFields);
      remover.Traverse(result);
      result.AllTypes.RemoveAll(td => finder.helperTypes.ContainsKey(td.InternedKey)); // depends on RemoveAll preserving order
      return result;
    }

  }

  /// <summary>
  /// A mutator that copies metadata models into mutable code models by using the base MetadataMutator class to make a mutable copy
  /// of a given metadata model and also replaces any method bodies with instances of SourceMethodBody, which implements the ISourceMethodBody.Block property
  /// by decompiling the metadata model information provided by the properties of IMethodBody.
  /// </summary>
  internal class ReplaceMetadataMethodBodiesWithDecompiledMethodBodies : MetadataTraverser {

    /// <summary>
    /// An object that can provide information about the local scopes of a method. May be null. 
    /// </summary>
    ILocalScopeProvider/*?*/ localScopeProvider;

    /// <summary>
    /// An object that can map offsets in an IL stream to source locations and block scopes. May be null.
    /// </summary>
    ISourceLocationProvider/*?*/ sourceLocationProvider;

    /// <summary>
    /// An object representing the application that is hosting this mutator. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.
    /// </summary>
    IMetadataHost host;

    /// <summary>
    /// True if iterator classes should be decompiled into iterator methods.
    /// </summary>
    bool decompileIterators;

    /// <summary>
    /// Allocates a mutator that copies metadata models into mutable code models by using the base MetadataMutator class to make a mutable copy
    /// of a given metadata model and also replaces any method bodies with instances of SourceMethodBody, which implements the ISourceMethodBody.Block property
    /// by decompiling the metadata model information provided by the properties of IMethodBody.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this mutator. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="unit">The unit of metadata that will be mutated.</param>
    /// <param name="sourceLocationProvider">An object that can map some kinds of ILocation objects to IPrimarySourceLocation objects. May be null.</param>
    /// <param name="localScopeProvider">An object that can provide information about the local scopes of a method. May be null.</param>
    /// <param name="decompileIterators">True if iterator classes should be decompiled into iterator methods.</param>
    internal ReplaceMetadataMethodBodiesWithDecompiledMethodBodies(IMetadataHost host, IUnit unit,
      ISourceLocationProvider/*?*/ sourceLocationProvider, ILocalScopeProvider/*?*/ localScopeProvider, bool decompileIterators) {
      this.localScopeProvider = localScopeProvider;
      this.sourceLocationProvider = sourceLocationProvider;
      this.host = host;
      this.decompileIterators = decompileIterators;
    }

    /// <summary>
    /// Replaces the body of the given method with an equivalent instance of SourceMethod body, which in addition also implements ISourceMethodBody,
    /// which has the additional property, Block, which represents the corresponding Code Model for the method body.
    /// </summary>
    public override void TraverseChildren(IMethodDefinition method) {
      if (method.IsExternal || method.IsAbstract) return;
      ((MethodDefinition)method).Body = new SourceMethodBody(method.Body, this.host, this.sourceLocationProvider, this.localScopeProvider, this.decompileIterators);
    }

  }

  /// <summary>
  /// A traverser that visits every method body and collects together all of the private helper types of these bodies.
  /// </summary>
  internal sealed class HelperTypeFinder : MetadataTraverser {

    /// <summary>
    /// Contains an entry for every type that has been introduced by the compiler to hold the state of an anonymous delegate or of an iterator.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these types should be removed from member lists.
    /// They stick around as PrivateHelperTypes of the methods containing the iterators and anonymous delegates.
    /// </summary>
    internal Dictionary<uint, ITypeDefinition> helperTypes = new Dictionary<uint, ITypeDefinition>();

    /// <summary>
    /// Contains an entry for every method that has been introduced by the compiler in order to implement anonymous delegates.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these members should be removed from member lists.
    /// They stick around as PrivateHelperMembers of the methods containing the anonymous delegates.
    /// </summary>
    internal Dictionary<uint, IMethodDefinition> helperMethods = new Dictionary<uint, IMethodDefinition>();

    /// <summary>
    /// Contains an entry for every field that has been introduced by the compiler in order to implement anonymous delegates.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these members should be removed from member lists.
    /// They stick around as PrivateHelperMembers of the methods containing the anonymous delegates.
    /// </summary>
    internal Dictionary<IFieldDefinition, IFieldDefinition> helperFields = new Dictionary<IFieldDefinition, IFieldDefinition>();

    /// <summary>
    /// An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.
    /// </summary>
    IMetadataHost host;

    /// <summary>
    /// An object that can map some kinds of ILocation objects to IPrimarySourceLocation objects. May be null.
    /// </summary>
    ISourceLocationProvider/*?*/ sourceLocationProvider;

    /// <summary>
    /// A traverser that visits every method body and collects together all of the private helper types of these bodies.
    /// </summary>
    /// <param name="host">An object representing the application that is hosting this decompiler. It is used to obtain access to some global
    /// objects and services such as the shared name table and the table for interning references.</param>
    /// <param name="sourceLocationProvider">An object that can map some kinds of ILocation objects to IPrimarySourceLocation objects. May be null.</param>
    internal HelperTypeFinder(IMetadataHost host, ISourceLocationProvider/*?*/ sourceLocationProvider) {
      this.host = host;
      this.sourceLocationProvider = sourceLocationProvider;
      this.TraverseIntoMethodBodies = true;
    }

    /// <summary>
    /// Traverses only the namespace root of the given assembly, removing any type from the model that have the same
    /// interned key as one of the entries of this.typesToRemove.
    /// </summary>
    public override void TraverseChildren(IModule module) {
      this.Traverse(module.UnitNamespaceRoot);
    }

    /// <summary>
    /// Traverses only the nested types and methods and collects together all of the private helper types that are introduced by the compiler
    /// when methods that contain closures or iterators are compiled.
    /// </summary>
    public override void TraverseChildren(INamedTypeDefinition typeDefinition) {
      var mutableTypeDefinition = (NamedTypeDefinition)typeDefinition;
      foreach (ITypeDefinition nestedType in mutableTypeDefinition.NestedTypes)
        this.Traverse(nestedType);
      foreach (IMethodDefinition method in mutableTypeDefinition.Methods)
        this.Traverse(method);
    }

    /// <summary>
    /// Traverses only the (possibly missing) body of the method.
    /// </summary>
    /// <param name="method"></param>
    public override void TraverseChildren(IMethodDefinition method) {
      if (method.IsAbstract || method.IsExternal) return;
      this.Traverse(method.Body);
    }

    /// <summary>
    /// Records all of the helper types of the method body into this.helperTypes.
    /// </summary>
    /// <param name="methodBody"></param>
    public override void TraverseChildren(IMethodBody methodBody) {
      var mutableBody = (SourceMethodBody)methodBody;
      var block = mutableBody.Block; //force decompilation
      bool denormalize = false;
      if (mutableBody.privateHelperTypesToRemove != null) {
        denormalize = true;
        foreach (var helperType in mutableBody.privateHelperTypesToRemove)
          this.helperTypes.Add(helperType.InternedKey, helperType);
      }
      if (mutableBody.privateHelperMethodsToRemove != null) {
        denormalize = true;
        foreach (var helperMethod in mutableBody.privateHelperMethodsToRemove.Values)
          this.helperMethods.Add(helperMethod.InternedKey, helperMethod);
      }
      if (mutableBody.privateHelperFieldsToRemove != null) {
        denormalize = true;
        foreach (var helperField in mutableBody.privateHelperFieldsToRemove.Values)
          this.helperFields.Add(helperField, helperField);
      }
      if (denormalize) {
        var mutableMethod = (MethodDefinition)mutableBody.MethodDefinition;
        var denormalizedBody = new Microsoft.Cci.MutableCodeModel.SourceMethodBody(this.host, this.sourceLocationProvider);
        denormalizedBody.LocalsAreZeroed = mutableBody.LocalsAreZeroed;
        denormalizedBody.IsNormalized = false;
        denormalizedBody.Block = block;
        denormalizedBody.MethodDefinition = mutableMethod;
        mutableMethod.Body = denormalizedBody;
      }
    }

  }

  /// <summary>
  /// A traverser for a mutable code model that removes a specified set of types from the model.
  /// </summary>
  internal class RemoveUnnecessaryTypes : MetadataTraverser {

    /// <summary>
    /// Contains an entry for every type that has been introduced by the compiler to hold the state of an anonymous delegate or of an iterator.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these types should be removed from member lists.
    /// They stick around as PrivateHelperTypes of the methods containing the iterators and anonymous delegates.
    /// </summary>
    Dictionary<uint, ITypeDefinition> helperTypes;

    /// <summary>
    /// Contains an entry for every method that has been introduced by the compiler in order to implement anonymous delegates.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these members should be removed from member lists.
    /// They stick around as PrivateHelperMembers of the methods containing the anonymous delegates.
    /// </summary>
    Dictionary<uint, IMethodDefinition> helperMethods;

    /// <summary>
    /// Contains an entry for every field that has been introduced by the compiler in order to implement anonymous delegates.
    /// Since decompilation re-introduces the anonymous delegates and iterators, these members should be removed from member lists.
    /// They stick around as PrivateHelperMembers of the methods containing the anonymous delegates.
    /// </summary>
    Dictionary<IFieldDefinition, IFieldDefinition> helperFields;

    /// <summary>
    /// Allocates a traverser for a mutable code model that removes a specified set of types from the model.
    /// </summary>
    /// <param name="helperTypes">A dictionary whose keys are the interned keys of the types to remove from member lists.</param>
    /// <param name="helperMethods">A dictionary whose keys are the interned keys of the methods to remove from member lists.</param>
    /// <param name="helperFields">A dictionary whose keys are the interned keys of the methods to remove from member lists.</param>
    internal RemoveUnnecessaryTypes(Dictionary<uint, ITypeDefinition> helperTypes, Dictionary<uint, IMethodDefinition> helperMethods,
      Dictionary<IFieldDefinition, IFieldDefinition> helperFields) {
      this.helperTypes = helperTypes;
      this.helperMethods = helperMethods;
      this.helperFields = helperFields;
    }

    /// <summary>
    /// Traverses only the namespace root of the given assembly, removing any type from the model that have the same
    /// interned key as one of the entries of this.typesToRemove.
    /// </summary>
    public override void TraverseChildren(IModule module) {
      this.Traverse(module.UnitNamespaceRoot);
    }

    /// <summary>
    /// Traverses the specified type definition, removing any nested types that are compiler introduced private helper types
    /// for maintaining the state of closures and anonymous delegates.
    /// </summary>
    public override void TraverseChildren(INamedTypeDefinition typeDefinition) {
      var mutableTypeDefinition = (NamedTypeDefinition)typeDefinition;
      for (int i = 0; i < mutableTypeDefinition.NestedTypes.Count; i++) {
        var nestedType = mutableTypeDefinition.NestedTypes[i];
        if (this.helperTypes.ContainsKey(nestedType.InternedKey)) {
          mutableTypeDefinition.NestedTypes.RemoveAt(i);
          i--;
        } else
          this.Traverse(nestedType);
      }
      for (int i = 0; i < mutableTypeDefinition.Methods.Count; i++) {
        var helperMethod = mutableTypeDefinition.Methods[i];
        if (this.helperMethods.ContainsKey(helperMethod.InternedKey)) {
          mutableTypeDefinition.Methods.RemoveAt(i);
          i--;
        }
      }
      for (int i = 0; i < mutableTypeDefinition.Fields.Count; i++) {
        var helperField = mutableTypeDefinition.Fields[i];
        if (this.helperFields.ContainsKey(helperField)) {
          mutableTypeDefinition.Fields.RemoveAt(i);
          i--;
        }
      }
    }

  }

}