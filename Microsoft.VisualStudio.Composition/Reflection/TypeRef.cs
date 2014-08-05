﻿namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class TypeRef : IEquatable<TypeRef>, IEquatable<Type>
    {
        /// <summary>
        /// A cache of TypeRef instances that correspond to Type instances.
        /// </summary>
        /// <remarks>
        /// This is for efficiency to avoid duplicates where convenient to do so.
        /// It is not intended as a guarantee of reference equality across equivalent TypeRef instances.
        /// </remarks>
        private static readonly Dictionary<Type, WeakReference<TypeRef>> instanceCache = new Dictionary<Type, WeakReference<TypeRef>>();

        private TypeRef(AssemblyName assemblyName, int metadataToken, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.NotNull(assemblyName, "assemblyName");

            this.AssemblyName = assemblyName;
            this.MetadataToken = metadataToken;
            this.GenericTypeParameterCount = genericTypeParameterCount;
            this.GenericTypeArguments = genericTypeArguments;
        }

        private TypeRef(Type type)
        {
            this.AssemblyName = type.Assembly.GetName();
            this.MetadataToken = type.MetadataToken;
            this.GenericTypeParameterCount = type.GetTypeInfo().GenericTypeParameters.Length;
            this.GenericTypeArguments = type.GenericTypeArguments != null && type.GenericTypeArguments.Length > 0
                ? type.GenericTypeArguments.Select(t => new TypeRef(t)).ToImmutableArray()
                : ImmutableArray<TypeRef>.Empty;
        }

        public AssemblyName AssemblyName { get; private set; }

        public int MetadataToken { get; private set; }

        public int GenericTypeParameterCount { get; private set; }

        public ImmutableArray<TypeRef> GenericTypeArguments { get; private set; }

        public bool IsGenericTypeDefinition
        {
            get { return this.GenericTypeParameterCount > 0 && this.GenericTypeArguments.Length == 0; }
        }

        public static TypeRef Get(AssemblyName assemblyName, int metadataToken, int genericTypeParameterCount, ImmutableArray<TypeRef> genericTypeArguments)
        {
            return new TypeRef(assemblyName, metadataToken, genericTypeParameterCount, genericTypeArguments);
        }

        public static TypeRef Get(Type type)
        {
            if (type == null)
            {
                return null;
            }

            TypeRef result;
            lock (instanceCache)
            {
                WeakReference<TypeRef> weakResult;
                if (!instanceCache.TryGetValue(type, out weakResult))
                {
                    result = new TypeRef(type);
                    instanceCache.Add(type, new WeakReference<TypeRef>(result));
                }
                else
                {
                    if (!weakResult.TryGetTarget(out result))
                    {
                        result = new TypeRef(type);
                        weakResult.SetTarget(result);
                    }
                }
            }

            return result;
        }

        public TypeRef MakeGenericType(ImmutableArray<TypeRef> genericTypeArguments)
        {
            Requires.Argument(!genericTypeArguments.IsDefault, "genericTypeArguments", "Not initialized.");
            Verify.Operation(this.IsGenericTypeDefinition, "This is not a generic type definition.");
            return new Reflection.TypeRef(this.AssemblyName, this.MetadataToken, this.GenericTypeParameterCount, genericTypeArguments);
        }

        public override int GetHashCode()
        {
            return this.MetadataToken;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRef && this.Equals((TypeRef)obj);
        }

        public bool Equals(TypeRef other)
        {
            return ByValueEquality.AssemblyName.Equals(this.AssemblyName, other.AssemblyName)
                && this.MetadataToken == other.MetadataToken
                && this.GenericTypeParameterCount == other.GenericTypeParameterCount
                && this.GenericTypeArguments.EqualsByValue(other.GenericTypeArguments);
        }

        public bool Equals(Type other)
        {
            return this.Equals(TypeRef.Get(other));
        }
    }
}