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
using Microsoft.Cci.MutableCodeModel.Contracts;

namespace Microsoft.Cci.MutableContracts {

  /// <summary>
  /// Helper class for performing common tasks on mutable contracts
  /// </summary>
  public class ContractHelper {

    /// <summary>
    /// Accumulates all elements from <paramref name="sourceContract"/> into <paramref name="targetContract"/>
    /// </summary>
    /// <param name="targetContract">Contract which is target of accumulator</param>
    /// <param name="sourceContract">Contract which is source of accumulator</param>
    public static void AddMethodContract(MethodContract targetContract, IMethodContract sourceContract) {
      targetContract.Locations.AddRange(sourceContract.Locations);
      targetContract.Preconditions.AddRange(sourceContract.Preconditions);
      targetContract.Postconditions.AddRange(sourceContract.Postconditions);
      targetContract.ThrownExceptions.AddRange(sourceContract.ThrownExceptions);
      targetContract.IsPure |= sourceContract.IsPure; // need the disjunction
      return;
    }

    /// <summary>
    /// Mutates the given <paramref name="module"/> by injecting calls to contract methods at the
    /// beginnning of any method in the <paramref name="module"/> that has a corresponding contract
    /// in the <paramref name="contractProvider"/>. It also creates a contract invariant method
    /// for each type in <paramref name="module"/> that has a type contract in the
    /// <paramref name="contractProvider"/>.
    /// </summary>
    public static void InjectContractCalls(IMetadataHost host, Module module, ContractProvider contractProvider, ISourceLocationProvider sourceLocationProvider) {
      var cci = new ContractCallInjector(host, contractProvider, sourceLocationProvider);
      cci.Rewrite(module);
    }

    private class ContractCallInjector : CodeAndContractRewriter {

      private readonly ITypeReference systemVoid;
      private ISourceLocationProvider sourceLocationProvider;

      public ContractCallInjector(IMetadataHost host, ContractProvider contractProvider, ISourceLocationProvider sourceLocationProvider)
        : base(host, contractProvider) {
        this.systemVoid = host.PlatformType.SystemVoid;
        this.sourceLocationProvider = sourceLocationProvider;
      }

      private static List<T> MkList<T>(T t) { var xs = new List<T>(); xs.Add(t); return xs; }

      public override INamespaceTypeDefinition Rewrite(INamespaceTypeDefinition namespaceTypeDefinition) {
        this.VisitTypeDefinition(namespaceTypeDefinition);
        return base.Rewrite(namespaceTypeDefinition);
      }

      public override INestedTypeDefinition Rewrite(INestedTypeDefinition nestedTypeDefinition) {
        this.VisitTypeDefinition(nestedTypeDefinition);
        return base.Rewrite(nestedTypeDefinition);
      }

      /// <summary>
      /// If the <paramref name="typeDefinition"/> has a type contract, generate a
      /// contract invariant method and add it to the Methods of the <paramref name="typeDefinition"/>.
      /// </summary>
      private void VisitTypeDefinition(ITypeDefinition typeDefinition) {
        ITypeContract typeContract = this.contractProvider.GetTypeContractFor(typeDefinition);
        if (typeContract != null) {
          #region Define the method
          List<IStatement> statements = new List<IStatement>();
          var methodBody = new SourceMethodBody(this.host, null, null) {
            LocalsAreZeroed = true,
            Block = new BlockStatement() { Statements = statements }
          };
          List<ICustomAttribute> attributes = new List<ICustomAttribute>();
          MethodDefinition m = new MethodDefinition() {
            Attributes = attributes,
            Body = methodBody,
            CallingConvention = CallingConvention.HasThis,
            ContainingTypeDefinition = typeDefinition,
            InternFactory = this.host.InternFactory,
            IsStatic = false,
            Name = this.host.NameTable.GetNameFor("$InvariantMethod$"),
            Type = systemVoid,
            Visibility = TypeMemberVisibility.Private,
          };
          methodBody.MethodDefinition = m;
          #region Add calls to Contract.Invariant
          foreach (var inv in typeContract.Invariants) {
            var methodCall = new MethodCall() {
              Arguments = MkList(inv.Condition),
              IsStaticCall = true,
              MethodToCall = this.contractProvider.ContractMethods.Invariant,
              Type = systemVoid,
              Locations = new List<ILocation>(inv.Locations),
            };
            ExpressionStatement es = new ExpressionStatement() {
              Expression = methodCall
            };
            statements.Add(es);
          }
          statements.Add(new ReturnStatement());
          #endregion
          #region Add [ContractInvariantMethod]
          var contractInvariantMethodType = new Immutable.NamespaceTypeReference(
            this.host,
            this.host.PlatformType.SystemDiagnosticsContractsContract.ContainingUnitNamespace,
            this.host.NameTable.GetNameFor("ContractInvariantMethodAttribute"),
            0,
            false,
            false,
            PrimitiveTypeCode.NotPrimitive
            );
          var contractInvariantMethodCtor = new Microsoft.Cci.MutableCodeModel.MethodReference() {
            CallingConvention = CallingConvention.HasThis,
            ContainingType = contractInvariantMethodType,
            GenericParameterCount = 0,
            InternFactory = this.host.InternFactory,
            Name = host.NameTable.Ctor,
            Type = host.PlatformType.SystemVoid,
          };
          var contractInvariantMethodAttribute = new CustomAttribute();
          contractInvariantMethodAttribute.Constructor = contractInvariantMethodCtor;
          attributes.Add(contractInvariantMethodAttribute);
          #endregion
          var namedTypeDefinition = (NamedTypeDefinition)typeDefinition;
          if (namedTypeDefinition.Methods == null) namedTypeDefinition.Methods = new List<IMethodDefinition>(1);
          namedTypeDefinition.Methods.Add(m);
          #endregion Define the method
        }
      }

      public override void RewriteChildren(MethodDefinition methodDefinition) {
        IMethodContract methodContract = this.contractProvider.GetMethodContractFor(methodDefinition);
        if (methodContract == null) return;
        ISourceMethodBody sourceMethodBody = methodDefinition.Body as ISourceMethodBody;
        if (sourceMethodBody == null) return;
        List<IStatement> contractStatements = new List<IStatement>();
        foreach (var precondition in methodContract.Preconditions) {
          var methodCall = new MethodCall() {
            Arguments = MkList(precondition.Condition),
            IsStaticCall = true,
            MethodToCall = this.contractProvider.ContractMethods.Requires,
            Type = systemVoid,
            Locations = new List<ILocation>(precondition.Locations),
          };
          ExpressionStatement es = new ExpressionStatement() {
            Expression = methodCall
          };
          contractStatements.Add(es);
        }
        foreach (var postcondition in methodContract.Postconditions) {
          var methodCall = new MethodCall() {
            Arguments = MkList(this.Rewrite(postcondition.Condition)),
            IsStaticCall = true,
            MethodToCall = this.contractProvider.ContractMethods.Ensures,
            Type = systemVoid,
            Locations = new List<ILocation>(postcondition.Locations),
          };
          ExpressionStatement es = new ExpressionStatement() {
            Expression = methodCall
          };
          contractStatements.Add(es);
        }
        List<IStatement> existingStatements = new List<IStatement>(sourceMethodBody.Block.Statements);
        existingStatements = this.Rewrite(existingStatements);
        // keep the call to the base constructor at the top
        if (methodDefinition.IsConstructor && existingStatements.Count > 0) {
          contractStatements.Insert(0, existingStatements[0]);
          existingStatements.RemoveAt(0);
        }
        contractStatements.AddRange(existingStatements); // replaces assert/assume
        var newSourceMethodBody = new SourceMethodBody(this.host, this.sourceLocationProvider) {
          Block = new BlockStatement() {
            Statements = contractStatements,
          },
          IsNormalized = false,
          LocalsAreZeroed = sourceMethodBody.LocalsAreZeroed,
          MethodDefinition = methodDefinition,
        };
        methodDefinition.Body = newSourceMethodBody;
        return;
      }

      /// <summary>
      /// Converts the assert statement into a call to Contract.Assert
      /// </summary>
      public override IStatement Rewrite(IAssertStatement assertStatement) {
        var methodCall = new MethodCall() {
          Arguments = MkList(assertStatement.Condition),
          IsStaticCall = true,
          MethodToCall = this.contractProvider.ContractMethods.Assert,
          Type = systemVoid,
          Locations = new List<ILocation>(assertStatement.Locations),
        };
        ExpressionStatement es = new ExpressionStatement() {
          Expression = methodCall
        };
        return es;
      }

      /// <summary>
      /// Converts the assume statement into a call to Contract.Assume
      /// </summary>
      public override IStatement Rewrite(IAssumeStatement assumeStatement) {
        var methodCall = new MethodCall() {
          Arguments = MkList(assumeStatement.Condition),
          IsStaticCall = true,
          MethodToCall = this.contractProvider.ContractMethods.Assume,
          Type = systemVoid,
          Locations = new List<ILocation>(assumeStatement.Locations),
        };
        ExpressionStatement es = new ExpressionStatement() {
          Expression = methodCall
        };
        return es;
      }

      /// <summary>
      /// Converts the old value into a call to Contract.OldValue
      /// </summary>
      public override IExpression Rewrite(IOldValue oldValue) {

        var mref = this.contractProvider.ContractMethods.Old;
        var methodToCall = new Microsoft.Cci.MutableCodeModel.GenericMethodInstanceReference() {
          CallingConvention = CallingConvention.Generic,
          ContainingType = mref.ContainingType,
          GenericArguments = new List<ITypeReference> { oldValue.Type },
          GenericMethod = mref,
          InternFactory = this.host.InternFactory,
          Name = mref.Name,
          Parameters = new List<IParameterTypeInformation>{
            new ParameterTypeInformation { Type = oldValue.Type, }
          },
          Type = oldValue.Type,
        };
        var methodCall = new MethodCall() {
          Arguments = MkList(oldValue.Expression),
          IsStaticCall = true,
          MethodToCall = methodToCall,
          Type = oldValue.Type,
          Locations = new List<ILocation>(oldValue.Locations),
        };
        return methodCall;
      }

      /// <summary>
      /// Converts the return value into a call to Contract.Result
      /// </summary>
      public override IExpression Rewrite(IReturnValue returnValue) {

        var mref = this.contractProvider.ContractMethods.Result;
        var methodToCall = new Microsoft.Cci.MutableCodeModel.GenericMethodInstanceReference() {
          CallingConvention = CallingConvention.Generic,
          ContainingType = mref.ContainingType,
          GenericArguments = new List<ITypeReference> { returnValue.Type },
          GenericMethod = mref,
          InternFactory = this.host.InternFactory,
          Name = mref.Name,
          Type = returnValue.Type,
        };
        var methodCall = new MethodCall() {
          IsStaticCall = true,
          MethodToCall = methodToCall,
          Type = returnValue.Type,
          Locations = new List<ILocation>(returnValue.Locations),
        };
        return methodCall;
      }

    }

    /// <summary>
    /// Accumulates all elements from <paramref name="sourceContract"/> into <paramref name="targetContract"/>
    /// </summary>
    /// <param name="targetContract">Contract which is target of accumulator</param>
    /// <param name="sourceContract">Contract which is source of accumulator</param>
    public static void AddTypeContract(TypeContract targetContract, ITypeContract sourceContract) {
      targetContract.ContractFields.AddRange(sourceContract.ContractFields);
      targetContract.ContractMethods.AddRange(sourceContract.ContractMethods);
      targetContract.Invariants.AddRange(sourceContract.Invariants);
      return;
    }

    /// <summary>
    /// First, uninstantiates the method if it is a reference to a generic method.
    /// Then, if the resulting method reference is specialized, the unspecialized
    /// version is returned.
    /// </summary>
    public static IMethodReference UninstantiateAndUnspecialize(IMethodReference method) {
      IMethodReference result = method;
      IGenericMethodInstanceReference gmir = result as IGenericMethodInstanceReference;
      if (gmir != null) {
        result = gmir.GenericMethod;
      }
      ISpecializedMethodReference smr = result as ISpecializedMethodReference;
      if (smr != null) {
        result = smr.UnspecializedVersion;
      }
      return result;
    }

    public static IMethodDefinition UninstantiateAndUnspecializeMethodDefinition(IMethodDefinition method) {
      IMethodDefinition result = method;
      var gmi = result as IGenericMethodInstance;
      if (gmi != null)
        result = gmi.GenericMethod.ResolvedMethod;
      var smd = result as ISpecializedMethodDefinition;
      if (smd != null)
        result = smd.UnspecializedVersion;
      return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static ITypeReference Unspecialized(ITypeReference type) {
      var instance = type as IGenericTypeInstanceReference;
      if (instance != null) {
        var gt = instance.GenericType;
        var spec = gt as ISpecializedNestedTypeReference;
        if (spec != null) return spec.UnspecializedVersion;
        return gt;
      }
      var sntr = type as ISpecializedNestedTypeReference;
      if (sntr != null) return sntr.UnspecializedVersion;
      return type;
    }

    /// <summary>
    /// Returns the first string argument in the constructor in a custom attribute.
    /// I.e., [A("x")]
    /// </summary>
    /// <param name="attributes">The list of attributes that will be searched</param>
    /// <param name="attributeName">Name of the attribute.</param>
    /// <returns></returns>
    public static string/*?*/ GetStringArgumentFromAttribute(IEnumerable<ICustomAttribute> attributes, string attributeName) {
      ICustomAttribute foundAttribute = null;
      foreach (ICustomAttribute attribute in attributes) {
        if (TypeHelper.GetTypeName(attribute.Type) == attributeName) {
          foundAttribute = attribute;
          break;
        }
      }
      if (foundAttribute == null) return null;
      var args = new List<IMetadataExpression>(foundAttribute.Arguments);
      if (args.Count < 1) return null;
      var mdc = args[0] as IMetadataConstant;
      if (mdc == null) return null;
      string arg = mdc.Value as string;
      return arg;
    }

    /// <summary>
    /// Returns a type definition for a type referenced in a custom attribute.
    /// </summary>
    /// <param name="typeDefinition">The type definition whose attributes will be searched</param>
    /// <param name="attributeName">Name of the attribute.</param>
    /// <returns></returns>
    public static INamedTypeDefinition/*?*/ GetTypeDefinitionFromAttribute(ITypeDefinition typeDefinition, string attributeName) {
      ICustomAttribute foundAttribute = null;
      foreach (ICustomAttribute attribute in typeDefinition.Attributes) {
        if (TypeHelper.GetTypeName(attribute.Type) == attributeName) {
          foundAttribute = attribute;
          break;
        }
      }
      if (foundAttribute == null) return null;
      List<IMetadataExpression> args = new List<IMetadataExpression>(foundAttribute.Arguments);
      if (args.Count < 1) return null;
      IMetadataTypeOf abstractTypeMD = args[0] as IMetadataTypeOf;
      if (abstractTypeMD == null) return null;
      ITypeReference referencedTypeReference = Unspecialized(abstractTypeMD.TypeToGet);
      ITypeDefinition referencedTypeDefinition = referencedTypeReference.ResolvedType;
      return referencedTypeDefinition as INamedTypeDefinition;
    }

    /// <summary>
    /// Given an abstract method (i.e., either an interface method, J.M, or else
    /// an abstract method M), see if its defining type is marked with the
    /// [ContractClass(typeof(T))] attribute. If not, then return null.
    /// If it is marked with the attribute, then:
    /// 1) If J is an interface: return the method from T that implements M
    /// (i.e., either T.J.M if T explicitly implements J.M or else the method
    /// T.M that is used as the implicit interface implementation of J.M).
    /// 2) Otherwise, the method from T that is an override of M.
    /// </summary>
    public static IMethodDefinition/*?*/ GetMethodFromContractClass(IMetadataHost host, IMethodDefinition methodDefinition) {

      //var unspecializedMethodDefinition = UninstantiateAndUnspecialize(methodDefinition);
      //ITypeDefinition definingType = unspecializedMethodDefinition.ResolvedMethod.ContainingTypeDefinition;

      var definingType = methodDefinition.ContainingTypeDefinition;

      ITypeDefinition typeHoldingContractDefinition = GetTypeDefinitionFromAttribute(definingType, "System.Diagnostics.Contracts.ContractClassAttribute");
      if (typeHoldingContractDefinition == null) return null;
      if (definingType.IsInterface) {
        var unspecializedMethodDefinition = UninstantiateAndUnspecialize(methodDefinition);
        definingType = unspecializedMethodDefinition.ResolvedMethod.ContainingTypeDefinition;
        #region Explicit Interface Implementations
        foreach (IMethodImplementation methodImplementation in typeHoldingContractDefinition.ExplicitImplementationOverrides) {
          var implementedInterfaceMethod = UninstantiateAndUnspecialize(methodImplementation.ImplementedMethod);
          if (unspecializedMethodDefinition.InternedKey == implementedInterfaceMethod.InternedKey)
              return methodImplementation.ImplementingMethod.ResolvedMethod;
        }
        #endregion Explicit Interface Implementations
        #region Implicit Interface Implementations
        var implicitImplementations = typeHoldingContractDefinition.GetMatchingMembers(
          tdm => {
            IMethodDefinition md = tdm as IMethodDefinition;
            if (md == null) return false;
            if (md.Name != methodDefinition.Name) return false;
            return ImplicitlyImplementsInterfaceMethod(md, methodDefinition);
          });
        if (IteratorHelper.EnumerableIsNotEmpty(implicitImplementations))
          return IteratorHelper.Single(implicitImplementations) as IMethodDefinition;
        #endregion Implicit Interface Implementations
        return null;
      } else if (methodDefinition.IsAbstract) {
        var smd = methodDefinition as ISpecializedMethodDefinition;
        if (smd != null) {
          // need to instantiate the type holding the contracts to match the instantiation of the containing type of the method
          var gti = (IGenericTypeInstanceReference)smd.ContainingType;
          typeHoldingContractDefinition = Immutable.GenericTypeInstance.GetGenericTypeInstance((INamedTypeDefinition)typeHoldingContractDefinition, gti.GenericArguments, host.InternFactory);
          
        }
        IMethodDefinition method = MemberHelper.GetImplicitlyOverridingDerivedClassMethod(methodDefinition, typeHoldingContractDefinition);
        var implicitImplementations = typeHoldingContractDefinition.GetMatchingMembers(
          tdm => {
            IMethodDefinition md = tdm as IMethodDefinition;
            if (md == null) return false;
            if (md.Name != methodDefinition.Name) return false;
            return true;
          });
        if (IteratorHelper.EnumerableIsNotEmpty(implicitImplementations)) {
          return IteratorHelper.First(implicitImplementations) as IMethodDefinition;
        }
        if (method is Dummy) return null;
        return method;
      }
      return null;
    }

    private static bool ImplicitlyImplementsInterfaceMethod(IMethodDefinition contractClassMethod, IMethodDefinition ifaceMethod) {
      foreach (var ifm in ContractHelper.GetAllImplicitlyImplementedInterfaceMethods(contractClassMethod)) {
        var unspec = UninstantiateAndUnspecialize(ifm);
        if (MemberHelper.MethodsAreEquivalent(unspec.ResolvedMethod, ifaceMethod)) return true;
      }
      return false;
    }

    /// <summary>
    /// Given an method, M, see if it is declared in a type that is holding a contract class, i.e.,
    /// it will be marked with [ContractClassFor(typeof(T))]. If so, then return T.M, else null.
    /// </summary>
    /// <param name="methodDefinition"></param>
    /// <returns></returns>
    public static IMethodDefinition/*?*/ GetAbstractMethodForContractMethod(IMethodDefinition methodDefinition) {
      ITypeDefinition definingType = methodDefinition.ContainingTypeDefinition;
      var abstractTypeDefinition = GetTypeDefinitionFromAttribute(definingType, "System.Diagnostics.Contracts.ContractClassForAttribute");
      if (abstractTypeDefinition == null) return null;
      if (abstractTypeDefinition.IsInterface) {
        foreach (IMethodReference methodReference in MemberHelper.GetExplicitlyOverriddenMethods(methodDefinition)) {
          return methodReference.ResolvedMethod;
        }
      } else if (abstractTypeDefinition.IsAbstract) {
        IMethodDefinition method = MemberHelper.GetImplicitlyOverriddenBaseClassMethod(methodDefinition);
        if (method is Dummy) return null;
        return method;
      }
      return null;
    }

    /// <summary>
    /// Returns the first method found in <paramref name="typeDefinition"/> containing an instance of 
    /// an attribute with the name "ContractInvariantMethodAttribute", if it exists.
    /// </summary>
    /// <param name="typeDefinition">The type whose members will be searched</param>
    /// <returns>May return null if not found</returns>
    public static IEnumerable<IMethodDefinition> GetInvariantMethods(ITypeDefinition typeDefinition) {
      foreach (IMethodDefinition methodDef in typeDefinition.Methods)
        foreach (var attr in methodDef.Attributes) {
          INamespaceTypeReference ntr = attr.Type as INamespaceTypeReference;
          if (ntr != null && ntr.Name.Value == "ContractInvariantMethodAttribute")
            yield return methodDef;
        }
    }

    /// <summary>
    /// Creates a type reference anchored in the given assembly reference and whose names are relative to the given host.
    /// When the type name has periods in it, a structured reference with nested namespaces is created.
    /// </summary>
    public static INamespaceTypeReference CreateTypeReference(IMetadataHost host, IAssemblyReference assemblyReference, string typeName) {
      IUnitNamespaceReference ns = new Immutable.RootUnitNamespaceReference(assemblyReference);
      string[] names = typeName.Split('.');
      for (int i = 0, n = names.Length - 1; i < n; i++)
        ns = new Immutable.NestedUnitNamespaceReference(ns, host.NameTable.GetNameFor(names[i]));
      return new Immutable.NamespaceTypeReference(host, ns, host.NameTable.GetNameFor(names[names.Length - 1]), 0, false, false, PrimitiveTypeCode.NotPrimitive);
    }

    /// <summary>
    /// C# uses CompilerGenerated, VB uses DebuggerNonUserCode
    /// </summary>
    public static bool IsAutoPropertyMember(IMetadataHost host, ITypeDefinitionMember member) {
      if (AttributeHelper.Contains(member.Attributes, host.PlatformType.SystemRuntimeCompilerServicesCompilerGeneratedAttribute)) return true;
      var systemDiagnosticsDebuggerNonUserCodeAttribute = CreateTypeReference(host, new Immutable.AssemblyReference(host, host.ContractAssemblySymbolicIdentity), "System.Diagnostics.DebuggerNonUserCodeAttribute");
      return AttributeHelper.Contains(member.Attributes, systemDiagnosticsDebuggerNonUserCodeAttribute);
    }

    /// <summary>
    /// Returns true iff the type definition is a contract class for an interface or abstract class.
    /// </summary>
    public static bool IsContractClass(IMetadataHost host, ITypeDefinition typeDefinition) {
      var contractClassFor = CreateTypeReference(host, new Immutable.AssemblyReference(host, host.ContractAssemblySymbolicIdentity), "System.Diagnostics.Contracts.ContractClassForAttribute");
      return AttributeHelper.Contains(typeDefinition.Attributes, contractClassFor);
    }

    /// <summary>
    /// Returns true iff the method definition is an invariant method.
    /// </summary>
    public static bool IsInvariantMethod(IMetadataHost host, IMethodDefinition methodDefinition) {
      var contractInvariantMethod = CreateTypeReference(host, new Immutable.AssemblyReference(host, host.ContractAssemblySymbolicIdentity), "System.Diagnostics.Contracts.ContractInvariantMethodAttribute");
      return AttributeHelper.Contains(methodDefinition.Attributes, contractInvariantMethod);
    }

    /// <summary>
    /// Returns true iff the method resolves to a definition which is decorated
    /// with an attribute named either "ContractArgumentValidatorAttribute"
    /// or "ContractAbbreviatorAttribute".
    /// The namespace the attribute belongs to is ignored.
    /// </summary>
    public static bool IsValidatorOrAbbreviator(IMethodReference method) {
      return (IsAbbreviator(method) || IsValidator(method));
    }

    /// <summary>
    /// Returns true iff the method resolves to a definition which is decorated
    /// with an attribute named "ContractAbbreviatorAttribute".
    /// The namespace the attribute belongs to is ignored.
    /// </summary>
    public static bool IsAbbreviator(IMethodReference method) {
      IMethodDefinition methodDefinition = method.ResolvedMethod;
      if (methodDefinition is Dummy) return false;
      foreach (var a in methodDefinition.Attributes) {
        string name = TypeHelper.GetTypeName(a.Type, NameFormattingOptions.None);
        if (name.EndsWith("ContractAbbreviatorAttribute"))
          return true;
      }
      return false;
    }
    /// <summary>
    /// Returns true iff the method resolves to a definition which is decorated
    /// with an attribute named  "ContractArgumentValidatorAttribute".
    /// The namespace the attribute belongs to is ignored.
    /// </summary>
    public static bool IsValidator(IMethodReference method) {
      IMethodDefinition methodDefinition = method.ResolvedMethod;
      if (methodDefinition is Dummy) return false;
      foreach (var a in methodDefinition.Attributes) {
        string name = TypeHelper.GetTypeName(a.Type, NameFormattingOptions.None);
        if (name.EndsWith("ContractArgumentValidatorAttribute"))
          return true;
      }
      return false;
    }

    /// <summary>
    /// Returns the attribute iff the field resolves to a definition which is decorated
    /// with an attribute named "ContractModelAttribute".
    /// The namespace the attribute belongs to is ignored.
    /// </summary>
    public static ICustomAttribute/*?*/ IsModel(IFieldDefinition fieldDefinition) {
      if (fieldDefinition is Dummy) return null;
      ICustomAttribute/*?*/ attr;
      TryGetAttributeByName(fieldDefinition.Attributes, "ContractModelAttribute", out attr);
      return attr;
    }
    /// <summary>
    /// Returns the attribute iff the method resolves to a definition which is decorated
    /// with an attribute named "ContractModelAttribute".
    /// The namespace the attribute belongs to is ignored.
    /// </summary>
    public static ICustomAttribute/*?*/ IsModel(IMethodReference method) {
      var mr = UninstantiateAndUnspecialize(method);
      IMethodDefinition methodDefinition = mr.ResolvedMethod;
      if (methodDefinition is Dummy) return null;
      ICustomAttribute/*?*/ attr;
      if (TryGetAttributeByName(methodDefinition.Attributes, "ContractModelAttribute", out attr)) return attr;
      // TODO: Need to cache this information. This is an expensive way to tell if something is a getter.
      bool isPropertyGetter = methodDefinition.IsSpecialName && methodDefinition.Name.Value.StartsWith("get_");
      if (isPropertyGetter) {
        foreach (var p in methodDefinition.ContainingTypeDefinition.Properties)
          if (p.Getter != null && p.Getter.ResolvedMethod == methodDefinition) {
            if (TryGetAttributeByName(p.Attributes, "ContractModelAttribute", out attr)) {
              return attr;
            }
          }
      }
      return null;
    }

    public static bool IsPure(IMetadataHost host, IMethodReference methodReference) {
      var contractAssemblyReference = new Immutable.AssemblyReference(host, host.ContractAssemblySymbolicIdentity);
      var pureAttribute = ContractHelper.CreateTypeReference(host, contractAssemblyReference, "System.Diagnostics.Contracts.PureAttribute"); 
      if (AttributeHelper.Contains(methodReference.Attributes, pureAttribute)) return true;
      var methodDefinition = methodReference.ResolvedMethod;
      return methodDefinition != methodReference && AttributeHelper.Contains(methodDefinition.Attributes, pureAttribute);
    }

    private static bool TryGetAttributeByName(IEnumerable<ICustomAttribute> attributes, string attributeTypeName, out ICustomAttribute/*?*/ attribute) {
      attribute = null;
      foreach (ICustomAttribute a in attributes) {
        if (TypeHelper.GetTypeName(a.Type).EndsWith(attributeTypeName)) {
          attribute = a;
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Indicates when the unit is marked with the assembly-level attribute
    /// [System.Diagnostics.Contracts.ContractReferenceAssembly]
    /// where that attribute type is defined in the unit itself.
    /// </summary>
    public static bool IsContractReferenceAssembly(IMetadataHost host, IUnit unit) {
      IAssemblyReference ar = unit as IAssemblyReference;
      if (ar == null) return false;
      var declAttr = CreateTypeReference(host, ar, "System.Diagnostics.Contracts.ContractReferenceAssemblyAttribute");
      return AttributeHelper.Contains(unit.Attributes, declAttr);
    }

    /// <summary>
    /// Returns a (possibly-null) method contract relative to a contract-aware host.
    /// If the method is instantiated or specialized, then the contract is looked
    /// for on the uninstantiated and unspecialized method.
    /// Note that this behavior is *not* necessarily present in any individual
    /// contract provider.
    /// However, if you already know which unit the method is defined in and/or
    /// already have the contract provider for the unit in which the method is
    /// defined, and you know the method is uninstantiated and unspecialized,
    /// then you would do just as well to directly query that contract provider.
    /// </summary>
    public static IMethodContract GetMethodContractFor(IContractAwareHost host, IMethodDefinition methodDefinition) {

      if (methodDefinition is Dummy) return null;
      var u = TypeHelper.GetDefiningUnitReference(methodDefinition.ContainingType);
      if (u == null) return null;
      var cp = host.GetContractExtractor(u.UnitIdentity);
      if (cp == null) return null;
      var mc = cp.GetMethodContractFor(methodDefinition);
      return mc;
    }

    /// <summary>
    /// Returns a method contract containing the 'effective' contract for the given
    /// method definition. The effective contract contains all contracts for the method:
    /// any that it has on its own, as well as all those inherited from any methods
    /// that it overrides or interface methods that it implements (either implicitly
    /// or explicitly).
    /// All parameters in inherited contracts are substituted for by
    /// the method's own parameters.
    /// If there are no contracts, then it returns null.
    /// </summary>
    public static IMethodContract GetMethodContractForIncludingInheritedContracts(IContractAwareHost host, IMethodDefinition methodDefinition) {
      MethodContract cumulativeContract = new MethodContract();
      bool atLeastOneContract = false;
      IMethodContract/*?*/ mc = ContractHelper.GetMethodContractFor(host, methodDefinition);
      if (mc != null) {
        Microsoft.Cci.MutableContracts.ContractHelper.AddMethodContract(cumulativeContract, mc);
        atLeastOneContract = true;
      }
      #region Overrides of base class methods
      if (!methodDefinition.IsNewSlot) { // REVIEW: Is there a better test?
        IMethodDefinition overriddenMethod = MemberHelper.GetImplicitlyOverriddenBaseClassMethod(methodDefinition) as IMethodDefinition;
        while (overriddenMethod != null && !(overriddenMethod is Dummy)) {
          IMethodContract/*?*/ overriddenContract = ContractHelper.GetMethodContractFor(host, overriddenMethod);
          if (overriddenContract != null) {

            overriddenContract = CopyContractIntoNewContext(host, overriddenContract, methodDefinition, overriddenMethod);
            overriddenContract = FilterUserMessage(host, overriddenContract, methodDefinition.ContainingTypeDefinition);

            // if the method is generic, then need to specialize the contract to have the same method type parameters as the method
            if (methodDefinition.IsGeneric) {
              var d = new Dictionary<uint, ITypeReference>();
              IteratorHelper.Zip(overriddenMethod.GenericParameters, methodDefinition.GenericParameters, (i,j) => d.Add(i.InternedKey, j));
              var cs = new CodeSpecializer(host, d);
              overriddenContract = cs.Rewrite(overriddenContract);
            }

            Microsoft.Cci.MutableContracts.ContractHelper.AddMethodContract(cumulativeContract, overriddenContract);
            atLeastOneContract = true;
          }
          overriddenMethod = MemberHelper.GetImplicitlyOverriddenBaseClassMethod(overriddenMethod) as IMethodDefinition;
        }
      }
      #endregion Overrides of base class methods
      #region Implicit interface implementations
      foreach (IMethodDefinition ifaceMethod in ContractHelper.GetAllImplicitlyImplementedInterfaceMethods(methodDefinition)) {
        IMethodContract/*?*/ ifaceContract = ContractHelper.GetMethodContractFor(host, ifaceMethod);
        if (ifaceContract == null) continue;
        ifaceContract = CopyContractIntoNewContext(host, ifaceContract, methodDefinition, ifaceMethod);
        ifaceContract = FilterUserMessage(host, ifaceContract, methodDefinition.ContainingTypeDefinition);
        Microsoft.Cci.MutableContracts.ContractHelper.AddMethodContract(cumulativeContract, ifaceContract);
        atLeastOneContract = true;
      }
      #endregion Implicit interface implementations
      #region Explicit interface implementations and explicit method overrides
      foreach (IMethodReference ifaceMethodRef in MemberHelper.GetExplicitlyOverriddenMethods(methodDefinition)) {
        IMethodDefinition/*?*/ ifaceMethod = ifaceMethodRef.ResolvedMethod;
        if (ifaceMethod == null) continue;
        IMethodContract/*?*/ ifaceContract = ContractHelper.GetMethodContractFor(host, ifaceMethod);
        if (ifaceContract == null) continue;
        ifaceContract = CopyContractIntoNewContext(host, ifaceContract, methodDefinition, ifaceMethod);
        ifaceContract = FilterUserMessage(host, ifaceContract, methodDefinition.ContainingTypeDefinition);
        Microsoft.Cci.MutableContracts.ContractHelper.AddMethodContract(cumulativeContract, ifaceContract);
        atLeastOneContract = true;
      }
      #endregion Explicit interface implementations and explicit method overrides
      return atLeastOneContract ? cumulativeContract : null;
    }

    private static MethodContract FilterUserMessage(IContractAwareHost host, IMethodContract contract, ITypeDefinition typeDefinition) {
      var mc = new MethodContract(contract);
      mc.Postconditions = FilterUserMessage(host, contract.Postconditions, typeDefinition);
      mc.Preconditions = FilterUserMessage(host, contract.Preconditions, typeDefinition);
      mc.ThrownExceptions = FilterUserMessage(host, contract.ThrownExceptions, typeDefinition);
      return mc;
    }

    private static List<IPostcondition> FilterUserMessage(IContractAwareHost host, IEnumerable<IPostcondition> postconditions, ITypeDefinition typeDefinition) {
      int n = (int)IteratorHelper.EnumerableCount(postconditions);
      var filteredPostconditions = new IPostcondition[n];
      var i = 0;
      foreach (var p in postconditions) {
        if (CanAccess(p.Description, typeDefinition)) {
          filteredPostconditions[i] = p;
        } else {
          var newP = new Postcondition(p);
          newP.Description = null;
          filteredPostconditions[i] = newP;
        }
        i++;
      }
      return new List<IPostcondition>(filteredPostconditions);
    }
    private static List<IPrecondition> FilterUserMessage(IContractAwareHost host, IEnumerable<IPrecondition> preconditions, ITypeDefinition typeDefinition) {
      int n = (int)IteratorHelper.EnumerableCount(preconditions);
      var filteredPreconditions = new IPrecondition[n];
      var i = 0;
      foreach (var p in preconditions) {
        if (CanAccess(p.Description, typeDefinition)) {
          filteredPreconditions[i] = p;
        } else {
          var newP = new Precondition(p);
          newP.Description = null;
          filteredPreconditions[i] = newP;
        }
        i++;
      }
      return new List<IPrecondition>(filteredPreconditions);
    }
    private static List<IThrownException> FilterUserMessage(IContractAwareHost host, IEnumerable<IThrownException> thrownExceptions, ITypeDefinition typeDefinition) {
      int n = (int)IteratorHelper.EnumerableCount(thrownExceptions);
      var filteredThrownExceptions = new IThrownException[n];
      var i = 0;
      foreach (var te in thrownExceptions) {
        if (CanAccess(te.Postcondition.Description, typeDefinition)) {
          filteredThrownExceptions[i] = te;
        } else {
          var newP = new Postcondition(te.Postcondition);
          newP.Description = null;
          filteredThrownExceptions[i] = new ThrownException() {
            ExceptionType = te.ExceptionType,
            Postcondition = newP,
          };
        }
        i++;
      }
      return new List<IThrownException>(filteredThrownExceptions);
    }
    private static bool CanAccess(IExpression expression, ITypeDefinition typeDefinition) {
      if (expression is ICompileTimeConstant) return true;
      IMethodCall mc = expression as IMethodCall;
      if (mc != null) return TypeHelper.CanAccess(typeDefinition, mc.MethodToCall.ResolvedMethod);
      IFieldReference fr = expression as IFieldReference;
      if (fr != null) return TypeHelper.CanAccess(typeDefinition, fr.ResolvedField);
      // there shouldn't be anything else other than a method call or field reference, but if
      // there is, just return false. (TODO: see if this causes a silent error)
      return false;
    }

    /// <summary>
    /// Given a method contract (<paramref name="methodContact"/> for a unspecialized/uninstantiated method reference/definition
    /// (<paramref name="unspec"/>), specialize and instantiate (i.e., the generics) in the contract so that it is a contract
    /// relative to the specialized/instantiated method reference/definition (<paramref name="mr"/>).
    /// </summary>
    /// <returns>
    /// A deep copy of <paramref name="methodContract"/>, properly specialized and instantiated.
    /// </returns>
    public static MethodContract InstantiateAndSpecializeContract(IMetadataHost host, IMethodContract methodContract, IMethodReference mr, IMethodReference unspec) {
      //Contract.Requires(mr is IGenericMethodInstanceReference || mr is ISpecializedMethodReference);

      var copier = new CodeAndContractDeepCopier(host);
      var mutableContract = copier.Copy(methodContract);

      var unspecializedTypeReferences = new List<ITypeReference>();
      var specializedTypeReferences = new List<ITypeReference>();

      var copyOfUnspecializedContainingType = copier.Copy(unspec.ContainingType);
      unspecializedTypeReferences.Add(copyOfUnspecializedContainingType);
      var containingType = mr.ContainingType;
      var gtir2 = containingType as IGenericTypeInstanceReference;
      var copyOfSpecializedContainingType = gtir2 != null ? copier.Copy(gtir2) : copier.Copy(containingType);
      specializedTypeReferences.Add(copyOfSpecializedContainingType);

      var unspecializedMethodDefinition = unspec as IMethodDefinition;

      #region Map generic type parameters
      var smr = mr as ISpecializedMethodReference;
      if (smr != null) {
        if (unspecializedMethodDefinition != null) {
          foreach (var t in unspecializedMethodDefinition.ContainingTypeDefinition.GenericParameters) {
            // without the cast, the copy is not object equals to references within the contract
            var copyOfT = copier.Copy((IGenericTypeParameterReference)t);
            unspecializedTypeReferences.Add(copyOfT);
          }
          var gtir = smr.ContainingType as IGenericTypeInstanceReference;
          if (gtir != null) {
            foreach (var t in gtir.GenericArguments) {
              var copyOfT = copier.Copy(t);
              specializedTypeReferences.Add(copyOfT);
            }
          }
        }
      }
      #endregion
      #region Map generic method parameters
      var gmir = mr as IGenericMethodInstanceReference;
      if (gmir != null) {
        var unspecialized = gmir.GenericMethod.ResolvedMethod;
        foreach (var t in unspecialized.GenericParameters) {
          var copyOfT = copier.Copy(t);
          unspecializedTypeReferences.Add(copyOfT);
        }
        foreach (var t in gmir.GenericArguments) {
          var copyOfT = copier.Copy(t);
          specializedTypeReferences.Add(copyOfT);
        }
      }
      #endregion
      var d = new Dictionary<uint, ITypeReference>();
      IteratorHelper.Zip(unspecializedTypeReferences, specializedTypeReferences, (u, s) => d.Add(u.InternedKey, s));
      var specializer = new CodeSpecializer(host, d);
      
      mutableContract = (MethodContract)specializer.Rewrite(mutableContract);
      mutableContract = (MethodContract)ContractHelper.CopyContractIntoNewContext(host, mutableContract, smr != null ? smr.ResolvedMethod : mr.ResolvedMethod, unspecializedMethodDefinition);
      return mutableContract;
    }

    /// <summary>
    /// Given a mutable module that is a "declarative" module, i.e., it has contracts expressed as contract calls
    /// at the beginning of method bodies, this method will extract them, leaving the method bodies without those
    /// calls and return a contract provider for the module containing any extracted contracts.
    /// </summary>
    public static ContractProvider ExtractContracts(IContractAwareHost host, Module module, PdbReader/*?*/ pdbReader, ILocalScopeProvider/*?*/ localScopeProvider) {
      var contractMethods = new ContractMethods(host);
      var cp = new Microsoft.Cci.MutableContracts.ContractProvider(contractMethods, module);
      var extractor = new SeparateContractsFromCode(host, pdbReader, localScopeProvider, cp);
      extractor.Traverse(module);
      return cp;
    }

    /// <summary>
    /// A traverser that extracts contracts from methods and updates a given contract provider with these contracts.
    /// </summary>
    private class SeparateContractsFromCode : CodeTraverser {

      private ContractProvider contractProvider;
      private PdbReader/*?*/ pdbReader;
      private ILocalScopeProvider/*?*/ localScopeProvider;
      private IContractAwareHost contractAwareHost;

      internal SeparateContractsFromCode(IContractAwareHost host, PdbReader/*?*/ pdbReader, ILocalScopeProvider/*?*/ localScopeProvider, ContractProvider contractProvider) {
        this.contractAwareHost = host;
        this.pdbReader = pdbReader;
        this.localScopeProvider = localScopeProvider;
        this.contractProvider = contractProvider;
        this.TraverseIntoMethodBodies = true;
      }

      public override void TraverseChildren(ITypeDefinition typeDefinition) {
        var contract = ContractExtractor.GetTypeContract(this.contractAwareHost, typeDefinition, this.pdbReader, this.localScopeProvider);
        if (contract != null) {
          this.contractProvider.AssociateTypeWithContract(typeDefinition, contract);
        }
        base.TraverseChildren(typeDefinition);
      }

      /// <summary>
      /// For each method body, use the extractor to split it into the codeand contract.
      /// </summary>
      public override void TraverseChildren(ISourceMethodBody sourceMethodBody) {
        var codeAndContractPair = ContractExtractor.SplitMethodBodyIntoContractAndCode(this.contractAwareHost, sourceMethodBody,
        this.pdbReader);
        this.contractProvider.AssociateMethodWithContract(sourceMethodBody.MethodDefinition, codeAndContractPair.MethodContract);
      }

    }

    /// <summary>
    /// Returns zero or more interface methods that are implemented by the given method, even if the interface is not
    /// directly implemented by the containing type of the given method. (It may be that the given method is an override
    /// of a method in a base class that directly implements the interface. In fact, that base class might have an abstract
    /// method implementing the interface method.)
    /// </summary>
    /// <remarks>
    /// IMethodDefinitions are returned (as opposed to IMethodReferences) because it isn't possible to find the interface methods
    /// without resolving the interface references to their definitions.
    /// </remarks>
    public static IEnumerable<IMethodDefinition> GetAllImplicitlyImplementedInterfaceMethods(IMethodDefinition implementingMethod) {
      if (!implementingMethod.IsVirtual) yield break;
      List<uint> explicitImplementations = null;
      foreach (IMethodImplementation methodImplementation in implementingMethod.ContainingTypeDefinition.ExplicitImplementationOverrides) {
        if (explicitImplementations == null) explicitImplementations = new List<uint>();
        explicitImplementations.Add(methodImplementation.ImplementedMethod.InternedKey);
      }
      if (explicitImplementations != null) explicitImplementations.Sort();
      var overiddenMethod = MemberHelper.GetImplicitlyOverriddenBaseClassMethod(implementingMethod);
      var implicitImplementations = new List<uint>();
      foreach (ITypeReference interfaceReference in implementingMethod.ContainingTypeDefinition.Interfaces) {
        foreach (ITypeDefinitionMember interfaceMember in interfaceReference.ResolvedType.GetMembersNamed(implementingMethod.Name, false)) {
          IMethodDefinition/*?*/ interfaceMethod = interfaceMember as IMethodDefinition;
          if (interfaceMethod == null) continue;
          if (MemberHelper.MethodsAreEquivalent(implementingMethod, interfaceMethod)) {
            var key = interfaceMethod.InternedKey;
            if (explicitImplementations == null || explicitImplementations.BinarySearch(key) < 0) {
              if (!(overiddenMethod is Dummy)) implicitImplementations.Add(key);
              yield return interfaceMethod;
            }
          }
        }
      }
      if (!(overiddenMethod is Dummy)) {
        foreach (var implicitImplementation in MemberHelper.GetImplicitlyImplementedInterfaceMethods(overiddenMethod)) {
          var key = implicitImplementation.InternedKey;
          if (!implicitImplementations.Contains(key)) {
            implicitImplementations.Add(key);
            yield return implicitImplementation;
          }
        }
      }
    }

    /// <summary>
    /// Returns a modified <paramref name="methodContract"/> so that instead of belonging to <paramref name="fromMethod"/> it
    /// is a method contract for <paramref name="toMethod"/>.
    /// All parameters from <paramref name="fromMethod"/> are replaced with the corresponding
    /// parameters from <paramref name="toMethod"/>.
    /// Any local definitions which are introduced in local declaration statements are modified so that the defining method
    /// each local points to is <paramref name="toMethod"/> instead of <paramref name="fromMethod"/>
    /// </summary>
    public static IMethodContract CopyContractIntoNewContext(IMetadataHost host, IMethodContract methodContract, IMethodDefinition toMethod, IMethodDefinition fromMethod) {

      var result = new CodeAndContractDeepCopier(host).Copy(methodContract);
      var rewriter = new RewriteContractInNewContext(host, toMethod, fromMethod);
      return rewriter.Rewrite(result);
    }

    /// <summary>
    /// A rewriter that substitutes parameters from one method with the parameters
    /// from another method as it is copying. It also re-parents any locals so that they point
    /// to the method the contract is being copied "into".
    /// </summary>
    private class RewriteContractInNewContext : CodeAndContractRewriter {
      private IMethodDefinition targetMethod;
      private IMethodDefinition sourceMethod;
      private ITypeReference targetType;
      private ITypeReference sourceType;
      private List<IParameterDefinition> sourceParameters;
      private List<IParameterDefinition> targetParameters;
      private Dictionary<ILocalDefinition, ILocalDefinition> newLocals = new Dictionary<ILocalDefinition, ILocalDefinition>();

      public RewriteContractInNewContext(IMetadataHost host, IMethodDefinition toMethod, IMethodDefinition fromMethod)
        : base(host, true) {
        this.targetMethod = toMethod;
        this.targetType = toMethod.ContainingType;
        // If it is a method definition, then its containing type can be a generic
        // type definition. Need to have an instance of it to use in rewriting the contract.
        var td = this.targetType as ITypeDefinition;
        if (td != null && td.IsGeneric) {
          this.targetType = td.InstanceType;
        }
        this.sourceMethod = fromMethod;
        this.sourceType = fromMethod.ContainingType;
        this.sourceParameters = new List<IParameterDefinition>(fromMethod.Parameters);
        this.targetParameters = new List<IParameterDefinition>(toMethod.Parameters);
      }

      public override ILocalDefinition Rewrite(ILocalDefinition localDefinition) {
        if (localDefinition.MethodDefinition/*.InternedKey*/ == this.sourceMethod/*.InternedKey*/) {
          ILocalDefinition alreadyReparentedLocal;
          if (!this.newLocals.TryGetValue(localDefinition, out alreadyReparentedLocal)) {
            var ld = new LocalDefinition() {
              MethodDefinition = this.targetMethod,
              Name = localDefinition.Name,
              Type = localDefinition.Type,
            };
            this.newLocals.Add(localDefinition, ld);
            alreadyReparentedLocal = ld;
          }
          return alreadyReparentedLocal;
        }
        return localDefinition;
      }
      /// <summary>
      /// TODO: This is necessary only because the base rewriter for things like TargetExpression call
      /// this method and its definition in the base rewriter is to not visit it, but to just return it.
      /// </summary>
      public override object RewriteReference(ILocalDefinition localDefinition) {
        return this.Rewrite(localDefinition);
      }

      public override IParameterDefinition Rewrite(IParameterDefinition parameterDefinition) {
        if (parameterDefinition.ContainingSignature == this.sourceMethod)
          return this.targetParameters[parameterDefinition.Index];
        return base.Rewrite(parameterDefinition);
      }
      public override object RewriteReference(IParameterDefinition parameterDefinition) {
        if (parameterDefinition.ContainingSignature == this.sourceMethod)
          return this.targetParameters[parameterDefinition.Index];
        return base.Rewrite(parameterDefinition);
      }


      public override void RewriteChildren(ThisReference thisReference) {
        if (TypeHelper.TypesAreEquivalent(thisReference.Type,this.sourceType))
          thisReference.Type = this.targetType;
      }

      /// <summary>
      /// The inherited contract might have a method call of the form "this.M(...)" where "this" had been
      /// a reference type in the original contract (e.g., in an interface contract). But if it is
      /// inherited into a method belonging to a struct (or other value type), then the "this" reference
      /// needs to have its type changed to be a managed pointer.
      /// </summary>
      public override void RewriteChildren(MethodCall methodCall) {
        base.RewriteChildren(methodCall);
        if (!methodCall.IsStaticCall && methodCall.ThisArgument is IThisReference && methodCall.ThisArgument.Type.IsValueType) {
          methodCall.ThisArgument =
              new ThisReference() {
                Type = MutableModelHelper.GetManagedPointerTypeReference(methodCall.ThisArgument.Type, this.host.InternFactory, methodCall.ThisArgument.Type)
              };
        }
      }

    }

    /// <summary>
    /// Given a method definition for a getter or setter that the compiler produced for an auto-property,
    /// mine the type contract and extract contracts from any invariants that mention the property.
    /// If the <paramref name="methodDefinition"/> is a getter, then the returned method contract contains
    /// only postconditions.
    /// If the <paramref name="methodDefinition"/> is a setter, then the returned method contract contains
    /// only preconditions.
    /// If an invariant does not mention the property, then it is not represented in the returned contract.
    /// </summary>
    /// <param name="typeContract">
    /// This must be the type contract corresponding to the containing type of <paramref name="methodDefinition"/>.
    /// </param>
    /// <param name="methodDefinition">
    /// A method definition that should be a getter or setter for an auto-property. If it is not, then null is returned.
    /// </param>
    /// <returns>Either null or a method contract containing pre- or postconditions (mutually exclusive)
    /// mined from the invariants contained in the <paramref name="typeContract"/>.
    /// </returns>
    public static MethodContract/*?*/ GetAutoPropertyContract(IMetadataHost host, ITypeContract typeContract, IMethodDefinition methodDefinition) {
      // If the method was generated for an auto-property, then need to see if a contract can be derived by mining the invariant.
      if (!methodDefinition.IsSpecialName) return null;
      bool isPropertyGetter = methodDefinition.Name.Value.StartsWith("get_");
      bool isPropertySetter = methodDefinition.Name.Value.StartsWith("set_");
      //^ assume !(isPropertyGetter && isPropertySetter); // maybe neither, but never both!
      if (!((isPropertyGetter || isPropertySetter) && ContractHelper.IsAutoPropertyMember(host, methodDefinition))) return null;
      IMethodDefinition getter = null;
      IMethodDefinition setter = null;
      // needs to have both a setter and a getter
      var ct = methodDefinition.ContainingTypeDefinition;
      if (isPropertyGetter) {
        getter = methodDefinition;
        var mms = ct.GetMatchingMembersNamed(host.NameTable.GetNameFor("set_" + methodDefinition.Name.Value.Substring(4)), false, md => ContractHelper.IsAutoPropertyMember(host, md));
        foreach (var mem in mms) {
          setter = mem as IMethodDefinition;
          break;
        }
      } else { // isPropertySetter
        setter = methodDefinition;
        var mms = ct.GetMatchingMembersNamed(host.NameTable.GetNameFor("get_" + methodDefinition.Name.Value.Substring(4)), false, md => ContractHelper.IsAutoPropertyMember(host, md));
        foreach (var mem in mms) {
          getter = mem as IMethodDefinition;
          break;
        }
      }

      // If the auto-property inherits any contracts then it doesn't derive any from the invariant
      var inheritsContract = false;
      IMethodDefinition overriddenMethod = MemberHelper.GetImplicitlyOverriddenBaseClassMethod(getter) as IMethodDefinition;
      var isOverride = getter.IsNewSlot && overriddenMethod != null && !(overriddenMethod is Dummy);
      inheritsContract |= isOverride;
      if (!inheritsContract) {
        inheritsContract |= IteratorHelper.EnumerableIsNotEmpty(ContractHelper.GetAllImplicitlyImplementedInterfaceMethods(getter));
      }
      if (!inheritsContract) {
        inheritsContract |= IteratorHelper.EnumerableIsNotEmpty(MemberHelper.GetExplicitlyOverriddenMethods(getter));
      }

      if (inheritsContract || getter == null || setter == null) return null;

      if (typeContract == null) return null;
      MethodContract derivedMethodContract = null;
      if (isPropertyGetter) {
        var derivedPostConditions = new List<IPostcondition>();
        foreach (var i in typeContract.Invariants) {
          if (!MemberFinder.ExpressionContains(i.Condition, getter)) continue;

          var v = Visibility.MostRestrictiveVisibility(host, i.Condition);
          var currentVisibility = getter.Visibility;
          var intersection = TypeHelper.VisibilityIntersection(v, currentVisibility);
          if (intersection != currentVisibility) continue;

          derivedPostConditions.Add(
            new Postcondition() {
              Condition = ReplaceAutoPropGetter.MakeEnsures(host, getter, i.Condition),
              Description = i.Description,
              OriginalSource = i.OriginalSource,
              Locations = new List<ILocation>(i.Locations),
            });
        }
        if (0 < derivedPostConditions.Count) {
          derivedMethodContract = new MethodContract() {
            Postconditions = derivedPostConditions,
          };
        }
      } else { // isPropertySetter
        var derivedPreconditions = new List<IPrecondition>();
        foreach (var i in typeContract.Invariants) {
          if (!MemberFinder.ExpressionContains(i.Condition, getter)) continue;

          var v = Visibility.MostRestrictiveVisibility(host, i.Condition);
          var currentVisibility = setter.Visibility;
          var intersection = TypeHelper.VisibilityIntersection(v, currentVisibility);
          if (intersection != currentVisibility) continue;

          derivedPreconditions.Add(
            new Precondition() {
              Condition = ReplaceAutoPropGetter.MakeRequires(host, getter, setter, i.Condition),
              Description = i.Description,
              OriginalSource = i.OriginalSource,
              Locations = new List<ILocation>(i.Locations),
            });
        }
        if (0 < derivedPreconditions.Count) {
          derivedMethodContract = new MethodContract() {
            Preconditions = derivedPreconditions,
          };
        }
      }
      return derivedMethodContract;
    }

    private class MemberFinder : CodeTraverser {
      ITypeDefinitionMember memberToFind;
      bool found = false;
      private MemberFinder(ITypeDefinitionMember memberToFind) {
        this.memberToFind = memberToFind;
      }
      public static bool ExpressionContains(IExpression expression, ITypeDefinitionMember member) {
        var mf = new MemberFinder(member);
        mf.Traverse(expression);
        return mf.found;
      }
      public override void TraverseChildren(IMethodReference methodReference) {
        var mr = ContractHelper.UninstantiateAndUnspecialize(methodReference);
        if (mr.InternedKey == (this.memberToFind as IMethodReference).InternedKey) {
          this.StopTraversal = true;
          this.found = true;
          return;
        }
      }

    }

    private class ReplaceAutoPropGetter : CodeRewriter {
      IMethodDefinition getter;
      IMethodDefinition/*?*/ setter;
      private ReplaceAutoPropGetter(IMetadataHost host, IMethodDefinition getter, IMethodDefinition/*?*/ setter)
        : base(host) {
        this.getter = getter;
        this.setter = setter;
      }
      public static IExpression MakeRequires(IMetadataHost host, IMethodDefinition getter, IMethodDefinition setter, IExpression expression) {
        var e = new CodeDeepCopier(host).Copy(expression);
        var rewriter = new ReplaceAutoPropGetter(host, getter, setter);
        return rewriter.Rewrite(e);
      }
      public static IExpression MakeEnsures(IMetadataHost host, IMethodDefinition getter, IExpression expression) {
        var e = new CodeDeepCopier(host).Copy(expression);
        var rewriter = new ReplaceAutoPropGetter(host, getter, null);
        return rewriter.Rewrite(e);
      }
      public override IExpression Rewrite(IMethodCall methodCall) {
        var mr = ContractHelper.UninstantiateAndUnspecialize(methodCall.MethodToCall);
        if (mr.InternedKey == this.getter.InternedKey) {
          if (this.setter != null) {
            var p = IteratorHelper.First(this.setter.Parameters);
            return new BoundExpression() { Definition = p, Instance = null, Type = p.Type, };
          } else {
            return new ReturnValue() { Type = this.getter.Type, };
          }
        }
        return base.Rewrite(methodCall);
      }
    }



  }
  /// <summary>
  /// A mutator that reparents locals defined in the target method so that they point to the source method.
  /// </summary>
  internal sealed class ReparentLocals : CodeAndContractRewriter {
    private IMethodDefinition targetMethod;
    private IMethodDefinition sourceMethod;

    /// <summary>
    /// Creates a mutator that reparents locals defined in the target method so that they point to the source method.
    /// </summary>
    public ReparentLocals(IMetadataHost host, IMethodDefinition targetMethodDefinition, IMethodDefinition sourceMethodDefinition)
      : base(host) {
      this.targetMethod = targetMethodDefinition;
      this.sourceMethod = sourceMethodDefinition;
    }

    public override void RewriteChildren(LocalDefinition localDefinition) {
      if (localDefinition.MethodDefinition.InternedKey == this.targetMethod.InternedKey) {
        localDefinition.MethodDefinition = this.sourceMethod;
      }
      return;
    }
  }
  /// <summary>
  /// A mutator that substitutes parameters defined in the target method with those from the source method
  /// (including the "this" parameter).
  /// </summary>
  public sealed class SubstituteParameters : CodeAndContractRewriter {
    private IMethodDefinition targetMethod;
    private IMethodDefinition sourceMethod;
    private ITypeReference targetType;
    List<IParameterDefinition> parameters;

    /// <summary>
    /// Creates a mutator that replaces all occurrences of parameters from the target method with those from the source method.
    /// </summary>
    public SubstituteParameters(IMetadataHost host, IMethodDefinition targetMethodDefinition, IMethodDefinition sourceMethodDefinition)
      : base(host) {
      this.targetMethod = targetMethodDefinition;
      this.sourceMethod = sourceMethodDefinition;
      this.targetType = targetMethodDefinition.ContainingType;
      this.parameters = new List<IParameterDefinition>(sourceMethod.Parameters);
    }

    /// <summary>
    /// If the <paramref name="addressableExpression"/> represents a parameter of the target method,
    /// it is replaced with the equivalent parameter of the source method.
    /// </summary>
    /// <param name="addressableExpression">The addressable expression.</param>
    public override void RewriteChildren(AddressableExpression addressableExpression) {
      ParameterDefinition/*?*/ par = addressableExpression.Definition as ParameterDefinition;
      if (par != null && par.ContainingSignature == this.targetMethod) {
        addressableExpression.Definition = this.parameters[par.Index];
        if (addressableExpression.Instance != null) {
          addressableExpression.Instance = this.Rewrite(addressableExpression.Instance);
        }
        addressableExpression.Type = this.Rewrite(addressableExpression.Type);
        return;
      } else {
        base.RewriteChildren(addressableExpression);
      }
    }

    /// <summary>
    /// If the <paramref name="boundExpression"/> represents a parameter of the target method,
    /// it is replaced with the equivalent parameter of the source method.
    /// </summary>
    /// <param name="boundExpression">The bound expression.</param>
    public override void RewriteChildren(BoundExpression boundExpression) {
      ParameterDefinition/*?*/ par = boundExpression.Definition as ParameterDefinition;
      if (par != null && par.ContainingSignature == this.targetMethod) {
        boundExpression.Definition = this.parameters[par.Index];
        if (boundExpression.Instance != null) {
          boundExpression.Instance = this.Rewrite(boundExpression.Instance);
        }
        boundExpression.Type = this.Rewrite(boundExpression.Type);
        return;
      } else {
        base.RewriteChildren(boundExpression);
      }
    }

    //public override IParameterDefinition VisitReferenceTo(IParameterDefinition parameterDefinition) {
    //  //The referrer must refer to the same copy of the parameter definition that was (or will be) produced by a visit to the actual definition.
    //  return this.Visit(parameterDefinition);
    //}

    /// <summary>
    /// Replaces the specified this reference with a this reference to the containing type of the target method
    /// </summary>
    /// <param name="thisReference">The this reference.</param>
    /// <returns>a this reference to the containing type of the target method</returns>
    public override IExpression Rewrite(IThisReference thisReference) {
      return new ThisReference() {
        Type = this.Rewrite(this.targetType),
      };
    }
  }

  /// <summary>
  /// A mutator that substitutes expressions for parameters in the expression/code/contract.
  /// </summary>
  public class BetaReducer : CodeAndContractRewriter {
    private IMethodDefinition targetMethod;
    private IMethodDefinition sourceMethod;
    private ITypeReference targetType;
    private ITypeReference sourceType;
    private IThisReference ThisReference;
    private Dictionary<object, IExpression> values = new Dictionary<object, IExpression>();
    private List<IParameterDefinition> fromParameters;
    private  List<IExpression> expressions;

    public BetaReducer(IMetadataHost host, IMethodDefinition fromMethod, IMethodDefinition toMethod, List<IExpression> expressions)
      : base(host) {
      this.fromParameters = new List<IParameterDefinition>(fromMethod.Parameters);
      this.expressions = expressions;
      for (ushort i = 0; i < fromMethod.ParameterCount; i++) {
        this.values.Add(this.fromParameters[i], expressions[i]);
      }
      this.targetMethod = toMethod;
      this.targetType = toMethod.ContainingType;
      this.sourceMethod = fromMethod;
      this.sourceType = fromMethod.ContainingType;
      this.ThisReference = new ThisReference() {
        Type = this.targetType,
      };
    }

    public override void RewriteChildren(LocalDefinition localDefinition) {
      if (localDefinition.MethodDefinition.InternedKey == this.targetMethod.InternedKey)
        localDefinition.MethodDefinition = this.sourceMethod;
      base.RewriteChildren(localDefinition);
    }

    public override IExpression Rewrite(IThisReference thisReference) {
      var t = thisReference.Type;
      var gt = t as IGenericTypeInstanceReference;
      if (gt != null) t = gt.GenericType;
      var k = t.InternedKey;
      if (k == this.sourceType.InternedKey) {
        ITypeReference st = this.targetType;
        return new ThisReference() {
          Type = NamedTypeDefinition.SelfInstance((INamedTypeDefinition)st.ResolvedType, this.host.InternFactory),
        };
      }
      return base.Rewrite(thisReference);
    }

    public override void RewriteChildren(MethodCall methodCall) {
      base.RewriteChildren(methodCall);
      if (!methodCall.IsStaticCall && methodCall.ThisArgument is IThisReference && this.targetType.IsValueType && !this.sourceType.IsValueType) {
        methodCall.ThisArgument =
            new ThisReference() {
              Type = MutableModelHelper.GetManagedPointerTypeReference(methodCall.ThisArgument.Type, this.host.InternFactory, methodCall.ThisArgument.Type)
            };
      }
    }

    public override IExpression Rewrite(IBoundExpression boundExpression) {
      // Can't depend on object identity. A copy might have been made of the paramter
      // definition as part of specializing the contract.
      var p = boundExpression.Definition as IParameterDefinition;
      if (p != null) {
        var md = p.ContainingSignature as IMethodDefinition;
        if (md != null) {
          if (MemberHelper.MethodsAreEquivalent(md, this.sourceMethod)) {
            return this.expressions[p.Index];
          }
        }
      }
      return base.Rewrite(boundExpression);
    }
  }

  public class CodeSpecializer : CodeAndContractRewriter {
    Dictionary<uint, ITypeReference> typeRefMap;
    public CodeSpecializer(IMetadataHost host, Dictionary<uint, ITypeReference> typeRefMap)
      : base(host, true){
      this.typeRefMap = typeRefMap;
    }
    private ITypeReference/*?*/ TryMap(ITypeReference typeReference) {
      ITypeReference mappedTo;
      if (this.typeRefMap.TryGetValue(typeReference.InternedKey, out mappedTo))
        return mappedTo;
      else
        return null;
    }

    public override ITypeReference Rewrite(IGenericMethodParameterReference genericMethodParameterReference) {
      var result = this.TryMap(genericMethodParameterReference);
      return result ?? base.Rewrite(genericMethodParameterReference);
    }
    public override ITypeReference Rewrite(IGenericTypeParameterReference genericTypeParameterReference) {
      var result = this.TryMap(genericTypeParameterReference);
      return result ?? base.Rewrite(genericTypeParameterReference);
    }
    public override void RewriteChildren(SpecializedFieldReference fieldReference) {
      fieldReference.ContainingType = this.Rewrite(fieldReference.ContainingType);
      fieldReference.Type = this.Rewrite(fieldReference.Type);
    }
    public override void RewriteChildren(SpecializedMethodReference specializedMethodReference) {
      specializedMethodReference.ContainingType = this.Rewrite(specializedMethodReference.ContainingType);
      specializedMethodReference.Parameters = this.Rewrite(specializedMethodReference.Parameters);
      specializedMethodReference.Type = this.Rewrite(specializedMethodReference.Type);
    }
    public override void RewriteChildren(SpecializedNestedTypeReference specializedNestedTypeReference) {
      specializedNestedTypeReference.ContainingType = this.Rewrite(specializedNestedTypeReference.ContainingType);
    }
    /// <summary>
    /// Base rewriter doesn't go into the type of the local.
    /// </summary>
    public override object RewriteReference(ILocalDefinition localDefinition) {
      return this.Rewrite(localDefinition);
    }

  }

  internal class SimpleHostEnvironment : MetadataReaderHost, IContractAwareHost {
    PeReader peReader;
    public SimpleHostEnvironment(INameTable nameTable, IInternFactory internFactory)
      : base(nameTable, internFactory, 0, null, false) {
      this.peReader = new PeReader(this);
    }

    public override IUnit LoadUnitFrom(string location) {
      IUnit result = this.peReader.OpenModule(BinaryDocument.GetBinaryDocumentForFile(location, this));
      this.RegisterAsLatest(result);
      return result;
    }


    #region IContractAwareHost Members

    public IContractExtractor GetContractExtractor(UnitIdentity unitIdentity) {
      throw new NotImplementedException();
    }

    #endregion
  }

  /// <summary>
  /// An IContractAwareHost which automatically loads reference assemblies and attaches
  /// a (code-contract aware, aggregating) lazy contract provider to each unit it loads.
  /// This host supports re-loading units that have been updated: it reloads any units
  /// that had been dependent on the updated unit. Clients must re-resolve references
  /// in order to see the updated contents of the units.
  /// </summary>
  public class CodeContractAwareHostEnvironment : MetadataReaderHost, IContractAwareHost {

    #region Fields
    PeReader peReader;
    readonly List<string> libPaths = new List<string>();
    protected Dictionary<UnitIdentity, IContractExtractor> unit2ContractExtractor = new Dictionary<UnitIdentity, IContractExtractor>();
    List<IContractProviderCallback> callbacks = new List<IContractProviderCallback>();
    private List<IMethodDefinition> methodsBeingExtracted = new List<IMethodDefinition>();

    // These next two tables must be kept in sync.
    private Dictionary<string, DateTime> location2LastModificationTime = new Dictionary<string, DateTime>();
    private Dictionary<string, IUnit> location2Unit = new Dictionary<string, IUnit>();

    // A table that maps each unit, U, to all of the units that have a reference to U.
    private Dictionary<UnitIdentity, List<IUnitReference>> unit2DependentUnits = new Dictionary<UnitIdentity, List<IUnitReference>>();
    // A table that maps each unit, U, to all of the reference assemblies for U.
    private Dictionary<IUnitReference, List<IUnitReference>> unit2ReferenceAssemblies = new Dictionary<IUnitReference, List<IUnitReference>>();
    #endregion

    #region Constructors
    /// <summary>
    /// Allocates an object that can be used as an IMetadataHost which automatically loads reference assemblies and attaches
    /// a (lazy) contract provider to each unit it loads.
    /// </summary>
    public CodeContractAwareHostEnvironment()
      : this(new NameTable(), 0, true) {
    }

    /// <summary>
    /// Allocates an object that can be used as an IMetadataHost which automatically loads reference assemblies and attaches
    /// a (lazy) contract provider to each unit it loads.
    /// </summary>
    /// <param name="loadPDBs">Whether PDB files should be loaded by the extractors attached to each unit.</param>
    public CodeContractAwareHostEnvironment(bool loadPDBs)
      : this(new NameTable(), 0, loadPDBs) {
    }

    /// <summary>
    /// Allocates an object that can be used as an IMetadataHost which automatically loads reference assemblies and attaches
    /// a (lazy) contract provider to each unit it loads.
    /// </summary>
    /// <param name="searchPaths">
    /// Initial value for the set of search paths to use.
    /// </param>
    public CodeContractAwareHostEnvironment(IEnumerable<string> searchPaths)
      : this(searchPaths, false, true) {
    }

    /// <summary>
    /// Allocates an object that can be used as an IMetadataHost which automatically loads reference assemblies and attaches
    /// a (lazy) contract provider to each unit it loads.
    /// </summary>
    /// <param name="searchPaths">
    /// Initial value for the set of search paths to use.
    /// </param>
    /// <param name="searchInGAC">
    /// Whether the GAC (Global Assembly Cache) should be searched when resolving references.
    /// </param>
    public CodeContractAwareHostEnvironment(IEnumerable<string> searchPaths, bool searchInGAC)
      : this(searchPaths, searchInGAC, true) {
    }

    /// <summary>
    /// Allocates an object that can be used as an IMetadataHost which automatically loads reference assemblies and attaches
    /// a (lazy) contract provider to each unit it loads.
    /// </summary>
    /// <param name="searchPaths">
    /// Initial value for the set of search paths to use.
    /// </param>
    /// <param name="searchInGAC">
    /// Whether the GAC (Global Assembly Cache) should be searched when resolving references.
    /// </param>
    /// <param name="loadPDBs">Whether PDB files should be loaded by the extractors attached to each unit.</param>
    public CodeContractAwareHostEnvironment(IEnumerable<string> searchPaths, bool searchInGAC, bool loadPDBs)
      : base(new NameTable(), new InternFactory(), 0, searchPaths, searchInGAC) {
      this.peReader = new PeReader(this);
      this.AllowExtractorsToUsePdbs = loadPDBs;
    }

    /// <summary>
    /// Allocates an object that provides an abstraction over the application hosting compilers based on this framework.
    /// </summary>
    /// <param name="nameTable">
    /// A collection of IName instances that represent names that are commonly used during compilation.
    /// This is a provided as a parameter to the host environment in order to allow more than one host
    /// environment to co-exist while agreeing on how to map strings to IName instances.
    /// </param>
    public CodeContractAwareHostEnvironment(INameTable nameTable)
      : this(nameTable, 0, true) {
    }

    /// <summary>
    /// Allocates an object that provides an abstraction over the application hosting compilers based on this framework.
    /// </summary>
    /// <param name="nameTable">
    /// A collection of IName instances that represent names that are commonly used during compilation.
    /// This is a provided as a parameter to the host environment in order to allow more than one host
    /// environment to co-exist while agreeing on how to map strings to IName instances.
    /// </param>
    /// <param name="pointerSize">The size of a pointer on the runtime that is the target of the metadata units to be loaded
    /// into this metadta host. This parameter only matters if the host application wants to work out what the exact layout
    /// of a struct will be on the target runtime. The framework uses this value in methods such as TypeHelper.SizeOfType and
    /// TypeHelper.TypeAlignment. If the host application does not care about the pointer size it can provide 0 as the value
    /// of this parameter. In that case, the first reference to IMetadataHost.PointerSize will probe the list of loaded assemblies
    /// to find an assembly that either requires 32 bit pointers or 64 bit pointers. If no such assembly is found, the default is 32 bit pointers.
    /// </param>
    /// <param name="loadPDBs">Whether PDB files should be loaded by the extractors attached to each unit.</param>
    public CodeContractAwareHostEnvironment(INameTable nameTable, byte pointerSize, bool loadPDBs)
      : base(nameTable, new InternFactory(), pointerSize, null, false)
      //^ requires pointerSize == 0 || pointerSize == 4 || pointerSize == 8;
    {
      this.peReader = new PeReader(this);
      this.AllowExtractorsToUsePdbs = loadPDBs;
    }
    #endregion Constructors

    #region Methods introduced by this class
    /// <summary>
    /// Set this before loading any units with this host. Default is true.
    /// Note that extractors may use PDB file to open source files.
    /// Both PDB and source files may be opened with exclusive access.
    /// </summary>
    public virtual bool AllowExtractorsToUsePdbs { get; protected set; }

    /// <summary>
    /// Adds a new pair of (assembly name, path) to the table of candidates to use
    /// when searching for a unit to load. Overwrites previous entry if the assembly
    /// name is already in the table. Note that "assembly name" does not have an
    /// extension.
    /// </summary>
    /// <param name="path">
    /// A valid path in the file system that ends with a file name. The
    /// file name (without extension) is used as the key in the candidate
    /// table.
    /// </param>
    /// <returns>
    /// Returns true iff <paramref name="path"/> is a valid path pointing
    /// to an existing file and the table was successfully updated.
    /// </returns>
    public virtual bool AddResolvedPath(string path) {
      if (path == null) return false;
      if (!File.Exists(path)) return false;
      var fileNameWithExtension = Path.GetFileName(path);
      if (String.IsNullOrEmpty(fileNameWithExtension)) return false;
      var fileName = Path.GetFileNameWithoutExtension(path);
      if (this.assemblyNameToPath.ContainsKey(fileName))
        this.assemblyNameToPath[fileName] = path;
      else
        this.assemblyNameToPath.Add(fileName, path);
      return true;
    }
    private Dictionary<string, string> assemblyNameToPath = new Dictionary<string, string>();
    #endregion

    #region MetadataReaderHost Overrides
    public override AssemblyIdentity ProbeAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly) {
      string pathFromTable;
      var assemblyName = referencedAssembly.Name.Value;
      if (this.assemblyNameToPath.TryGetValue(assemblyName, out pathFromTable)) {
        return new AssemblyIdentity(referencedAssembly, pathFromTable);
      } else {
        return base.ProbeAssemblyReference(referringUnit, referencedAssembly);
      }
    }

    /// <summary>
    /// Returns the unit that is stored at the given location, or a dummy unit if no unit exists at that location or if the unit at that location is not accessible.
    /// </summary>
    public override IUnit LoadUnitFrom(string location) {
      if (location.StartsWith("file://")) { // then it is a URL
        try {
          Uri u = new Uri(location, UriKind.Absolute); // Let the Uri class figure out how to go from URL to local file path
          location = u.LocalPath;
        } catch (UriFormatException) {
          return Dummy.Unit;
        }
      }

      string pathFromTable;
      var assemblyName = Path.GetFileNameWithoutExtension(location);
      if (this.assemblyNameToPath.TryGetValue(assemblyName, out pathFromTable)) {
        location = pathFromTable;
      }

      var unloadedOrFirstTime = UnloadPreviouslyLoadedUnitIfLocationIsNewer(location);
      IUnit result = this.peReader.OpenModule(BinaryDocument.GetBinaryDocumentForFile(Path.GetFullPath(location), this));

      this.RegisterAsLatest(result);

      if (unloadedOrFirstTime) {
        foreach (var d in result.UnitReferences) {
          var key = d.UnitIdentity;
          if (!this.unit2DependentUnits.ContainsKey(key)) {
            this.unit2DependentUnits[key] = new List<IUnitReference>();
          }
          this.unit2DependentUnits[key].Add(result);
        }

        this.unit2ContractExtractor.Add(result.UnitIdentity, null); // a marker to communicate with GetContractExtractor
        this.location2Unit[result.Location] = result;
      }
      return result;
    }

    #region Helper Methods
    /// <summary>
    /// Checks the location against all previously loaded locations. If a unit
    /// had been loaded from it before and if the last write time is newer, then
    /// the previously loaded unit, all units dependent on it, and all reference
    /// assemblies for it are deleted from the cache and a new PeReader is created
    /// for reading in all future units.
    /// </summary>
    /// <returns>
    /// true iff (unit was unloaded or this is the first time this location has been seen)
    /// the latter case should be for the first time the unit has been loaded.
    /// </returns>
    private bool UnloadPreviouslyLoadedUnitIfLocationIsNewer(string location) {
      location = Path.GetFullPath(location);
      var timeOfLastModification = File.GetLastWriteTime(location);
      DateTime previousTime;
      if (!this.location2LastModificationTime.TryGetValue(location, out previousTime)) {
        this.location2LastModificationTime.Add(location, timeOfLastModification);
        return true;
      }
      if (!(previousTime.CompareTo(timeOfLastModification) < 0))
        return false;

      // file has been modified. Need to throw away PeReader because it caches based on identity and
      // won't actually read in and construct an object model from the file. Even though at this
      // point we don't even know what assembly is at this location. Maybe it isn't even an updated
      // version, but instead some completely different assembly?
      this.peReader = new PeReader(this);

      var unit = this.location2Unit[location];
      // Need to dump all units that depended on this one because their reference is now stale.
      List<IUnitReference> referencesToDependentUnits;
      if (this.unit2DependentUnits.TryGetValue(unit.UnitIdentity, out referencesToDependentUnits)) {
        foreach (var d in referencesToDependentUnits) {
          this.RemoveUnit(d.UnitIdentity);
        }
      }

      // Need to remove unit from the list of dependent units for each unit
      // it *was* dependent on in case the newer version has a changed list of
      // dependencies.
      foreach (var d in unit.UnitReferences) {
        this.unit2DependentUnits[d.UnitIdentity].Remove(unit);
      }

      // Dump all reference assemblies for this unit.
      List<IUnitReference> referenceAssemblies;
      if (this.unit2ReferenceAssemblies.TryGetValue(unit, out referenceAssemblies)) {
        foreach (var d in referenceAssemblies) {
          this.RemoveUnit(d.UnitIdentity);
        }
      }

      // Dump stale version from cache
      this.RemoveUnit(unit.UnitIdentity);
      return true;
    }

    /// <summary>
    /// If the unit is a reference assembly, then just attach a contract extractor to it.
    /// Otherwise, create an aggregating extractor that encapsulates the unit and any
    /// reference assemblies that are found on the search path.
    /// Each contract extractor is actually a composite comprising a code-contracts
    /// extractor layered on top of a lazy extractor.
    /// </summary>
    protected void AttachContractExtractorAndLoadReferenceAssembliesFor(IUnit alreadyLoadedUnit) {

      // Because of unification, the "alreadyLoadedUnit" might have actually already been loaded previously
      // and gone through here (and so already has a contract provider attached to it).
      if (this.unit2ContractExtractor.ContainsKey(alreadyLoadedUnit.UnitIdentity))
        this.unit2ContractExtractor.Remove(alreadyLoadedUnit.UnitIdentity);
      //return;

      var contractMethods = new ContractMethods(this);
      using (var lazyContractProviderForLoadedUnit = new LazyContractExtractor(this, alreadyLoadedUnit, contractMethods, this.AllowExtractorsToUsePdbs)) {
        var contractProviderForLoadedUnit = new CodeContractsContractExtractor(this, lazyContractProviderForLoadedUnit);
        if (ContractHelper.IsContractReferenceAssembly(this, alreadyLoadedUnit)) {
          // If we're asked to explicitly load a reference assembly, then go ahead and attach a contract provider to it,
          // but *don't* look for reference assemblies for *it*.
          this.unit2ContractExtractor.Add(alreadyLoadedUnit.UnitIdentity, contractProviderForLoadedUnit);
        } else {
          #region Load any reference assemblies for the loaded unit
          var loadedAssembly = alreadyLoadedUnit as IAssembly; // Only assemblies can have associated reference assemblies.
          var oobProvidersAndHosts = new List<KeyValuePair<IContractProvider, IMetadataHost>>();
          if (loadedAssembly != null) {
            var refAssemWithoutLocation =
              new AssemblyIdentity(this.NameTable.GetNameFor(alreadyLoadedUnit.Name.Value + ".Contracts"),
                loadedAssembly.AssemblyIdentity.Culture,
                loadedAssembly.AssemblyIdentity.Version,
                loadedAssembly.AssemblyIdentity.PublicKeyToken,
                "");
            var referenceAssemblyIdentity = this.ProbeAssemblyReference(alreadyLoadedUnit, refAssemWithoutLocation);
            IUnit referenceUnit = null;
            IContractAwareHost hostForReferenceAssembly = this; // default
            if (referenceAssemblyIdentity.Location.Equals("unknown://location")) {
              // It might be the case that this was returned because the identity constructed for it had the wrong version number
              // (or something else that didn't match the identity of hte already loaded unit). But it might be that the probing
              // logic succeeded in loading *some* reference assembly. And we're not picky: the reference assembly is just
              // the first assembly found with the right name.
              foreach (var u in this.LoadedUnits) {
                if (u.Name.Equals(refAssemWithoutLocation.Name)) {
                  // fine, use this one!
                  referenceUnit = u;
                  break;
                }
              }
              if (referenceUnit != null && loadedAssembly.AssemblyIdentity.Equals(this.CoreAssemblySymbolicIdentity)) {
                // Need to use a separate host because the reference assembly for the core assembly thinks *it* is the core assembly
                var separateHost = new SimpleHostEnvironment(this.NameTable, this.InternFactory);
                this.disposableObjectAllocatedByThisHost.Add(separateHost);
                referenceUnit = separateHost.LoadUnitFrom(referenceUnit.Location);
                hostForReferenceAssembly = separateHost;
              }
            } else {
              // referenceAssemblyIdentity.Location != "unknown://location")
              #region Load reference assembly
              if (loadedAssembly.AssemblyIdentity.Equals(this.CoreAssemblySymbolicIdentity)) {
                // Need to use a separate host because the reference assembly for the core assembly thinks *it* is the core assembly
                var separateHost = new SimpleHostEnvironment(this.NameTable, this.InternFactory);
                this.disposableObjectAllocatedByThisHost.Add(separateHost);
                referenceUnit = separateHost.LoadUnitFrom(referenceAssemblyIdentity.Location);
                hostForReferenceAssembly = separateHost;
              } else {
                // Load reference assembly, but don't cause a recursive call!! So don't call LoadUnit or LoadUnitFrom
                referenceUnit = this.peReader.OpenModule(BinaryDocument.GetBinaryDocumentForFile(referenceAssemblyIdentity.Location, this));
                this.RegisterAsLatest(referenceUnit);
              }
              #endregion
            }
            #region Attach a contract provider to it
            if (referenceUnit != null && !(referenceUnit is Dummy)) {
              IAssembly referenceAssembly = referenceUnit as IAssembly;
              if (referenceAssembly != null) {
                var referenceAssemblyContractProvider = new CodeContractsContractExtractor(hostForReferenceAssembly,
                  new LazyContractExtractor(hostForReferenceAssembly, referenceAssembly, contractMethods, this.AllowExtractorsToUsePdbs));
                oobProvidersAndHosts.Add(new KeyValuePair<IContractProvider, IMetadataHost>(referenceAssemblyContractProvider, hostForReferenceAssembly));
                if (!this.unit2ReferenceAssemblies.ContainsKey(alreadyLoadedUnit)) {
                  this.unit2ReferenceAssemblies[alreadyLoadedUnit] = new List<IUnitReference>();
                }
                // Reference assemblies don't have references to the real assembly but they are "dependent"
                // on them in that they should be dumped and reloaded if the real assembly changes.
                this.unit2ReferenceAssemblies[alreadyLoadedUnit].Add(referenceAssembly);
              }
            }
            #endregion
          }
          var aggregateContractProvider = new AggregatingContractExtractor(this, contractProviderForLoadedUnit, oobProvidersAndHosts);
          this.unit2ContractExtractor.Add(alreadyLoadedUnit.UnitIdentity, aggregateContractProvider);
          #endregion Load any reference assemblies for the loaded unit
        }
        foreach (var c in this.callbacks) {
          contractProviderForLoadedUnit.RegisterContractProviderCallback(c);
        }
      }
    }
    #endregion
    #endregion

    #region IContractAwareHost Members

    /// <summary>
    /// If a unit has been loaded with this host, then it will have attached a (lazy) contract provider to that unit.
    /// This method returns that contract provider. If the unit has not been loaded by this host, then null is returned.
    /// </summary>
    public IContractExtractor/*?*/ GetContractExtractor(UnitIdentity unitIdentity) {
      IContractExtractor cp;
      if (!this.unit2ContractExtractor.TryGetValue(unitIdentity, out cp))
        return null;
      if (cp == null) {
        foreach (var u in this.LoadedUnits) {
          if (u.UnitIdentity.Equals(unitIdentity)) {
            this.AttachContractExtractorAndLoadReferenceAssembliesFor(u);
            cp = this.unit2ContractExtractor[unitIdentity];
            break;
          }
        }
      }
      return cp;
    }

    /// <summary>
    /// The host will register this callback with each contract provider it creates.
    /// </summary>
    /// <param name="contractProviderCallback"></param>
    public void RegisterContractProviderCallback(IContractProviderCallback contractProviderCallback) {
      this.callbacks.Add(contractProviderCallback);
    }

    #endregion
  }

  /// <summary>
  /// A host that is a subtype of Microsoft.Cci.PeReader.DefaultHost that also maintains
  /// a (mutable) table mapping assembly names to paths.
  /// When an assembly is to be loaded, if its name is in the table, then the
  /// associated path is used to load it.
  /// </summary>
  public class FullyResolvedPathHost : Microsoft.Cci.PeReader.DefaultHost {

    private Dictionary<string, string> assemblyNameToPath = new Dictionary<string, string>();

    /// <summary>
    /// Adds a new pair of (assembly name, path) to the table of candidates to use
    /// when searching for a unit to load. Overwrites previous entry if the assembly
    /// name is already in the table. Note that "assembly name" does not have an
    /// extension.
    /// </summary>
    /// <param name="path">
    /// A valid path in the file system that ends with a file name. The
    /// file name (without extension) is used as the key in the candidate
    /// table.
    /// </param>
    /// <returns>
    /// Returns true iff <paramref name="path"/> is a valid path pointing
    /// to an existing file and the table was successfully updated.
    /// </returns>
    public virtual bool AddResolvedPath(string path) {
      if (path == null) return false;
      if (!File.Exists(path)) return false;
      var fileNameWithExtension = Path.GetFileName(path);
      if (String.IsNullOrEmpty(fileNameWithExtension)) return false;
      var fileName = Path.GetFileNameWithoutExtension(path);
      if (this.assemblyNameToPath.ContainsKey(fileName))
        this.assemblyNameToPath[fileName] = path;
      else
        this.assemblyNameToPath.Add(fileName, path);
      return true;
    }

    public override IUnit LoadUnitFrom(string location) {
      string pathFromTable;
      var assemblyName = Path.GetFileNameWithoutExtension(location);
      if (this.assemblyNameToPath.TryGetValue(assemblyName, out pathFromTable)) {
        return base.LoadUnitFrom(pathFromTable);
      } else {
        return base.LoadUnitFrom(location);
      }
    }

    public override AssemblyIdentity ProbeAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly) {
      string pathFromTable;
      var assemblyName = referencedAssembly.Name.Value;
      if (this.assemblyNameToPath.TryGetValue(assemblyName, out pathFromTable)) {
        return new AssemblyIdentity(referencedAssembly, pathFromTable);
      } else {
        return base.ProbeAssemblyReference(referringUnit, referencedAssembly);
      }
    }


  }


}
