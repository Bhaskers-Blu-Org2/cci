//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Diagnostics;

//^ using Microsoft.Contracts;

namespace Microsoft.Cci.Ast {

  /// <summary>
  /// 
  /// </summary>
  public class NestedUnitNamespace : UnitNamespace, INestedUnitNamespace {

    /// <summary>
    /// 
    /// </summary>
    /// <param name="containingUnitNamespace"></param>
    /// <param name="name"></param>
    /// <param name="unit"></param>
    public NestedUnitNamespace(IUnitNamespace containingUnitNamespace, IName name, IUnit unit)
      : base(name, unit)
      //^ requires containingUnitNamespace is RootUnitNamespace || containingUnitNamespace is NestedUnitNamespace;
    {
      this.containingUnitNamespace = containingUnitNamespace;
    }

    /// <summary>
    /// The unit namespace that contains this member.
    /// </summary>
    /// <value></value>
    public IUnitNamespace ContainingUnitNamespace {
      [DebuggerNonUserCode]
      get
        //^ ensures result is RootUnitNamespace || result is NestedUnitNamespace;
      {
        return this.containingUnitNamespace;
      }
    }
    readonly IUnitNamespace containingUnitNamespace;
    //^ invariant containingUnitNamespace is RootUnitNamespace || containingUnitNamespace is NestedUnitNamespace;

    /// <summary>
    /// Calls the visitor.Visit(INestedUnitNamespace) method.
    /// </summary>
    public override void Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
      if (this.ContainingUnitNamespace is IRootUnitNamespace) return this.Name.Value;
      return this.ContainingUnitNamespace.ToString() + "." + this.Name.Value;
    }

    #region INamespaceMember Members

    INamespaceDefinition INamespaceMember.ContainingNamespace {
      [DebuggerNonUserCode]
      get { return this.ContainingUnitNamespace; }
    }

    #endregion

    #region IScopeMember<IScope<INamespaceMember>> Members

    /// <summary>
    /// The scope instance with a Members collection that includes this instance.
    /// </summary>
    /// <value></value>
    public IScope<INamespaceMember> ContainingScope {
      [DebuggerNonUserCode]
      get { return this.ContainingUnitNamespace; }
    }

    #endregion

    #region IContainerMember<INamespace> Members

    /// <summary>
    /// The container instance with a Members collection that includes this instance.
    /// </summary>
    /// <value></value>
    public INamespaceDefinition Container {
      [DebuggerNonUserCode]
      get { return this.ContainingUnitNamespace; }
    }

    #endregion

    #region IUnitNamespaceReference Members

    IUnitReference IUnitNamespaceReference.Unit {
      [DebuggerNonUserCode]
      get { return this.Unit; }
    }

    #endregion

    #region INestedUnitNamespaceReference Members

    IUnitNamespaceReference INestedUnitNamespaceReference.ContainingUnitNamespace {
      [DebuggerNonUserCode]
      get { return this.ContainingUnitNamespace; }
    }

    /// <summary>
    /// The namespace definition being referred to.
    /// </summary>
    /// <value></value>
    public INestedUnitNamespace ResolvedNestedUnitNamespace {
      [DebuggerNonUserCode]
      get { return this; }
    }

    #endregion
  }

  /// <summary>
  /// A named collection of namespace members, with routines to search and maintain the collection. All the members belong to an associated
  /// IUnit instance.
  /// </summary>
  public abstract class UnitNamespace : AggregatedNamespace<NamespaceDeclaration, IAggregatableNamespaceDeclarationMember>, IUnitNamespace {

    /// <summary>
    /// Allocates a named collection of namespace members, with routines to search and maintain the collection. All the members belong to an associated
    /// IUnit instance.
    /// </summary>
    /// <param name="name">The name of the namespace.</param>
    /// <param name="unit">The IUnit instance associated with this namespace.</param>
    protected UnitNamespace(IName name, IUnit unit)
      : base(name) {
      this.unit = unit;
    }

    /// <summary>
    /// The members of this namespace definition are the aggregation of the members of zero or more namespace declarations. This
    /// method adds another namespace declaration of the list of declarations that supply the members of this namespace definition.
    /// </summary>
    /// <param name="declaration">The namespace declaration to add to the list of declarations that together define this namespace definition.</param>
    protected internal void AddNamespaceDeclaration(NamespaceDeclaration declaration) {
      this.namespaceDeclarations.Add(declaration);
      this.AddContainer(declaration);
    }

    /// <summary>
    /// A collection of metadata custom attributes that are associated with this definition.
    /// </summary>
    public override IEnumerable<ICustomAttribute> Attributes {
      [DebuggerNonUserCode]
      get {
        foreach (NamespaceDeclaration nsDecl in this.namespaceDeclarations) {
          foreach (SourceCustomAttribute attr in nsDecl.SourceAttributes)
            yield return new CustomAttribute(attr);
        }
      }
    }

    /// <summary>
    /// The list of namespace declarations that together define this namespace definition.
    /// </summary>
    public IEnumerable<NamespaceDeclaration> NamespaceDeclarations {
      [DebuggerNonUserCode]
      get {
        return this.namespaceDeclarations.AsReadOnly();
      }
    }
    List<NamespaceDeclaration> namespaceDeclarations = new List<NamespaceDeclaration>();

    /// <summary>
    /// Appends all of the type members of the given namespace (as well as those of its nested namespaces and types) to
    /// the given list of types.
    /// </summary>
    /// <param name="nameSpace">The namespace to (recursively) traverse to find nested types.</param>
    /// <param name="typeList">A mutable list of types to which any types found inside the given namespace will be appended.</param>
    internal static void FillInWithTypes(INamespaceDefinition nameSpace, List<INamedTypeDefinition> typeList) {
      foreach (INamespaceMember member in nameSpace.Members) {
        INamedTypeDefinition/*?*/ type = member as INamedTypeDefinition;
        if (type != null)
          FillInWithNestedTypes(type, typeList);
        else {
          INamespaceDefinition/*?*/ ns = member as INamespaceDefinition;
          if (ns != null)
            FillInWithTypes(ns, typeList);
        }
      }
    }

    /// <summary>
    /// Appends all of the type members of the given type definition (as well as those of its nested types) to the given list of types.
    /// </summary>
    /// <param name="typeDefinition">The type definition to (recursively) traverse to find nested types.</param>
    /// <param name="typeList">A mutable list of types to which any types found inside the given namespace will be appended.</param>
    private static void FillInWithNestedTypes(INamedTypeDefinition typeDefinition, List<INamedTypeDefinition> typeList) {
      typeList.Add(typeDefinition);
      foreach (ITypeDefinitionMember member in typeDefinition.Members) {
        INamedTypeDefinition/*?*/ nestedType = member as INamedTypeDefinition;
        if (nestedType != null)
          FillInWithNestedTypes(nestedType, typeList);
      }
    }

    /// <summary>
    /// Finds or creates an aggregated member instance corresponding to the given member. Usually this should result in the given member being added to the declarations
    /// collection of the aggregated member.
    /// </summary>
    /// <param name="member">The member to aggregate.</param>
    protected override INamespaceMember GetAggregatedMember(IAggregatableNamespaceDeclarationMember member) {
      return member.AggregatedMember;
    }

    #region IUnitNamespace Members

    /// <summary>
    /// The IUnit instance associated with this namespace.
    /// </summary>
    public IUnit Unit {
      [DebuggerNonUserCode]
      get { return this.unit; }
    }
    IUnit unit;

    #endregion

    #region INamespace Members

    /// <summary>
    /// The object associated with the namespace. For example an IUnit or IUnitSet instance. This namespace is either the root namespace of that object
    /// or it is a nested namespace that is directly of indirectly nested in the root namespace.
    /// </summary>
    public sealed override INamespaceRootOwner RootOwner {
      [DebuggerNonUserCode]
      get { return this.unit; }
    }

    #endregion

    #region IDefinition Members

    /// <summary>
    /// The source locations of the zero or more namespace declarations that together define this namespace definition.
    /// </summary>
    public sealed override IEnumerable<ILocation> Locations {
      [DebuggerNonUserCode]
      get {
        foreach (NamespaceDeclaration declaration in this.namespaceDeclarations)
          yield return declaration.SourceLocation;
      }
    }

    #endregion

    #region IUnitNamespaceReference Members

    IUnitReference IUnitNamespaceReference.Unit {
      [DebuggerNonUserCode]
      get { return this.unit; }
    }

    /// <summary>
    /// The namespace definition being referred to.
    /// </summary>
    /// <value></value>
    public IUnitNamespace ResolvedUnitNamespace {
      [DebuggerNonUserCode]
      get { return this; }
    }

    #endregion
  }

  /// <summary>
  /// A unit namespace that is not nested inside another namespace.
  /// </summary>
  public class RootUnitNamespace : UnitNamespace, IRootUnitNamespace {

    /// <summary>
    /// Allocates a unit namespace that is not nested inside another namespace.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="unit"></param>
    public RootUnitNamespace(IName name, IUnit unit)
      : base(name, unit) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="container"></param>
    protected override void AddContainer(NamespaceDeclaration container) {
      base.AddContainer(container);
    }

    /// <summary>
    /// Calls visitor.Visit(IRootUnitNamespace).
    /// </summary>
    public override void Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
      return "global::";
    }

  }

}
