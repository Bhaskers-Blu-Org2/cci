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
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Cci.Contracts;
using Microsoft.Cci.MutableCodeModel;

namespace Microsoft.Cci.ILToCodeModel {

  /// <summary>
  /// A contract provider that layers on top of an existing contract provider and which
  /// takes into account the way contracts for abstract methods are represented
  /// when IL uses the Code Contracts library. Namely, the containing type of an abstract method has an
  /// attribute that points to a class of proxy methods which hold the contracts for the corresponding
  /// abstract method.
  /// This provider wraps an existing non-code-contracts-aware provider and caches to avoid recomputing
  /// whether a contract exists or not.
  /// </summary>
  public class CodeContractsContractExtractor : IContractExtractor {

    /// <summary>
    /// needed to be able to map the contracts from a contract class proxy method to an abstract method
    /// </summary>
    IMetadataHost host;
    /// <summary>
    /// The (non-aware) provider that was used to extract the contracts from the IL.
    /// </summary>
    IContractExtractor underlyingContractProvider;
    /// <summary>
    /// Used just to cache results to that the underlyingContractProvider doesn't have to get asked
    /// more than once.
    /// </summary>
    ContractProvider contractProviderCache;

    /// <summary>
    /// Creates a contract provider which is aware of how abstract methods have their contracts encoded.
    /// </summary>
    /// <param name="host">
    /// The host that was used to load the unit for which the <paramref name="underlyingContractProvider"/>
    /// is a provider for.
    /// </param>
    /// <param name="underlyingContractProvider">
    /// The (non-aware) provider that was used to extract the contracts from the IL.
    /// </param>
    public CodeContractsContractExtractor(IMetadataHost host, IContractExtractor underlyingContractProvider) {
      this.host = host;
      this.underlyingContractProvider = underlyingContractProvider;
      this.contractProviderCache = new ContractProvider(underlyingContractProvider.ContractMethods, underlyingContractProvider.Unit);
    }

    #region IContractProvider Members

    /// <summary>
    /// Returns the loop contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="loop">An object that might have been associated with a loop contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ILoopContract/*?*/ GetLoopContractFor(object loop) {
      return this.underlyingContractProvider.GetLoopContractFor(loop);
    }

    /// <summary>
    /// Returns the method contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="method">An object that might have been associated with a method contract. This can be any kind of object.</param>
    /// <returns></returns>
    public IMethodContract/*?*/ GetMethodContractFor(object method) {

      IMethodContract contract = this.contractProviderCache.GetMethodContractFor(method);
      if (contract != null) return contract == ContractDummy.MethodContract ? null : contract;

      IMethodReference methodReference = method as IMethodReference;
      if (methodReference == null) {
        this.contractProviderCache.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }
      IMethodDefinition methodDefinition = methodReference.ResolvedMethod;
      if (methodDefinition == Dummy.Method) {
        this.contractProviderCache.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }
      if (!methodDefinition.IsAbstract) {
        contract = this.underlyingContractProvider.GetMethodContractFor(method);
        if (contract != null) {
          return contract;
        } else {
          this.contractProviderCache.AssociateMethodWithContract(method, ContractDummy.MethodContract);
          return null;
        }
      }

      // But if it is an abstract method, then check to see if its containing type points to a class holding the contract
      IMethodDefinition/*?*/ proxyMethod = ContractHelper.GetMethodFromContractClass(methodDefinition);
      if (proxyMethod == null) {
        this.contractProviderCache.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }
      contract = this.underlyingContractProvider.GetMethodContractFor(proxyMethod);
      if (contract == null) return null;
      SubstituteParameters sps = new SubstituteParameters(this.host, methodDefinition, proxyMethod);
      MethodContract modifiedContract = sps.Visit(contract) as MethodContract;
      this.contractProviderCache.AssociateMethodWithContract(methodDefinition, modifiedContract);
      return modifiedContract;
    }

    /// <summary>
    /// Returns the triggers, if any, that have been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="quantifier">An object that might have been associated with triggers. This can be any kind of object.</param>
    /// <returns></returns>
    public IEnumerable<IEnumerable<IExpression>>/*?*/ GetTriggersFor(object quantifier) {
      return this.underlyingContractProvider.GetTriggersFor(quantifier);
    }

    /// <summary>
    /// Returns the type contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="type">An object that might have been associated with a type contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ITypeContract/*?*/ GetTypeContractFor(object type) {
      return this.underlyingContractProvider.GetTypeContractFor(type);
    }

    /// <summary>
    /// A collection of methods that can be called in a way that provides tools with information about contracts.
    /// </summary>
    /// <value></value>
    public IContractMethods/*?*/ ContractMethods {
      get { return this.underlyingContractProvider.ContractMethods; }
    }

    /// <summary>
    /// The unit that this is a contract provider for. Intentional design:
    /// no provider works on more than one unit.
    /// </summary>
    /// <value></value>
    public IUnit/*?*/ Unit {
      get { return this.underlyingContractProvider.Unit; }
    }

    #endregion

    #region IContractExtractor Members

    /// <summary>
    /// Delegate callback to underlying contract extractor.
    /// </summary>
    public void RegisterContractProviderCallback(IContractProviderCallback contractProviderCallback) {
      this.underlyingContractProvider.RegisterContractProviderCallback(contractProviderCallback);
    }

    /// <summary>
    /// For a client (e.g., the decompiler) that has a source method body and wants to have its
    /// contract extracted and added to the contract provider.
    /// </summary>
    public MethodContractAndMethodBody SplitMethodBodyIntoContractAndCode(ISourceMethodBody sourceMethodBody) {
      return this.underlyingContractProvider.SplitMethodBodyIntoContractAndCode(sourceMethodBody);
    }

    #endregion
  }

  /// <summary>
  /// A contract provider that can be used to get contracts from a unit by querying in
  /// a random-access manner. That is, the unit is *not* traversed eagerly.
  /// </summary>
  public class LazyContractExtractor : IContractExtractor, IDisposable {

    /// <summary>
    /// Needed because the decompiler requires the concrete class ContractProvider
    /// </summary>
    ContractProvider underlyingContractProvider;
    /// <summary>
    /// needed to pass to decompiler
    /// </summary>
    IContractAwareHost host;
    /// <summary>
    /// needed to pass to decompiler
    /// </summary>
    private PdbReader/*?*/ pdbReader;
    /// <summary>
    /// Objects interested in getting the method body after extraction.
    /// </summary>
    List<IContractProviderCallback> callbacks = new List<IContractProviderCallback>();

    private IUnit unit; // the module this is a lazy provider for

    /// <summary>
    /// Allocates an object that can be used to query for contracts by asking questions about specific methods/types, etc.
    /// </summary>
    /// <param name="host">The host that loaded the unit for which this is to be a contract provider.</param>
    /// <param name="unit">The unit to retrieve the contracts from.</param>
    /// <param name="contractMethods">A collection of methods that can be called in a way that provides tools with information about contracts.</param>
    /// <param name="usePdb">Whether to use the PDB file (and possibly the source files if available) during extraction.</param>
    public LazyContractExtractor(IContractAwareHost host, IUnit unit, IContractMethods contractMethods, bool usePdb) {
      this.host = host;
      this.underlyingContractProvider = new ContractProvider(contractMethods, unit);
      if (usePdb) {
        string pdbFile = Path.ChangeExtension(unit.Location, "pdb");
        if (File.Exists(pdbFile)) {
          using (var pdbStream = File.OpenRead(pdbFile)) {
            this.pdbReader = new PdbReader(pdbStream, host);
          }
        }
      }
      this.unit = unit;
    }

    /// <summary>
    /// Disposes the PdbReader object, if any, that is used to obtain the source text locations corresponding to contracts.
    /// </summary>
    public void Dispose() {
      this.Close();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the PdbReader object, if any, that is used to obtain the source text locations corresponding to contracts.
    /// </summary>
    ~LazyContractExtractor() {
       this.Close();
    }

    private void Close() {
      if (this.pdbReader != null)
        this.pdbReader.Dispose();
    }

    /// <summary>
    /// The unit that this is a contract provider for. Intentional design:
    /// no provider works on more than one unit.
    /// </summary>
    /// <value></value>
    public IUnit Unit { get { return this.unit; } }

    /// <summary>
    /// Gets the host.
    /// </summary>
    /// <value>The host.</value>
    public IMetadataHost Host { get { return this.host; } }

    #region IContractProvider Members

    /// <summary>
    /// Returns the loop contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="loop">An object that might have been associated with a loop contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ILoopContract/*?*/ GetLoopContractFor(object loop) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the method contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="method">An object that might have been associated with a method contract. This can be any kind of object.</param>
    /// <returns></returns>
    public IMethodContract/*?*/ GetMethodContractFor(object method) {

      IMethodContract contract = this.underlyingContractProvider.GetMethodContractFor(method);
      if (contract != null) return contract == ContractDummy.MethodContract ? null : contract;

      IMethodReference methodReference = method as IMethodReference;
      if (methodReference == null) {
        this.underlyingContractProvider.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }

      IMethodDefinition methodDefinition = methodReference.ResolvedMethod;
      if (methodDefinition == Dummy.Method) {
        this.underlyingContractProvider.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }

      if (methodDefinition.IsAbstract || methodDefinition.IsExternal) { // precondition of Body getter
        this.underlyingContractProvider.AssociateMethodWithContract(method, ContractDummy.MethodContract);
        return null;
      }

      IMethodBody methodBody = methodDefinition.Body;
      ISourceMethodBody/*?*/ sourceMethodBody = methodBody as ISourceMethodBody;
      if (sourceMethodBody == null) {
        sourceMethodBody = new SourceMethodBody(methodBody, this.host, this.pdbReader, this.pdbReader);
      }

      MethodContractAndMethodBody result = this.SplitMethodBodyIntoContractAndCode(sourceMethodBody);
      var methodContract = result.MethodContract;
      if (methodContract == null) {
        this.underlyingContractProvider.AssociateMethodWithContract(method, ContractDummy.MethodContract); // so we don't try to extract more than once
      } else {
        this.underlyingContractProvider.AssociateMethodWithContract(method, methodContract);
      }

      // Notify all interested parties
      foreach (var c in this.callbacks) {
        c.ProvideResidualMethodBody(methodDefinition, result.BlockStatement);
      }

      return methodContract;
    }

    /// <summary>
    /// Returns the triggers, if any, that have been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="quantifier">An object that might have been associated with triggers. This can be any kind of object.</param>
    /// <returns></returns>
    public IEnumerable<IEnumerable<IExpression>>/*?*/ GetTriggersFor(object quantifier) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the type contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="type">An object that might have been associated with a type contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ITypeContract/*?*/ GetTypeContractFor(object type) {

      ITypeContract/*?*/ typeContract = this.underlyingContractProvider.GetTypeContractFor(type);
      if (typeContract != null) return typeContract == ContractDummy.TypeContract ? null : typeContract;

      ITypeReference/*?*/ typeReference = type as ITypeReference;
      if (typeReference == null) {
        this.underlyingContractProvider.AssociateTypeWithContract(type, ContractDummy.TypeContract);
        return null;
      }

      ITypeDefinition/*?*/ typeDefinition = typeReference.ResolvedType;
      if (typeDefinition == null) {
        this.underlyingContractProvider.AssociateTypeWithContract(type, ContractDummy.TypeContract);
        return null;
      }

      TypeContract cumulativeContract = new TypeContract();
      foreach (var invariantMethod in ContractHelper.GetInvariantMethods(typeDefinition)) {
        IMethodBody methodBody = invariantMethod.Body;
        ISourceMethodBody/*?*/ sourceMethodBody = methodBody as ISourceMethodBody;
        if (sourceMethodBody == null) {
          sourceMethodBody = new SourceMethodBody(methodBody, this.host, this.pdbReader, this.pdbReader);
        }
        var e = new ContractExtractor(sourceMethodBody, this.host, this.pdbReader);
        var tc = e.ExtractObjectInvariant(sourceMethodBody.Block);
        if (tc != null) {
          cumulativeContract.Invariants.AddRange(tc.Invariants);
        }
      }

      if (cumulativeContract.Invariants.Count == 0) {
        this.underlyingContractProvider.AssociateTypeWithContract(type, ContractDummy.TypeContract); // so we don't try to extract more than once
        return null;
      } else {
        this.underlyingContractProvider.AssociateTypeWithContract(type, cumulativeContract);
        return cumulativeContract;
      }
    }

    /// <summary>
    /// A collection of methods that can be called in a way that provides tools with information about contracts.
    /// </summary>
    /// <value></value>
    public IContractMethods ContractMethods {
      get { return this.underlyingContractProvider.ContractMethods; }
    }

    #endregion

    #region IContractExtractor Members

    /// <summary>
    /// After the callback has been registered, when a contract is extracted
    /// from a method, the callback will be notified.
    /// </summary>
    public void RegisterContractProviderCallback(IContractProviderCallback contractProviderCallback) {
      this.callbacks.Add(contractProviderCallback);
    }

    /// <summary>
    /// For a client (e.g., the decompiler) that has a source method body and wants to have its
    /// contract extracted and added to the contract provider.
    /// </summary>
    public MethodContractAndMethodBody SplitMethodBodyIntoContractAndCode(ISourceMethodBody sourceMethodBody) {
      var e = new ContractExtractor(sourceMethodBody, this.host, this.pdbReader);
      return e.SplitMethodBodyIntoContractAndCode(sourceMethodBody.Block);
    }

    #endregion
  }

  /// <summary>
  /// Not used for now.
  /// </summary>
  internal class MethodMapper : CodeAndContractMutator {
    IMethodDefinition targetMethod;
    IMethodDefinition sourceMethod;
    public MethodMapper(IMetadataHost host, IMethodDefinition targetMethod, IMethodDefinition sourceMethod)
      : base(host, true) {
      this.targetMethod = targetMethod;
      this.sourceMethod = sourceMethod;
    }
    public override object GetMutableCopyIfItExists(IParameterDefinition parameterDefinition) {
      if (parameterDefinition.ContainingSignature == sourceMethod) {
        var ps = new List<IParameterDefinition>(targetMethod.Parameters);
        return ps[parameterDefinition.Index];
      }
      return base.GetMutableCopyIfItExists(parameterDefinition);
    }
  }

  /// <summary>
  /// A mutator that changes all references defined in one unit into being
  /// references defined in another unit.
  /// It does so by substituting the target unit's identity for the source
  /// unit's identity whenever it visits a unit reference.
  /// Other than that, it overrides all visit methods that visit things which could be either
  /// a reference or a definition. The base class does *not* visit definitions
  /// when they are being seen as references because it assumes that the definitions
  /// will get visited during a top-down visit of the unit. But this visitor can
  /// be used on just small snippets of trees. A side effect is that all definitions
  /// are replaced by references so it doesn't preserve that aspect of the object model.
  /// </summary>
  internal class MappingMutator : CodeAndContractMutator {

    private IUnit sourceUnit = null;
    private UnitIdentity sourceUnitIdentity;
    private IUnit targetUnit = null;

    /// <summary>
    /// A mutator that, when it visits anything, converts any references defined in the <paramref name="sourceUnit"/>
    /// into references defined in the <paramref name="targetUnit"/>
    /// </summary>
    /// <param name="host">
    /// The host that loaded the <paramref name="targetUnit"/>
    /// </param>
    /// <param name="targetUnit">
    /// The unit to which all references in the <paramref name="sourceUnit"/>
    /// will mapped.
    /// </param>
    /// <param name="sourceUnit">
    /// The unit from which references will be mapped into references from the <paramref name="targetUnit"/>
    /// </param>
    public MappingMutator(IMetadataHost host, IUnit targetUnit, IUnit sourceUnit)
      : base(host, false) { // NB!! Must make this mutator *always* copy (i.e., pass false to the base ctor) or it screws up ASTs that shouldn't be changed
      this.sourceUnit = sourceUnit;
      this.sourceUnitIdentity = sourceUnit.UnitIdentity;
      this.targetUnit = targetUnit;
    }

    #region Units
    public override IUnitReference Visit(IUnitReference unitReference) {
      if (unitReference.UnitIdentity.Equals(this.sourceUnitIdentity))
        return targetUnit;
      else
        return base.Visit(unitReference);
    }
    #endregion Units

    #region Namespaces
    public override IRootUnitNamespaceReference Visit(IRootUnitNamespaceReference rootUnitNamespaceReference) {
      return this.Visit(this.GetMutableCopy(rootUnitNamespaceReference));
    }
    public override INestedUnitNamespaceReference Visit(INestedUnitNamespaceReference nestedUnitNamespaceReference) {
      return this.Visit(this.GetMutableCopy(nestedUnitNamespaceReference));
    }
    #endregion Namespaces

    #region Types
    public override INamespaceTypeReference Visit(INamespaceTypeReference namespaceTypeReference) {
      return this.Visit(this.GetMutableCopy(namespaceTypeReference));
    }
    public override INestedTypeReference Visit(INestedTypeReference nestedTypeReference) {
      return this.Visit(this.GetMutableCopy(nestedTypeReference));
    }
    public override IGenericTypeParameterReference Visit(IGenericTypeParameterReference genericTypeParameterReference) {
      return this.Visit(this.GetMutableCopy(genericTypeParameterReference));
    }
    public override IGenericMethodParameterReference Visit(IGenericMethodParameterReference genericMethodParameterReference) {
      return this.Visit(this.GetMutableCopy(genericMethodParameterReference));
    }
    #endregion Types

    #region Methods
    public override IMethodReference Visit(IMethodReference methodReference) {
      return this.Visit(this.GetMutableCopy(methodReference));
    }
    #endregion Methods

    #region Fields
    public override IFieldReference Visit(IFieldReference fieldReference) {
      return this.Visit(this.GetMutableCopy(fieldReference));
    }
    #endregion Fields

  }

  /// <summary>
  /// A contract extractor that serves up the union of the contracts found from a set of contract extractors.
  /// One extractor is the primary extractor: all contracts retrieved from this contract extractor are expressed
  /// in terms of the types/members as defined by that extractor's unit. Optionally, a set of secondary extractors
  /// are used to query for contracts on equivalent methods/types: any contracts found are transformed into
  /// being contracts expressed over the types/members as defined by the primary provider and additively
  /// merged into the contracts from the primary extractor.
  /// </summary>
  public class AggregatingContractExtractor : IContractExtractor, IDisposable {

    private IUnit unit;
    private IContractExtractor primaryExtractor;
    private List<IContractProvider> oobExtractors;
    ContractProvider underlyingContractProvider; // used just because it provides a store so this provider can cache its results
    IMetadataHost host;
    private Dictionary<IContractProvider, MappingMutator> mapperForOobToPrimary = new Dictionary<IContractProvider, MappingMutator>();
    private Dictionary<IContractProvider, MappingMutator> mapperForPrimaryToOob = new Dictionary<IContractProvider, MappingMutator>();

    private List<object> methodsBeingExtracted = new List<object>();

    /// <summary>
    /// The constructor for creating an aggregating extractor.
    /// </summary>
    /// <param name="host">This is the host that loaded the unit for which the <paramref name="primaryProvider"/> is
    /// the extractor for.
    /// </param>
    /// <param name="primaryExtractor">
    /// The extractor that will be used to define the types/members of things referred to in contracts.
    /// </param>
    /// <param name="oobExtractorsAndHosts">
    /// These are optional. If non-null, then it must be a finite sequence of pairs: each pair is a contract extractor
    /// and the host that loaded the unit for which it is a extractor.
    /// </param>
    public AggregatingContractExtractor(IMetadataHost host, IContractExtractor primaryExtractor, IEnumerable<KeyValuePair<IContractProvider, IMetadataHost>>/*?*/ oobExtractorsAndHosts) {
      var primaryUnit = primaryExtractor.Unit;
      this.unit = primaryUnit;
      this.primaryExtractor = primaryExtractor;

      this.underlyingContractProvider = new ContractProvider(primaryExtractor.ContractMethods, primaryUnit);
      this.host = host;

      if (oobExtractorsAndHosts != null) {
        this.oobExtractors = new List<IContractProvider>();
        foreach (var oobProviderAndHost in oobExtractorsAndHosts) {
          var oobProvider = oobProviderAndHost.Key;
          var oobHost = oobProviderAndHost.Value;
          this.oobExtractors.Add(oobProvider);
          IUnit oobUnit = oobProvider.Unit;
          this.mapperForOobToPrimary.Add(oobProvider, new MappingMutator(host, primaryUnit, oobUnit));
          this.mapperForPrimaryToOob.Add(oobProvider, new MappingMutator(oobHost, oobUnit, primaryUnit));
        }
      }
    }

    /// <summary>
    /// Disposes any constituent contract providers that implement the IDisposable interface.
    /// </summary>
    public void Dispose() {
      this.Close();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes any constituent contract providers that implement the IDisposable interface. 
    /// </summary>
    ~AggregatingContractExtractor() {
       this.Close();
    }

    private void Close() {
      var primaryDisposable = this.primaryExtractor as IDisposable;
      if (primaryDisposable != null) primaryDisposable.Dispose();
      foreach (var oobProvider in this.oobExtractors) {
        var oobDisposable = oobProvider as IDisposable;
        if (oobDisposable != null)
          oobDisposable.Dispose();
      }
    }

    #region IContractProvider Members

    /// <summary>
    /// Returns the loop contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="loop">An object that might have been associated with a loop contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ILoopContract/*?*/ GetLoopContractFor(object loop) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the method contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="method">An object that might have been associated with a method contract. This can be any kind of object.</param>
    /// <returns></returns>
    public IMethodContract/*?*/ GetMethodContractFor(object method) {

      if (this.methodsBeingExtracted.Contains(method)) {
        // hit a cycle while chasing validators/abbreviators
        // TODO: signal error
        return null;
      } else {
        this.methodsBeingExtracted.Add(method);
      }

      try {

        IMethodContract contract = this.underlyingContractProvider.GetMethodContractFor(method);
        if (contract != null) return contract == ContractDummy.MethodContract ? null : contract;

        MethodContract result = new MethodContract();
        IMethodContract primaryContract = this.primaryExtractor.GetMethodContractFor(method);
        bool found = false;
        if (primaryContract != null) {
          found = true;
          Microsoft.Cci.Contracts.ContractHelper.AddMethodContract(result, primaryContract);
        }
        if (this.oobExtractors != null) {
          foreach (var oobProvider in this.oobExtractors) {

            IMethodReference methodReference = method as IMethodReference;
            if (methodReference == null) continue; // REVIEW: Is there anything else it could be and still find a contract for it?

            MappingMutator primaryToOobMapper = this.mapperForPrimaryToOob[oobProvider];
            var oobMethod = primaryToOobMapper.Visit(methodReference);

            if (oobMethod == null) continue;

            var oobContract = oobProvider.GetMethodContractFor(oobMethod);

            if (oobContract == null) continue;

            MappingMutator oobToPrimaryMapper = this.mapperForOobToPrimary[oobProvider];
            oobContract = oobToPrimaryMapper.Visit(oobContract);
            Microsoft.Cci.Contracts.ContractHelper.AddMethodContract(result, oobContract);
            found = true;

          }
        }

        // always cache so we don't try to extract more than once
        if (found) {
          this.underlyingContractProvider.AssociateMethodWithContract(method, result);
        } else {
          this.underlyingContractProvider.AssociateMethodWithContract(method, ContractDummy.MethodContract);
          result = null;
        }
        return result;
      } finally {
        this.methodsBeingExtracted.RemoveAt(this.methodsBeingExtracted.Count - 1);
      }
    }

    /// <summary>
    /// Returns the triggers, if any, that have been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="quantifier">An object that might have been associated with triggers. This can be any kind of object.</param>
    /// <returns></returns>
    public IEnumerable<IEnumerable<IExpression>>/*?*/ GetTriggersFor(object quantifier) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the type contract, if any, that has been associated with the given object. Returns null if no association exits.
    /// </summary>
    /// <param name="type">An object that might have been associated with a type contract. This can be any kind of object.</param>
    /// <returns></returns>
    public ITypeContract/*?*/ GetTypeContractFor(object type) {

      ITypeContract contract = this.underlyingContractProvider.GetTypeContractFor(type);
      if (contract != null) return contract == ContractDummy.TypeContract ? null : contract;

      TypeContract result = new TypeContract();
      ITypeContract primaryContract = this.primaryExtractor.GetTypeContractFor(type);
      bool found = false;
      if (primaryContract != null) {
        found = true;
        ContractHelper.AddTypeContract(result, primaryContract);
      }
      if (this.oobExtractors != null) {
        foreach (var oobProvider in this.oobExtractors) {
          var oobUnit = oobProvider.Unit;

          ITypeReference typeReference = type as ITypeReference;
          if (typeReference == null) continue; // REVIEW: Is there anything else it could be and still find a contract for it?

          MappingMutator primaryToOobMapper = this.mapperForPrimaryToOob[oobProvider];
          var oobType = primaryToOobMapper.Visit(typeReference);

          if (oobType == null) continue;

          var oobContract = oobProvider.GetTypeContractFor(oobType);

          if (oobContract == null) continue;

          MappingMutator oobToPrimaryMapper = this.mapperForOobToPrimary[oobProvider];
          oobContract = oobToPrimaryMapper.Visit(oobContract);
          ContractHelper.AddTypeContract(result, oobContract);
          found = true;

        }
      }

      // always cache so we don't try to extract more than once
      if (found) {
        this.underlyingContractProvider.AssociateTypeWithContract(type, result);
        return result;
      } else {
        this.underlyingContractProvider.AssociateTypeWithContract(type, ContractDummy.TypeContract);
        return null;
      }

    }

    /// <summary>
    /// A collection of methods that can be called in a way that provides tools with information about contracts.
    /// </summary>
    /// <value></value>
    public IContractMethods/*?*/ ContractMethods {
      get { return this.underlyingContractProvider.ContractMethods; }
    }

    /// <summary>
    /// The unit that this is a contract provider for. Intentional design:
    /// no provider works on more than one unit.
    /// </summary>
    /// <value></value>
    public IUnit/*?*/ Unit {
      get { return this.unit; }
    }

    #endregion

    #region IContractExtractor Members

    /// <summary>
    /// Delegate to the primary provider
    /// </summary>
    /// <param name="contractProviderCallback"></param>
    public void RegisterContractProviderCallback(IContractProviderCallback contractProviderCallback) {
      this.primaryExtractor.RegisterContractProviderCallback(contractProviderCallback);
    }

    /// <summary>
    /// For a client (e.g., the decompiler) that has a source method body and wants to have its
    /// contract extracted and added to the contract provider.
    /// </summary>
    public MethodContractAndMethodBody SplitMethodBodyIntoContractAndCode(ISourceMethodBody sourceMethodBody) {
      return this.primaryExtractor.SplitMethodBodyIntoContractAndCode(sourceMethodBody);
    }

    #endregion
  }

}